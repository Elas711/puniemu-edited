using Newtonsoft.Json;
using Puniemu.Src.Server.Infrastructure;
using System.Net;

namespace Puniemu.Src.Server.Web.Wiki
{
    // In-game wiki (/web/wiki.nhn) and its JSON API (/web/api/wiki/*).
    public static class WikiApi
    {
        public static readonly object[] LINKS =
        {
            new { title = "Mt. Wildwood - Puni Puni & Wibble Wobble", url = "https://mtwildwood.net/de/puni/", text = "Guides, Yo-kai lists and event write-ups (German / English)." },
            new { title = "Game8 Puni Puni wiki", url = "https://game8.jp/punipuni", text = "Japanese wiki: events, tier lists, hidden stage conditions, crank-a-kai." },
            new { title = "Game8 - hidden stage conditions", url = "https://game8.jp/punipuni/35982", text = "Unlock conditions of every hidden stage (Japanese)." },
            new { title = "Game8 - all Yo-kai", url = "https://game8.jp/punipuni/26088", text = "Complete Yo-kai list (Japanese)." },
            new { title = "Yo-kai Watch Espana - Puni Puni", url = "https://yo-kai-watch.es/secciones/juegos/punipuni", text = "Puni Puni section and Yo-kai database (Spanish)." },
            new { title = "Yo-kai Watch Wiki (Fandom)", url = "https://yokaiwatch.fandom.com/wiki/Yo-kai_Watch_Wibble_Wobble", text = "English wiki article about Wibble Wobble." },
            new { title = "YWP-Translation", url = "https://github.com/Lit8tech/YWP-Translation", text = "Community English translation of Puni Puni." },
        };

