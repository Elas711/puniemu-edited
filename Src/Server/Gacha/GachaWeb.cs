using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Logic;
using Puniemu.Src.Server.GameServer.Requests.ExecuteGacha.Logic;
using Puniemu.Src.Server.Infrastructure;
using Puniemu.Src.Server.Web.Wiki;
using Puniemu.Src.TableParser.Logic;
using Puniemu.Src.TableParser.DataClasses;

namespace Puniemu.Src.Server.Gacha
{
    // JSON API of the in-game Crank-a-kai page (wiki -> Crank-a-kai):
    //   GET  /web/api/gacha/machines?userId=   every machine, the player's mix and coins left today
    //   GET  /web/api/gacha/machine/{id}       a machine's lineup and odds
    //   POST /web/api/gacha/coin   {userId,id} a coin for a machine (free daily coins or Y-Money)
    //   POST /web/api/gacha/mix    {userId,ids} machines the player's Y-Money crank-a-kai mixes
    public static class GachaWeb
    {
        private static readonly SemaphoreSlim _coinLock = new(1, 1);

        private static async Task Json(HttpContext ctx, object o, int status = 200)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(o));
        }

        private static async Task<string?> Gdkey(string? userId) =>
            string.IsNullOrWhiteSpace(userId) || userId.Length > 64 ? null : await UserDataManager.Logic.UserDataManager.GetGdkeyFromUserId(userId);

        private static async Task<JObject?> Body(HttpContext ctx)
        {
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var text = await reader.ReadToEndAsync();
                return text.Length > 4096 ? null : JObject.Parse(text);
            }
            catch { return null; }
        }

        private static string Today() => DateTime.Now.ToString("yyyyMMdd");

        public static void Map(WebApplication app)
        {
            app.MapGet("/web/api/gacha/machines", async ctx =>
            {
                GachaPoolManager.EnsureLoaded();
                var s = ServerStateStore.Current.Gacha;
                var gdkey = await Gdkey(ctx.Request.Query["userId"]);
                var choice = gdkey == null ? new PlayerGachaChoice() : await GachaChoice.GetAsync(gdkey);
                int left = s.CoinShop == "free" ? Math.Max(0, s.FreeCoinsPerDay - (choice.CoinDay == Today() ? choice.CoinsToday : 0)) : 0;
                var list = GachaLineup.Machines.Values.OrderByDescending(m => m.Start).Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    coin = m.HasCoin ? m.CoinName : "",
                    price = m.PriceType == 1 ? m.Price + " Y-Money" : m.Price + " x " + (m.CoinName.Length > 0 ? m.CoinName : "item " + m.CoinItemId),
                    pulls = m.Pulls,
                    source = m.Source,
                    start = m.Start.Length >= 10 ? m.Start[..10] : m.Start,
                    featured = m.Featured.Count,
                    yokai = GachaPoolManager.TryGetPool(m.Id, out var p) ? p.Yokais?.Values.Sum(l => l.Count) ?? 0 : 0
                });
                await Json(ctx, new
                {
                    signedIn = gdkey != null,
                    settings = new { coinShop = s.CoinShop, freeCoinsPerDay = s.FreeCoinsPerDay, coinPriceYMoney = s.CoinPriceYMoney, allowMix = s.AllowMix },
                    mix = GachaChoice.MAX_MIX > 0 ? (s.AllowMix ? choice.Mix : s.ServerMix) : new List<int>(),
                    coinsLeft = left,
                    machines = list
                });
            });

            app.MapGet("/web/api/gacha/machine/{id:int}", async ctx =>
            {
                var id = int.Parse((string)ctx.Request.RouteValues["id"]!);
                if (!GachaLineup.Machines.TryGetValue(id, out var m) || !GachaPoolManager.TryGetPool(id, out var pool))
                {
                    await Json(ctx, new { error = "unknown machine" }, 404);
                    return;
                }
                var w = WikiData.Current;
                var total = pool.Weights.Values.Sum();
                var featured = m.Featured.ToHashSet();
                var tiers = (pool.Yokais ?? new()).OrderByDescending(kv => int.TryParse(kv.Key, out var n) ? n : 0).Select(kv =>
                {
                    int.TryParse(kv.Key, out var rank);
                    return new
                    {
                        rank = rank > 0 && rank < WikiData.RANKS.Length ? WikiData.RANKS[rank] : kv.Key,
                        chance = total > 0 && pool.Weights.TryGetValue(kv.Key, out var wt) ? Math.Round(wt / total * 100, 2) : (double?)null,
                        yokai = kv.Value.OrderByDescending(y => featured.Contains(y)).Select(y => new
                        {
                            id = y.ToString(),
                            name = w.YokaiById.TryGetValue(y.ToString(), out var yk) ? yk.Name : "#" + y,
                            image = w.YokaiById.TryGetValue(y.ToString(), out var y2) ? y2.Image : null,
                            featured = featured.Contains(y)
                        })
                    };
                });
                await Json(ctx, new { id = m.Id, name = m.Name, source = m.Source, pulls = m.Pulls, coin = m.HasCoin ? m.CoinName : "", tiers });
            });

            app.MapPost("/web/api/gacha/coin", async ctx =>
            {
                var b = await Body(ctx);
                var gdkey = await Gdkey(b?.Value<string>("userId"));
                var s = ServerStateStore.Current.Gacha;
                if (b == null || gdkey == null) { await Json(ctx, new { error = "Open this page from the in-game Help menu." }, 404); return; }
                if (s.CoinShop != "free" && s.CoinShop != "ymoney") { await Json(ctx, new { error = "Coins are not handed out on this server." }, 403); return; }
                var id = b.Value<int?>("id") ?? 0;
                if (!GachaLineup.Machines.TryGetValue(id, out var m) || !m.HasCoin)
                { await Json(ctx, new { error = "This machine does not use a coin." }, 400); return; }

                await _coinLock.WaitAsync();
                try
                {
                    var choice = await GachaChoice.GetAsync(gdkey);
                    if (choice.CoinDay != Today()) { choice.CoinDay = Today(); choice.CoinsToday = 0; }
                    var userData = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<YwpUserData>(gdkey, "ywp_user_data");
                    if (userData == null) { await Json(ctx, new { error = "No save found." }, 404); return; }
                    if (s.CoinShop == "free")
                    {
                        if (choice.CoinsToday >= s.FreeCoinsPerDay) { await Json(ctx, new { error = $"You already got your {s.FreeCoinsPerDay} free coins today. Come back tomorrow!" }, 409); return; }
                        choice.CoinsToday++;
                    }
                    else
                    {
                        if (userData.YMoney < s.CoinPriceYMoney) { await Json(ctx, new { error = $"A coin costs {s.CoinPriceYMoney} Y-Money." }, 409); return; }
                        userData.YMoney -= s.CoinPriceYMoney;
                        await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_data", userData);
                    }
                    var items = new TableParser<YwpUserItem>(await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(gdkey, "ywp_user_item") ?? "");
                    ItemManager.AddItem(items, m.CoinItemId, m.Price > 0 ? m.Price : 1);
                    await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_item", items.ToString());
                    await GachaChoice.SetAsync(gdkey, choice);
                    Log.Info("gacha", $"{gdkey} got a coin for crank-a-kai {id} ({s.CoinShop})");
                    await Json(ctx, new { ok = true, coin = m.CoinName, coinsLeft = s.CoinShop == "free" ? s.FreeCoinsPerDay - choice.CoinsToday : 0, ymoney = userData.YMoney });
                }
                finally { _coinLock.Release(); }
            });

            app.MapPost("/web/api/gacha/mix", async ctx =>
            {
                var b = await Body(ctx);
                var gdkey = await Gdkey(b?.Value<string>("userId"));
                if (b == null || gdkey == null) { await Json(ctx, new { error = "Open this page from the in-game Help menu." }, 404); return; }
                if (!ServerStateStore.Current.Gacha.AllowMix) { await Json(ctx, new { error = "The server owner chooses the crank-a-kai on this server." }, 403); return; }
                var ids = (b["ids"] as JArray)?.Select(x => x.Value<int?>() ?? 0).Where(GachaLineup.Machines.ContainsKey).Distinct().Take(GachaChoice.MAX_MIX).ToList() ?? new();
                var choice = await GachaChoice.GetAsync(gdkey);
                choice.Mix = ids;
                await GachaChoice.SetAsync(gdkey, choice);
                await Json(ctx, new { ok = true, mix = ids });
            });
        }
    }
}
