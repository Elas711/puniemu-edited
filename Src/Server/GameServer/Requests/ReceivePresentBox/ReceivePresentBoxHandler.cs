using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Admin;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.Infrastructure;
using Puniemu.Src.Utils.GeneralUtils;
using System.Text;

namespace Puniemu.Src.Server.GameServer.Requests.ReceivePresentBox
{
    // receivePresentBox.nhn: claims presents from the present box.
    // Request (from the 2.2.6 client, see docs/PROTOCOL.md): "seqs" = the presents to claim; an empty
    // list claims everything that can be received at once (canReceiveAll).
    // The answer carries the updated present box and every table a reward can change.
    public static class ReceivePresentBoxHandler
    {
        private static IEnumerable<long> Seqs(JToken? tok)
        {
            if (tok is JArray arr) return arr.Select(x => long.TryParse(x.ToString(), out var v) ? v : -1).Where(v => v >= 0);
            var s = tok?.ToString() ?? "";
            return s.Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => long.TryParse(x, out var v) ? v : -1).Where(v => v >= 0);
        }

        // Presents made before the type was stored: guess it from the id.
        private static RewardType TypeOf(PresentBoxList p)
        {
            if (Enum.IsDefined(typeof(RewardType), p.DistItemType) && p.DistItemType != 0) return (RewardType)p.DistItemType;
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            string id = p.DistItemId.ToString();
            if (p.DistItemId == 0) return RewardType.YMoney;
            if (cache.TryGetValue("ywp_mst_youkai", out var y) && y.Contains("*" + id + "|")) return RewardType.Yokai;
            return RewardType.Item;
        }

        public static async Task HandleAsync(HttpContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var json = NHNCrypt.Logic.NHNCrypt.DecryptRequest((await reader.ReadToEndAsync()).Trim());
            var req = JObject.Parse(json!);
            var gdkey = req.Value<string>("level5UserID") ?? req.Value<string>("level5UserId") ?? "";
            ctx.Response.ContentType = "application/json";

            var box = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<PresentBoxList>>(gdkey, "ywp_user_present_box_list") ?? new();
            var wanted = Seqs(req["seqs"]).ToHashSet();
            var take = wanted.Count > 0 ? box.Where(p => wanted.Contains(p.Seq)).ToList() : box.Where(p => p.CanReceiveAll == 1).ToList();

            int ymoney = 0, spirits = 0;
            var items = new List<ItemGrant>();
            var youkai = new List<long>();
            foreach (var p in take)
            {
                var count = Math.Max(1, p.DistItemCnt);
                switch (TypeOf(p))
                {
                    case RewardType.YMoney: ymoney += count; break;
                    case RewardType.Hitodama: spirits += count; break;
                    case RewardType.Yokai: youkai.Add(p.DistItemId); break;
                    case RewardType.Item: items.Add(new ItemGrant { Id = p.DistItemId, Count = count }); break;
                    default: break; // icons / titles / shop unlocks: not tracked by Puniemu yet
                }
            }
            if (take.Count > 0)
            {
                await GrantService.GrantAsync(gdkey, ymoney, spirits, items, youkai);
                box.RemoveAll(p => take.Contains(p));
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_present_box_list", box);
                Log.Info("present", $"{gdkey} received {take.Count} present(s)");
            }

            var res = await new CommonResponse().ToDictionary();
            res["resultCode"] = 0; res["resultType"] = 0; res["nextScreenType"] = 0;
            var dict = res.ToDictionary(k => k.Key, v => (object?)v.Value);
            await GeneralUtils.AddTablesToResponse(new[]
            {
                "ywp_user_data", "ywp_user_present_box_list", "ywp_user_item", "ywp_user_youkai", "ywp_user_youkai_skill",
                "ywp_user_dictionary", "ywp_user_youkai_bonus_effect", "ywp_user_icon_budge"
            }, dict, false, gdkey);
            await ctx.Response.WriteAsync(NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(dict)));
        }
    }
}