        private static async Task Json(HttpContext ctx, object o)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(o));
        }

        public static void Map(WebApplication app)
        {
            app.MapGet("/web/wiki.nhn", async ctx =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                await ctx.Response.WriteAsync(WikiPage.HTML
                    .Replace("{{SERVER_NAME}}", WebUtility.HtmlEncode(DataManager.Logic.DataManager.ServerName ?? "Puniemu"))
                    .Replace("{{USER_ID}}", Puniemu.Src.Utils.GeneralUtils.GeneralUtils.JsString(ctx.Request.Query["userId"].ToString())));
            });
            app.MapGet("/web/wiki/img/{name}", StaticContent.HandleAssetAsync);

            app.MapGet("/web/api/wiki/index", async ctx =>
            {
                var w = WikiData.Current;
                var st = ServerStateStore.Current;
                await Json(ctx, new
                {
                    server = DataManager.Logic.DataManager.ServerName,
                    game = DataManager.Logic.DataManager.IsWibWob ? "Yo-kai Watch Wibble Wobble" : "Yo-kai Watch Puni Puni",
                    articles = WikiArticles.All.Select(a => new { id = a.Id, icon = a.Icon, title = a.Title }),
                    counts = new { yokai = w.YokaiList.Count, maps = w.Maps.Count, stages = w.StageById.Count, items = w.Items.Count, missions = w.Missions.Count, gachas = Puniemu.Src.Server.Gacha.GachaLineup.Machines.Count },
                    settings = new
                    {
                        motd = st.Motd,
                        ymoney = st.YMoneyMultiplier,
                        exp = st.ExpMultiplier,
                        infiniteSpirits = st.InfiniteSpirits,
                        eventMode = st.Events.Mode,
                        playerEvents = st.Events.AllowPlayerChoice,
                        english = DataManager.Logic.DataManager.Language == "en",
                        gifts = st.Gifts.Count
                    },
                    links = LINKS
                });
            });

            app.MapGet("/web/api/wiki/article/{id}", async ctx =>
            {
                var id = (string?)ctx.Request.RouteValues["id"];
                var a = WikiArticles.All.FirstOrDefault(x => x.Id == id);
                if (a == null) { ctx.Response.StatusCode = 404; return; }
                await Json(ctx, new { id = a.Id, icon = a.Icon, title = a.Title, html = a.Html });
            });

            app.MapGet("/web/api/wiki/yokai", async ctx =>
            {
                await Json(ctx, WikiData.Current.YokaiList.Select(y => new { id = y.Id, name = y.Name, rank = y.Rank, tribe = y.Tribe, img = y.Image }));
            });

            app.MapGet("/web/api/wiki/yokai/{id}", async ctx =>
            {
                var w = WikiData.Current;
                var id = (string?)ctx.Request.RouteValues["id"] ?? "";
                if (!w.YokaiById.TryGetValue(id, out var y)) { ctx.Response.StatusCode = 404; return; }
                var evolvesFrom = w.YokaiList.Where(x => x.EvolvesTo == y.Id).Select(x => new { id = x.Id, name = x.Name, level = x.EvolveLevel }).ToList();
                var where = (w.StagesByYokai.TryGetValue(y.Id, out var sl) ? sl : new List<long>())
                    .Select(sid => w.StageById[sid])
                    .Select(s => new { stageId = s.Id, mapId = s.MapId, map = MapName(w, s.MapId), stage = s.Name.Length > 0 ? s.Name : "Stage " + s.No, boss = s.Boss, hidden = s.Hidden })
                    .ToList();
                await Json(ctx, new
                {
                    id = y.Id, name = y.Name, rank = y.Rank,
                    originalRank = long.TryParse(y.Id, out var nid) ? Puniemu.Src.Server.Balance.BalanceManager.RankOf(nid) : 0,
                    tribe = y.Tribe, food = y.Food, img = y.Image,
                    maxLevel = y.MaxLevel, baseHp = y.BaseHp, maxHp = y.MaxHp, baseAtk = y.BaseAtk, maxAtk = y.MaxAtk,
                    description = y.Description, skillName = y.SkillName, skillText = y.SkillText, bonus = y.Bonus,
                    evolvesTo = y.EvolvesTo.Length > 0 && w.YokaiById.TryGetValue(y.EvolvesTo, out var e) ? new { id = e.Id, name = e.Name, level = y.EvolveLevel } : null,
                    evolvesFrom,
                    where
                });
            });

            app.MapGet("/web/api/wiki/maps", async ctx =>
            {
                var w = WikiData.Current;
                await Json(ctx, w.Maps.Select(m =>
                {
                    var id = m.Value<long?>("mapId") ?? 0;
                    var st = w.StagesByMap.TryGetValue(id, out var l) ? l : new List<WikiData.Stage>();
                    return new
                    {
                        id, name = WikiData.Clean(m.Value<string>("mapName") ?? ""), type = m.Value<int?>("mapType") ?? 0,
                        eventId = m.Value<long?>("eventId") ?? 0, stages = st.Count, hidden = st.Count(x => x.Hidden), bosses = st.Count(x => x.Boss)
                    };
                }).Where(x => x.stages > 0 || x.name.Length > 0));
            });

            app.MapGet("/web/api/wiki/map/{id}", async ctx =>
            {
                var w = WikiData.Current;
                if (!long.TryParse((string?)ctx.Request.RouteValues["id"], out var id)) { ctx.Response.StatusCode = 404; return; }
                var m = w.Maps.FirstOrDefault(x => (x.Value<long?>("mapId") ?? 0) == id);
                var st = w.StagesByMap.TryGetValue(id, out var l) ? l : new List<WikiData.Stage>();
                await Json(ctx, new
                {
                    id,
                    name = m != null ? WikiData.Clean(m.Value<string>("mapName") ?? "") : "Map " + id,
                    description = m != null ? WikiData.Clean(m.Value<string>("description") ?? "") : "",
                    stages = st.Select(s => new
                    {
                        id = s.Id, no = s.No, name = s.Name, boss = s.Boss, hidden = s.Hidden, unlock = s.Unlock, level = s.RecommendLevel, stars = s.Stars,
                        enemies = s.Enemies.Select(e => new { id = e, name = w.YokaiById.TryGetValue(e, out var y) ? y.Name : "#" + e, img = w.YokaiById.TryGetValue(e, out var y2) ? y2.Image : null })
                    })
                });
            });

            app.MapGet("/web/api/wiki/items", async ctx => await Json(ctx, WikiData.Current.Items));
            app.MapGet("/web/api/wiki/missions", async ctx => await Json(ctx, WikiData.Current.Missions));
            app.MapGet("/web/api/wiki/gachas", async ctx => await Json(ctx, WikiData.Current.Gachas));
        }

        private static string MapName(WikiData w, long mapId)
        {
            var m = w.Maps.FirstOrDefault(x => (x.Value<long?>("mapId") ?? 0) == mapId);
            return m != null ? WikiData.Clean(m.Value<string>("mapName") ?? "") : "Map " + mapId;
        }
    }
}
