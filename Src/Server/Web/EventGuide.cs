using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Events;
using Puniemu.Src.Server.Infrastructure;
using System.Net;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Web
{
    // In-game Event Guide (/web/guide.nhn): every event of the live game with its featured
    // characters, the fan translation of its story, and - for events this server has data for - the
    // bonus Yo-kai, missions and maps taken from the master data, plus a "play this event" button.
    // Character pictures and stories are linked from the community Drive archive, not copied.
    public static class EventGuide
    {
        private static readonly Regex TAGS = new("</?[A-Za-z]+>", RegexOptions.Compiled);

        private static readonly Regex ART_ID = new(@"^art_(\d+)$", RegexOptions.Compiled);

        private static string Clean(string s) => TAGS.Replace(s, "").Replace("\r", "").Trim();

        // Some pictures are named after the Yo-kai id ("art_9002547"): show the Yo-kai's name instead.
        private static List<object> Characters(List<HistoryCharacter>? chars, Dictionary<string, string> yokai, out Dictionary<string, string> imageByYoukai)
        {
            imageByYoukai = new();
            var list = new List<object>();
            foreach (var c in chars ?? new())
            {
                var name = c.Name;
                var m = ART_ID.Match(name);
                if (m.Success)
                {
                    imageByYoukai.TryAdd(m.Groups[1].Value, c.Image);
                    name = yokai.TryGetValue(m.Groups[1].Value, out var n) ? n : "Yo-kai #" + m.Groups[1].Value;
                }
                list.Add(new { name, image = c.Image });
            }
            return list;
        }

        private static Dictionary<string, string> YoukaiNames()
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            var names = new Dictionary<string, string>();
            if (cache.TryGetValue("ywp_mst_youkai", out var raw))
                foreach (var r in EventManager.ReadPipeRows(raw, out _))
                    if (r.Length > 1) names.TryAdd(r[0], r[1]);
            return names;
        }

        private static List<string[]> Rows(string table)
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            return cache.TryGetValue(table, out var raw) ? EventManager.ReadPipeRows(raw, out _) : new List<string[]>();
        }

        public static async Task HandleListAsync(HttpContext ctx)
        {
            var catalog = EventManager.Catalog.ToDictionary(x => x.EventId);
            var h = EventManager.History;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                source = h.Source,
                otherStories = h.OtherStories,
                allowPlayerChoice = ServerStateStore.Current.Events.AllowPlayerChoice,
                series = h.Series.Select(s => new
                {
                    category = s.Category,
                    series = s.Series,
                    cover = s.Events.SelectMany(e => e.Characters ?? new()).Select(c => c.Image).FirstOrDefault(),
                    events = s.Events.Select(e => new
                    {
                        name = e.Name,
                        cover = e.Characters?.FirstOrDefault()?.Image,
                        characters = e.Characters?.Count ?? 0,
                        // character names for the search box
                        names = string.Join("|", (e.Characters ?? new()).Select(c => c.Name).Where(n => !ART_ID.IsMatch(n))),
                        story = e.Story != null,
                        available = e.EventIds.Any(id => catalog.TryGetValue(id, out var c) && c.Selectable)
                    })
                })
            }));
        }

        public static async Task HandleEventAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            var h = EventManager.History;
            if (!int.TryParse(ctx.Request.Query["s"], out var si) || !int.TryParse(ctx.Request.Query["e"], out var ei) ||
                si < 0 || si >= h.Series.Count || ei < 0 || ei >= h.Series[si].Events.Count)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.WriteAsync("{\"error\":\"unknown event\"}");
                return;
            }
            var series = h.Series[si];
            var ev = series.Events[ei];
            var catalog = EventManager.Catalog.ToDictionary(x => x.EventId);
            var linked = ev.EventIds.Where(catalog.ContainsKey).Select(id => catalog[id]).ToList();
            var ids = linked.Select(x => x.EventId.ToString()).ToHashSet();
            var yokai = YoukaiNames();

            // Bonus Yo-kai: ywp_mst_event_youkai_assist_disp = youkaiId | eventId | ? | start | end | text
            var characters = Characters(ev.Characters, yokai, out var imageByYoukai);
            var bonuses = Rows("ywp_mst_event_youkai_assist_disp")
                .Where(r => r.Length > 5 && ids.Contains(r[1]))
                .Select(r => new
                {
                    youkai = yokai.TryGetValue(r[0], out var n) ? n : "Yo-kai #" + r[0],
                    image = imageByYoukai.TryGetValue(r[0], out var im) ? im : null,
                    effect = Clean(r[5])
                })
                .ToList();
            // Newer data keeps the bonus Yo-kai in ywp_mst_event_youkai_assist (no description text)
            var cacheAssist = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            if (cacheAssist.TryGetValue("ywp_mst_event_youkai_assist", out var assistRaw))
            {
                var listed = bonuses.Select(b => b.youkai).ToHashSet();
                foreach (var row in EventManager.ReadJsonRows(assistRaw, out _))
                {
                    var eid = (row.Value<long?>("eventId") ?? 0).ToString();
                    var yid = (row.Value<long?>("youkaiId") ?? 0).ToString();
                    if (!ids.Contains(eid) || yid == "0") continue;
                    var name = yokai.TryGetValue(yid, out var n) ? n : "Yo-kai #" + yid;
                    if (!listed.Add(name)) continue;
                    bonuses.Add(new { youkai = name, image = imageByYoukai.TryGetValue(yid, out var im) ? im : null, effect = "Event bonus" });
                    if (bonuses.Count >= 60) break;
                }
            }
            // Missions: ywp_mst_event_quest_sub = eventId | ? | no | ? | ? | text | ...
            var missions = Rows("ywp_mst_event_quest_sub")
                .Where(r => r.Length > 5 && ids.Contains(r[0]) && r[5].Length > 0)
                .Select(r => Clean(r[5])).Distinct().Take(60).ToList();
            var mapNames = new List<string>();
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            var mapIds = linked.SelectMany(x => x.MapIds).ToHashSet();
            if (mapIds.Count > 0 && cache.TryGetValue("ywp_mst_map", out var mapRaw))
                foreach (var m in EventManager.ReadJsonRows(mapRaw, out _))
                    if (mapIds.Contains(m.Value<long?>("mapId") ?? 0)) mapNames.Add(m.Value<string>("mapName") ?? "");

            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                series = series.Series,
                category = series.Category,
                name = ev.Name,
                characters,
                story = ev.Story,
                driveFolder = ev.DriveFolder,
                notes = ev.Notes,
                available = linked.Any(x => x.Selectable),
                eventIds = linked.Select(x => x.EventId),
                period = ev.Dates is { Count: > 0 } ? string.Join(" / ", ev.Dates)
                       : string.Join(" / ", linked.Select(x => x.Period).Where(p => p.Length > 0).Distinct()),
                maps = mapNames.Where(n => n.Length > 0).Distinct().ToList(),
                bonuses,
                missions,
                siblings = series.Events.Select(e => e.Name).ToList()
            }));
        }

        // Adds the event to the player's own selection (switching them to "choose my own" if needed).
        public static async Task HandlePlayAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            if (!ServerStateStore.Current.Events.AllowPlayerChoice)
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsync("{\"error\":\"The server owner chooses the events on this server.\"}");
                return;
            }
            JObject body;
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                body = JObject.Parse(await reader.ReadToEndAsync());
            }
            catch
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("{\"error\":\"bad request\"}");
                return;
            }
            var userId = body.Value<string>("userId");
            var gdkey = string.IsNullOrWhiteSpace(userId) || userId!.Length > 64 ? null : await UserDataManager.Logic.UserDataManager.GetGdkeyFromUserId(userId);
            if (!string.IsNullOrEmpty(gdkey) && !Puniemu.Src.Server.Infrastructure.GameRequestMiddleware.IsFromPlayerDevice(ctx, gdkey))
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsync("{\"error\":\"Open this page from the in-game Help menu on your own device.\"}");
                return;
            }
            var h = EventManager.History;
            int si = body.Value<int?>("s") ?? -1, ei = body.Value<int?>("e") ?? -1;
            if (string.IsNullOrEmpty(gdkey) || si < 0 || si >= h.Series.Count || ei < 0 || ei >= h.Series[si].Events.Count)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.WriteAsync("{\"error\":\"Open the guide from the in-game Help menu.\"}");
                return;
            }
            var selectable = EventManager.Catalog.Where(x => x.Selectable).Select(x => x.EventId).ToHashSet();
            var add = h.Series[si].Events[ei].EventIds.Where(selectable.Contains).ToList();
            if (add.Count == 0)
            {
                ctx.Response.StatusCode = 409;
                await ctx.Response.WriteAsync("{\"error\":\"This server has no data for this event.\"}");
                return;
            }
            var choice = await EventManager.GetPlayerChoiceAsync(gdkey) ?? new PlayerEventChoice();
            if (choice.Mode != "custom")
            {
                var global = EventManager.GlobalSelection();
                choice = new PlayerEventChoice { Mode = "custom", Ids = global.Enabled ? global.ActiveIds.ToList() : new List<long>() };
            }
            bool remove = body.Value<bool?>("remove") == true;
            foreach (var id in add)
            {
                if (remove) choice.Ids.Remove(id);
                else if (!choice.Ids.Contains(id)) choice.Ids.Add(id);
            }
            await EventManager.SetPlayerChoiceAsync(gdkey, choice);
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new { ok = true, active = choice.Ids }));
        }

        public static async Task HandlePageAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await ctx.Response.WriteAsync(GuidePage.HTML
                .Replace("{{SERVER_NAME}}", WebUtility.HtmlEncode(DataManager.Logic.DataManager.ServerName ?? "Puniemu"))
                .Replace("{{USER_ID}}", Puniemu.Src.Utils.GeneralUtils.GeneralUtils.JsString(ctx.Request.Query["userId"].ToString())));
        }
    }
}
