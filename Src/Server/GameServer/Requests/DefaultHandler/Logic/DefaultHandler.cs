using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.Infrastructure;
using System.Collections.Concurrent;
using System.Text;

namespace Puniemu.Src.Server.GameServer.Requests.DefaultHandler.Logic
{
    // Requests nothing else handles.
    //
    // The 4.177.0 client knows 164 endpoints (found in libSGF.so); Puniemu implements the main ones.
    //  * Fire-and-forget calls (logs, "already read" flags, store review...) get a plain success
    //    answer, so the game shows no error for them.
    //  * Every other unknown request is recorded (decrypted, last few per endpoint) and shown in the
    //    admin panel (Logs -> Unimplemented requests), so the requests a real client sends can be
    //    used to implement them. The player gets the "Unimplemented request" message as before.
    public class DefaultHandler
    {
        // Endpoints whose answer the client only checks for success.
        private static readonly HashSet<string> ACKNOWLEDGE = new(StringComparer.OrdinalIgnoreCase)
        {
            "writeAppLog.nhn", "storeReview.nhn", "deferAdsPlay.nhn",
            "updateTrainingTutorialFlg.nhn", "updateTeamResultReadFlg.nhn", "updateNekomataInkFlg.nhn",
            "updateNekomataClearFlg.nhn", "updateMyfave.nhn", "updateCampaignInfo.nhn", "purchaseFailure.nhn"
        };

        public class Recorded
        {
            public string Endpoint { get; set; } = "";
            public int Count { get; set; }
            public bool Acknowledged { get; set; }
            public DateTime Last { get; set; }
            public List<string> Samples { get; set; } = new();
        }

        private static readonly ConcurrentDictionary<string, Recorded> _recorded = new(StringComparer.OrdinalIgnoreCase);
        private const int SAMPLES = 3;
        private const int SAMPLE_CHARS = 8000;

        public static IEnumerable<Recorded> RecordedRequests => _recorded.Values.OrderByDescending(r => r.Last);

        private static void Record(string endpoint, string? json, bool acknowledged)
        {
            if (_recorded.Count > 500 && !_recorded.ContainsKey(endpoint)) return; // junk paths
            var r = _recorded.GetOrAdd(endpoint, e => new Recorded { Endpoint = e });
            lock (r)
            {
                r.Count++;
                r.Acknowledged = acknowledged;
                r.Last = DateTime.Now;
                if (json != null)
                {
                    r.Samples.Insert(0, json.Length > SAMPLE_CHARS ? json[..SAMPLE_CHARS] + "..." : json);
                    if (r.Samples.Count > SAMPLES) r.Samples.RemoveAt(r.Samples.Count - 1);
                }
            }
        }

        public static async Task HandleAsync(HttpContext ctx)
        {
            try
            {
                var path = ctx.Request.Path.Value ?? "";
                var endpoint = path.Trim('/').Split('/').LastOrDefault() ?? path;
                string? json = null;
                if (HttpMethods.IsPost(ctx.Request.Method))
                {
                    try
                    {
                        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
                        var body = await reader.ReadToEndAsync();
                        if (body.Length > 0 && body.Length < 2_000_000) json = NHNCrypt.Logic.NHNCrypt.DecryptRequest(body.Trim());
                    }
                    catch
                    {
                        // not an encrypted game request
                    }
                }
                bool ack = endpoint.EndsWith(".nhn", StringComparison.OrdinalIgnoreCase) && ACKNOWLEDGE.Contains(endpoint);
                if (endpoint.EndsWith(".nhn", StringComparison.OrdinalIgnoreCase))
                {
                    Record(endpoint, json, ack);
                    if (!ack) Log.Warn("request", $"Unimplemented request {endpoint} (recorded for the admin panel)");
                }

                ctx.Response.Headers.ContentType = "application/json";
                if (ack)
                {
                    var res = await new CommonResponse().ToDictionary();
                    // (ToDictionary only covers fields; these are properties)
                    res["resultCode"] = 0;
                    res["resultType"] = 0;
                    res["nextScreenType"] = 0;
                    var gdkey = TryGdkey(json);
                    if (gdkey != null)
                    {
                        var userData = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<YwpUserData>(gdkey, "ywp_user_data");
                        if (userData != null) res["ywp_user_data"] = userData;
                    }
                    await ctx.Response.WriteAsync(NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(res)));
                    return;
                }

                var msgStruct = new MsgBoxResponse($"Unimplemented request:\n{path}", DataManager.Logic.DataManager.ServerName!);
                await ctx.Response.WriteAsync(NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(msgStruct)));
            }
            catch
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsync("Internal server error");
            }
        }

        private static string? TryGdkey(string? json)
        {
            if (json == null) return null;
            try
            {
                var o = JObject.Parse(json);
                foreach (var k in new[] { "level5UserID", "level5UserId", "gdkeyValue" })
                    if (o.Value<string>(k) is { Length: > 0 and < 128 } v && v != "0") return v;
            }
            catch { }
            return null;
        }
    }
}
