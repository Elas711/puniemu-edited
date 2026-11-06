using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Events;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.Infrastructure;
using Puniemu.Src.Server.Translation;
using System.Security.Cryptography;
using System.Text;
using UDM = Puniemu.Src.UserDataManager.Logic.UserDataManager;

namespace Puniemu.Src.Server.Admin
{
    // Web admin panel at /admin, JSON API under /admin/api/*. Disabled unless AdminToken is set; every
    // API call must send the token in the X-Admin-Token header.
    public static class AdminPanel
    {
        public static void Map(WebApplication app)
        {
            app.MapGet("/admin", async ctx =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'";
                if (string.IsNullOrEmpty(DataManager.Logic.DataManager.AdminToken))
                {
                    await ctx.Response.WriteAsync("<h1>Admin panel disabled</h1><p>Set <code>AdminToken</code> in appsettings.json and restart the server.</p>");
                    return;
                }
                await ctx.Response.WriteAsync(AdminPage.HTML);
            });

            Api(app, "GET", "/admin/api/overview", async ctx => new
            {
                server = DataManager.Logic.DataManager.ServerName,
                game = DataManager.Logic.DataManager.IsWibWob ? "Wibble Wobble" : "Puni Puni",
                version = DataManager.Logic.DataManager.GameVersion,
                db = await UDM.GetStatsAsync(),
                metrics = Metrics.Snapshot(),
                tables = DataManager.Logic.DataManager.GameDataManager.GamedataCache.Count,
                translation = new
                {
                    language = DataManager.Logic.DataManager.Language,
                    entries = TranslationManager.LoadedEntries,
                    translated = TranslationManager.TranslatedCells,
                    remaining = TranslationManager.RemainingJapaneseCells,
                    romaji = TranslationManager.RomajiCells,
                    untranslatedDistinct = TranslationManager.Untranslated.Count
                },
                events = new { total = EventManager.Catalog.Count, switchable = EventManager.Catalog.Count(x => x.Selectable) },
                balance = new
                {
                    mode = Puniemu.Src.Server.Balance.BalanceManager.Mode,
                    strength = Puniemu.Src.Server.Balance.BalanceManager.Strength,
                    rankMoves = Puniemu.Src.Server.Balance.BalanceManager.RankMoves,
                    factors = Puniemu.Src.Server.Balance.BalanceManager.Factors.OrderBy(k => k.Key).ToDictionary(k => k.Key.ToString(), k => Math.Round(k.Value, 2))
                },
                state = ServerStateStore.Current
            });

            Api(app, "GET", "/admin/api/players", async ctx =>
            {
                var q = ctx.Request.Query["q"].ToString();
                int.TryParse(ctx.Request.Query["limit"], out var limit);
                var list = await UDM.SearchAccountsAsync(q, limit == 0 ? 50 : limit);
                return list.Select(a => new
                {
                    a.Gdkey, a.CharacterId, a.UserId, a.PlayerName, a.LastLoginTime, a.Online,
                    banned = ServerStateStore.IsBanned(a.Gdkey, out _)
                });
            });

            Api(app, "GET", "/admin/api/player/{gdkey}", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var acc = await UDM.GetAccountFromGdkeyAsync(gdkey);
                if (acc == null) throw new KeyNotFoundException("No such save");
                var ud = await UDM.GetYwpUserAsync<YwpUserData>(gdkey, "ywp_user_data");
                var yokai = await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_youkai");
                var items = await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_item");
                ServerStateStore.IsBanned(gdkey, out var ban);
                return new
                {
                    gdkey,
                    friendCode = acc.CharacterId,
                    userId = acc.UserId,
                    udkey = acc.Udkey,
                    lastLogin = acc.LastLoginTime,
                    playerName = ud?.PlayerName,
                    ymoney = ud?.YMoney,
                    spirits = ud?.Hitodama,
                    freeSpirits = ud?.FreeHitodama,
                    currentStage = ud?.CurrentStageID,
                    yokaiCount = string.IsNullOrEmpty(yokai) ? 0 : yokai.Split('*').Length,
                    items = string.IsNullOrEmpty(items) ? new List<object>() :
                        items.Split('*').Select(r => r.Split('|')).Where(r => r.Length >= 2)
                             .Select(r => (object)new { id = r[0], count = r[1] }).ToList(),
                    eventChoice = await EventManager.GetPlayerChoiceAsync(gdkey),
                    ban
                };
            });

