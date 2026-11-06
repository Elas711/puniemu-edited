using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;

namespace Puniemu.Src.Server.Infrastructure
{
    // Exposes a fully buffered request body through Request.BodyReader.
    // The game handlers read the body with a single BodyReader.ReadAsync() call, which only returns
    // the first chunk of a large request. Pre-filling a pipe with the complete body makes that single
    // read return everything.
    internal sealed class BufferedBodyPipeFeature : IRequestBodyPipeFeature
    {
        public BufferedBodyPipeFeature(PipeReader reader) { Reader = reader; }
        public PipeReader Reader { get; }
    }

    // Per-request information extracted by the middleware.
    public static class RequestInfo
    {
        public const string GDKEY = "puniemu.gdkey";
        public const string UDKEY = "puniemu.udkey";
        public const string JSON = "puniemu.json";

        public static string? Gdkey(HttpContext ctx) => ctx.Items.TryGetValue(GDKEY, out var v) ? v as string : null;
        public static string? Udkey(HttpContext ctx) => ctx.Items.TryGetValue(UDKEY, out var v) ? v as string : null;
        public static JObject? Json(HttpContext ctx) => ctx.Items.TryGetValue(JSON, out var v) ? v as JObject : null;
    }

    // Runs in front of every game request (.nhn POST):
    //  * buffers the whole body (fixes truncated reads of big requests),
    //  * decrypts it once to learn which save (gdkey) and device (udkey) it is for,
    //  * enforces maintenance mode and bans,
    //  * optionally checks the device actually owns the save,
    //  * serializes requests for the same save so parallel requests can't duplicate currency,
    //  * turns unhandled exceptions into an in-game error dialog instead of a dropped connection,
    //  * records metrics.
    public static class GameRequestMiddleware
    {
        private const int MAX_BODY_BYTES = 32 * 1024 * 1024;
        private static readonly TimeSpan LOCK_TIMEOUT = TimeSpan.FromSeconds(30);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _saveLocks = new();
        private static readonly ConcurrentDictionary<string, byte> _verifiedOwnership = new();
        private static readonly ConcurrentDictionary<string, byte> _reportedOwnership = new();

        public static bool LogRequests { get; set; } = false;

        // Where each save last played from. In-game web pages only carry the userId (which other
        // players can see), so web actions that change a save also need a game request for that
        // save from the same address in the last hours.
        private static readonly ConcurrentDictionary<string, (string ip, DateTime at)> _lastSeen = new();
        private static readonly TimeSpan SEEN_WINDOW = TimeSpan.FromHours(12);

        private static string? Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString();

        public static bool IsFromPlayerDevice(HttpContext ctx, string gdkey)
        {
            var ip = Ip(ctx);
            if (ip == null) return true; // no address (tests, unix sockets): nothing to compare
            return _lastSeen.TryGetValue(gdkey, out var seen) && seen.ip == ip && DateTime.UtcNow - seen.at < SEEN_WINDOW;
        }

        // Runs a change to a save outside of a game request (web pages) under the same per-save
        // lock as the game requests, so neither overwrites the other's changes.
        public static async Task<T> WithSaveLockAsync<T>(string gdkey, Func<Task<T>> action)
        {
            var l = _saveLocks.GetOrAdd(gdkey, _ => new SemaphoreSlim(1, 1));
            if (!await l.WaitAsync(LOCK_TIMEOUT)) throw new TimeoutException("save is busy");
            try { return await action(); }
            finally { l.Release(); }
        }

        // Called when saves move between devices so the ownership cache is rebuilt.
        public static void ForgetOwnership() => _verifiedOwnership.Clear();