            Api(app, "POST", "/admin/api/player/{gdkey}/grant", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var b = await ReadJson(ctx);
                var items = b["items"]?.ToObject<List<ItemGrant>>() ?? new();
                var youkai = b["youkai"]?.ToObject<List<long>>() ?? new();
                var r = await GrantService.GrantAsync(gdkey, b.Value<int?>("ymoney") ?? 0, b.Value<int?>("spirits") ?? 0, items, youkai);
                Log.Info("admin", $"granted to {gdkey}: {b.ToString(Formatting.None)}");
                return r;
            });

            Api(app, "POST", "/admin/api/player/{gdkey}/ban", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var b = await ReadJson(ctx);
                ServerStateStore.Update(s => s.Bans[gdkey] = new BanEntry
                {
                    Reason = b.Value<string>("reason") ?? "",
                    Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                Log.Info("admin", $"banned {gdkey}");
                return new { ok = true };
            });

            Api(app, "POST", "/admin/api/player/{gdkey}/unban", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                ServerStateStore.Update(s => s.Bans.Remove(gdkey));
                Log.Info("admin", $"unbanned {gdkey}");
                await Task.CompletedTask;
                return new { ok = true };
            });

            Api(app, "POST", "/admin/api/player/{gdkey}/events", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var b = await ReadJson(ctx);
                await EventManager.SetPlayerChoiceAsync(gdkey, new PlayerEventChoice
                {
                    Mode = b.Value<string>("mode") == "custom" ? "custom" : "server",
                    Ids = b["ids"]?.ToObject<List<long>>() ?? new()
                });
                return new { ok = true };
            });

            // Full save as JSON (backup / move a save to another account)
            Api(app, "GET", "/admin/api/player/{gdkey}/export", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var acc = await UDM.GetAccountFromGdkeyAsync(gdkey);
                if (acc == null) throw new KeyNotFoundException("No such save");
                ctx.Response.Headers["Content-Disposition"] = $"attachment; filename=save-{acc.CharacterId}.json";
                return new { format = "puniemu-save-1", exported = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), friendCode = acc.CharacterId, tables = acc.YwpUserTables };
            });

            // Replaces the save's tables with an export. The friend code / ids of the target stay the same.
            Api(app, "POST", "/admin/api/player/{gdkey}/import", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var b = await ReadJson(ctx);
                if (b.Value<string>("format") != "puniemu-save-1" || b["tables"] is not JObject tables)
                    throw new InvalidOperationException("Not a Puniemu save export");
                var acc = await UDM.GetAccountFromGdkeyAsync(gdkey);
                if (acc == null) throw new KeyNotFoundException("No such save");
                var dict = tables.ToObject<Dictionary<string, object?>>()!;
                // Keep the target's own identity inside ywp_user_data
                if (acc.YwpUserTables != null && acc.YwpUserTables.TryGetValue("ywp_user_data", out var oldUd) && oldUd != null && dict.TryGetValue("ywp_user_data", out var newUd) && newUd != null)
                {
                    var o = JObject.FromObject(oldUd); var n = JObject.FromObject(newUd);
                    foreach (var k in new[] { "userId", "characterId" }) if (o[k] != null) n[k] = o[k];
                    dict["ywp_user_data"] = n;
                }
                await UDM.SetEntireUserData(gdkey, dict);
                Log.Info("admin", $"imported a save into {gdkey}");
                return new { ok = true, tables = dict.Count };
            });

            Api(app, "POST", "/admin/api/player/{gdkey}/rename", async ctx =>
            {
                var gdkey = (string)ctx.Request.RouteValues["gdkey"]!;
                var b = await ReadJson(ctx);
                var name = Puniemu.Src.Utils.GeneralUtils.GeneralUtils.SanitizePlayerText(b.Value<string>("name"));
                if (name.Length == 0) throw new InvalidOperationException("Empty name");
                var ud = await UDM.GetYwpUserAsync<YwpUserData>(gdkey, "ywp_user_data") ?? throw new KeyNotFoundException("No player data");
                ud.PlayerName = name;
                await UDM.SetYwpUserAsync(gdkey, "ywp_user_data", ud);
                await Puniemu.Src.Server.GameServer.Logic.GenerateFriendData.RefreshYwpUserFriend(gdkey, -1, -1, name, -1, "");
                return new { ok = true, name };
            });

            // Find Yo-kai / item ids by name (for the give fields). Idea from cursedmono's WWR Explorer.
            Api(app, "GET", "/admin/api/lookup", async ctx =>
            {
                await Task.CompletedTask;
                var q = ctx.Request.Query["q"].ToString().Trim();
                var type = ctx.Request.Query["type"].ToString() == "item" ? "ywp_mst_item" : "ywp_mst_youkai";
                if (q.Length == 0) return new List<object>();
                var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
                if (!cache.TryGetValue(type, out var raw)) return new List<object>();
                // name column: 1 in ywp_mst_youkai, 2 in ywp_mst_item
                int nameCol = type == "ywp_mst_item" ? 2 : 1;
                return EventManager.ReadPipeRows(raw, out _)
                    .Where(r => r.Length > nameCol && (r[0] == q || r[nameCol].Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .Take(50).Select(r => (object)new { id = r[0], name = r[nameCol] }).ToList();
            });

            Api(app, "GET", "/admin/api/events", async ctx =>
            {
                await Task.CompletedTask;
                var now = EventManager.GlobalSelection();
                return new { settings = ServerStateStore.Current.Events, runningNow = now.Enabled ? now.ActiveIds.ToList() : null, catalog = EventManager.Catalog };
            });

            Api(app, "POST", "/admin/api/events", async ctx =>
            {
                var b = await ReadJson(ctx);
                ServerStateStore.Update(s =>
                {
                    var e = s.Events;
                    if (b["mode"] != null)
                    {
                        var m = b.Value<string>("mode");
                        e.Mode = m == "custom" || m == "rotation" ? m! : "default";
                    }
                    if (b["rotationDays"] != null) e.RotationDays = Math.Clamp(b.Value<int>("rotationDays"), 1, 365);
                    if (b["rotationCount"] != null) e.RotationCount = Math.Clamp(b.Value<int>("rotationCount"), 1, 100);
                    if (b["activeEventIds"] is JArray ids) e.ActiveEventIds = ids.Select(x => x.Value<long>()).Distinct().ToList();
                    if (b["allowPlayerChoice"] != null) e.AllowPlayerChoice = b.Value<bool>("allowPlayerChoice");
                    if (b["hideInactiveEventMaps"] != null) e.HideInactiveEventMaps = b.Value<bool>("hideInactiveEventMaps");
                    if (b["eventDurationDays"] != null) e.EventDurationDays = Math.Clamp(b.Value<int>("eventDurationDays"), 1, 3650);
                });
                Log.Info("admin", $"event settings changed: mode={ServerStateStore.Current.Events.Mode}, active={string.Join(",", ServerStateStore.Current.Events.ActiveEventIds)}");
                return ServerStateStore.Current.Events;
            });

            Api(app, "GET", "/admin/api/events/history", async ctx =>
            {
                await Task.CompletedTask;
                return EventManager.DescribeHistory();
            });

            Api(app, "GET", "/admin/api/events/history/suggestions", async ctx =>
            {
                await Task.CompletedTask;
                return EventManager.SuggestHistoryLinks(3).Select(x => new { series = x.series, name = x.name, eventId = x.eventId, overlap = x.overlap });
            });

            Api(app, "POST", "/admin/api/events/history/autolink", async ctx =>
            {
                await Task.CompletedTask;
                var n = EventManager.AutoLinkHistory(10);
                Log.Info("admin", $"auto-linked {n} history entries");
                return new { linked = n };
            });

            Api(app, "POST", "/admin/api/events/history", async ctx =>
            {
                var b = await ReadJson(ctx);
                EventManager.LinkHistory(b.Value<string>("series") ?? "", b.Value<string>("name") ?? "",
                    b["eventIds"]?.ToObject<List<long>>() ?? new());
                return new { ok = true };
            });

            Api(app, "POST", "/admin/api/events/reload", async ctx =>
            {
                await Task.CompletedTask;
                EventManager.Reload();
                return new { total = EventManager.Catalog.Count };
            });

            Api(app, "POST", "/admin/api/settings", async ctx =>
            {
                var b = await ReadJson(ctx);
                ServerStateStore.Update(s =>
                {
                    if (b["maintenanceMode"] != null) s.MaintenanceMode = b.Value<bool>("maintenanceMode");
                    if (b["maintenanceMessage"] != null) s.MaintenanceMessage = b.Value<string>("maintenanceMessage") ?? "";
                    if (b["motd"] != null) s.Motd = b.Value<string>("motd") ?? "";
                    if (b["ymoneyMultiplier"] != null) s.YMoneyMultiplier = Math.Clamp(b.Value<double>("ymoneyMultiplier"), 0, 100);
                    if (b["expMultiplier"] != null) s.ExpMultiplier = Math.Clamp(b.Value<double>("expMultiplier"), 0, 100);
                    if (b["infiniteSpirits"] != null) s.InfiniteSpirits = b.Value<bool>("infiniteSpirits");
                    if (b["gacha"] is JObject g)
                    {
                        var mode = g.Value<string>("coinShop");
                        if (mode is "off" or "free" or "ymoney") s.Gacha.CoinShop = mode;
                        if (g["freeCoinsPerDay"] != null) s.Gacha.FreeCoinsPerDay = Math.Clamp(g.Value<int>("freeCoinsPerDay"), 0, 1000);
                        if (g["coinPriceYMoney"] != null) s.Gacha.CoinPriceYMoney = Math.Clamp(g.Value<int>("coinPriceYMoney"), 0, 100000000);
                        if (g["allowMix"] != null) s.Gacha.AllowMix = g.Value<bool>("allowMix");
                        if (g["serverMix"] is JArray mix)
                            s.Gacha.ServerMix = mix.Select(x => x.Value<int?>() ?? 0).Where(x => x > 0).Distinct().Take(Puniemu.Src.Server.Gacha.GachaChoice.MAX_MIX).ToList();
                    }
                });
                Log.Info("admin", "settings changed");
                return ServerStateStore.Current;
            });

            Api(app, "POST", "/admin/api/gifts", async ctx =>
            {
                var b = await ReadJson(ctx);
                var gift = new GiftEntry
                {
                    Message = b.Value<string>("message") ?? "",
                    YMoney = b.Value<int?>("ymoney") ?? 0,
                    Spirits = b.Value<int?>("spirits") ?? 0,
                    Items = b["items"]?.ToObject<List<ItemGrant>>() ?? new(),
                    Youkai = b["youkai"]?.ToObject<List<long>>() ?? new()
                };
                ServerStateStore.Update(s => s.Gifts.Add(gift));
                Log.Info("admin", $"server-wide gift created: {JsonConvert.SerializeObject(gift)}");
                return gift;
            });

            Api(app, "POST", "/admin/api/gifts/{id}/delete", async ctx =>
            {
                var id = (string)ctx.Request.RouteValues["id"]!;
                ServerStateStore.Update(s => s.Gifts.RemoveAll(g => g.Id == id));
                await Task.CompletedTask;
                return new { ok = true };
            });

            Api(app, "POST", "/admin/api/flush", async ctx =>
            {
                await UDM.FlushNowAsync();
                return new { ok = true };
            });

            // Every text that is still Japanese, as a dictionary file to translate (see Translations/README.md)
            app.MapGet("/admin/api/translation/untranslated", async ctx =>
            {
                if (!Authorized(ctx)) { ctx.Response.StatusCode = 401; return; }
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Headers["Content-Disposition"] = "attachment; filename=untranslated.json";
                await ctx.Response.WriteAsync(TranslationManager.ExportUntranslatedJson());
            });

            // Requests the server does not implement, as a real client sent them (see DefaultHandler)
            Api(app, "GET", "/admin/api/unimplemented", async ctx =>
            {
                await Task.CompletedTask;
                return Puniemu.Src.Server.GameServer.Requests.DefaultHandler.Logic.DefaultHandler.RecordedRequests.Select(r => new
                {
                    endpoint = r.Endpoint, count = r.Count, acknowledged = r.Acknowledged, last = r.Last, samples = r.Samples
                });
            });

            Api(app, "GET", "/admin/api/logs", async ctx =>
            {
                await Task.CompletedTask;
                return Log.Recent();
            });
        }

        private static bool Authorized(HttpContext ctx)
        {
            var expected = DataManager.Logic.DataManager.AdminToken;
            if (string.IsNullOrEmpty(expected)) return false;
            var got = ctx.Request.Headers["X-Admin-Token"].ToString();
            var a = Encoding.UTF8.GetBytes(got);
            var b = Encoding.UTF8.GetBytes(expected);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static void Api(WebApplication app, string method, string pattern, Func<HttpContext, Task<object>> handler)
        {
            app.MapMethods(pattern, new[] { method }, async ctx =>
            {
                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                if (!Authorized(ctx))
                {
                    Metrics.Incr("admin_auth_failed");
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.WriteAsync("{\"error\":\"unauthorized\"}");
                    return;
                }
                try
                {
                    var result = await handler(ctx);
                    await ctx.Response.WriteAsync(JsonConvert.SerializeObject(result));
                }
                catch (KeyNotFoundException ex)
                {
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new { error = ex.Message }));
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new { error = ex.Message }));
                }
            });
        }

        private static async Task<JObject> ReadJson(HttpContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var text = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(text)) return new JObject();
            return JObject.Parse(text);
        }
    }
}