        public static async Task InvokeAsync(HttpContext ctx, Func<Task> next)
        {
            var path = ctx.Request.Path.Value ?? "";
            bool isGameRequest = HttpMethods.IsPost(ctx.Request.Method) && path.Contains(".nhn", StringComparison.OrdinalIgnoreCase);
            if (!isGameRequest)
            {
                await next();
                return;
            }

            var sw = Stopwatch.StartNew();
            bool failed = false;
            SemaphoreSlim? saveLock = null;
            try
            {
                var body = await ReadAllAsync(ctx.Request.Body);
                if (body == null)
                {
                    ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
                await InstallBufferedBodyAsync(ctx, body);
                ExtractIdentity(ctx, body, path);

                var gdkey = RequestInfo.Gdkey(ctx);
                var udkey = RequestInfo.Udkey(ctx);
                var state = ServerStateStore.Current;

                if (state.MaintenanceMode)
                {
                    await WriteDialog(ctx, state.MaintenanceMessage, "Maintenance");
                    return;
                }

                if (ServerStateStore.IsBanned(gdkey, out var ban))
                {
                    Metrics.Incr("rejected_banned");
                    await WriteDialog(ctx, "This account has been banned.\n" + ban!.Reason, "Banned");
                    return;
                }

                // only saves that exist get checked / locked / counted: made-up ids would otherwise
                // grow these maps without limit (the account lookup is cached for real players)
                bool exists = !string.IsNullOrEmpty(gdkey) && await UserDataManager.Logic.UserDataManager.GetAccountFromGdkeyAsync(gdkey) != null;

                if (exists && !await CheckOwnershipAsync(gdkey!, udkey, path))
                {
                    Metrics.Incr("rejected_ownership");
                    await WriteDialog(ctx, "Authentication error.\nThis save does not belong to this device.", "Error");
                    return;
                }

                if (exists && !OperationLimiter.Allow(gdkey!, path))
                {
                    await WriteDialog(ctx, "You're going a bit fast.\nPlease wait a moment.", "Busy");
                    return;
                }

                if (exists)
                {
                    Metrics.SeenPlayer(gdkey!);
                    var ip = Ip(ctx);
                    if (ip != null) _lastSeen[gdkey!] = (ip, DateTime.UtcNow);
                    saveLock = _saveLocks.GetOrAdd(gdkey!, _ => new SemaphoreSlim(1, 1));
                    if (!await saveLock.WaitAsync(LOCK_TIMEOUT))
                    {
                        saveLock = null;
                        await WriteDialog(ctx, "The server is busy.\nPlease try again.", "Busy");
                        return;
                    }
                }

                await next();
            }
            catch (UserDataManager.Logic.UserDataManager.ServerFullException)
            {
                failed = true;
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = 503;
                    await WriteDialog(ctx, "The server is full.\nPlease try again later.", "Busy");
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Log.Error("request", $"{path} failed: {ex}");
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.Clear();
                    await WriteDialog(ctx, "A server error occurred.\nPlease try again.", DataManager.Logic.DataManager.ServerName ?? "Error");
                }
            }
            finally
            {
                saveLock?.Release();
                if (ctx.Response.StatusCode >= 500) failed = true;
                Metrics.Record(path.TrimEnd('/'), sw.ElapsedMilliseconds, failed);
            }
        }

        private static async Task<byte[]?> ReadAllAsync(Stream body)
        {
            using var ms = new MemoryStream();
            var buf = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(buf)) > 0)
            {
                ms.Write(buf, 0, read);
                if (ms.Length > MAX_BODY_BYTES) return null;
            }
            return ms.ToArray();
        }

        private static async Task InstallBufferedBodyAsync(HttpContext ctx, byte[] body)
        {
            ctx.Request.Body = new MemoryStream(body, writable: false);
            var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
            await pipe.Writer.WriteAsync(body);
            await pipe.Writer.CompleteAsync();
            ctx.Features.Set<IRequestBodyPipeFeature>(new BufferedBodyPipeFeature(pipe.Reader));
        }

        private static void ExtractIdentity(HttpContext ctx, byte[] body, string path)
        {
            try
            {
                var decrypted = NHNCrypt.Logic.NHNCrypt.DecryptRequest(Encoding.UTF8.GetString(body).Trim());
                if (decrypted == null) return;
                if (LogRequests) Log.Info("request", $"{path} <- {decrypted}");
                var jo = JObject.Parse(decrypted);
                ctx.Items[RequestInfo.JSON] = jo;
                ctx.Items[RequestInfo.GDKEY] = GetString(jo, "level5UserId") ?? GetString(jo, "gdkeyValue") ?? GetString(jo, "gdkey");
                ctx.Items[RequestInfo.UDKEY] = GetString(jo, "deviceId") ?? GetString(jo, "udkey");
            }
            catch
            {
                // Not every .nhn body is an encrypted JSON object; the handler decides what to do.
            }
        }

        private static string? GetString(JObject jo, string key)
        {
            var tok = jo.GetValue(key, StringComparison.OrdinalIgnoreCase);
            if (tok == null || tok.Type == JTokenType.Null) return null;
            var s = tok.ToString();
            return string.IsNullOrWhiteSpace(s) || s == "0" ? null : s;
        }

        private static async Task<bool> CheckOwnershipAsync(string gdkey, string? udkey, string path)
        {
            var mode = DataManager.Logic.DataManager.AccountOwnershipMode;
            if (mode == "off") return true;
            // "enforce": a request without a device, or from a device the server doesn't know, can't
            // prove it owns the save (otherwise any made-up deviceId would pass)
            if (string.IsNullOrEmpty(udkey)) return mode != "enforce";
            var cacheKey = udkey + "|" + gdkey;
            if (_verifiedOwnership.ContainsKey(cacheKey)) return true;

            bool owns;
            try
            {
                // GetDeviceGdkeys is empty for an unknown device
                var gdkeys = await UserDataManager.Logic.UserDataManager.GetDeviceGdkeysPublicAsync(udkey);
                owns = gdkeys.Contains(gdkey);
            }
            catch
            {
                return true;
            }

            if (owns)
            {
                _verifiedOwnership[cacheKey] = 0;
                return true;
            }

            if (_reportedOwnership.TryAdd(cacheKey, 0))
                Log.Warn("security", $"device {udkey} sent {path} for a save it does not own ({gdkey})");
            return mode != "enforce";
        }

        public static async Task WriteDialog(HttpContext ctx, string message, string title)
        {
            ctx.Response.ContentType = "application/json";
            var msg = new MsgBoxResponse(message, title);
            await ctx.Response.WriteAsync(NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(msg)));
        }
    }
}
