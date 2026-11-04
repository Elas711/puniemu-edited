using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Logic;
using Puniemu.Src.Server.Infrastructure;
using Puniemu.Src.TableParser.DataClasses;
using Puniemu.Src.TableParser.Logic;
using UDM = Puniemu.Src.UserDataManager.Logic.UserDataManager;

namespace Puniemu.Src.Server.Admin
{
    // Gives currencies, items and Yo-kai to a save. Used by the admin panel and server-wide gifts.
    public static class GrantService
    {
        public const string CLAIMED_GIFTS_TABLE = "puniemu_claimed_gifts";

        public class GrantResult
        {
            public int YMoney { get; set; }
            public int Hitodama { get; set; }
            public int FreeHitodama { get; set; }
            public List<string> Notes { get; set; } = new();
        }

        public static async Task<GrantResult> GrantAsync(string gdkey, int ymoney, int spirits, IEnumerable<ItemGrant>? items, IEnumerable<long>? youkai)
        {
            var result = new GrantResult();
            var userData = await UDM.GetYwpUserAsync<YwpUserData>(gdkey, "ywp_user_data");
            if (userData == null) throw new InvalidOperationException("This save has no player data yet (tutorial not finished).");

            if (ymoney != 0) userData.YMoney = Math.Max(0, userData.YMoney + ymoney);
            if (spirits != 0) userData.Hitodama = Math.Max(0, userData.Hitodama + spirits);
            await UDM.SetYwpUserAsync(gdkey, "ywp_user_data", userData);

            var itemList = items?.Where(x => x.Id != 0 && x.Count != 0).ToList() ?? new();
            if (itemList.Count > 0)
            {
                var userItems = new TableParser<YwpUserItem>(await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_item") ?? "");
                foreach (var it in itemList)
                {
                    if (it.Count > 0) userItems = ItemManager.AddItem(userItems, it.Id, it.Count);
                    else userItems = ItemManager.RemoveItem(userItems, it.Id, -it.Count);
                }
                await UDM.SetYwpUserAsync(gdkey, "ywp_user_item", userItems.ToString());
            }

            var yokaiList = youkai?.Where(x => x != 0).Distinct().ToList() ?? new();
            if (yokaiList.Count > 0)
            {
                var userYokai = new TableParser<YwpUserYoukai>(await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_youkai") ?? "");
                var userSkill = new TableParser<YwpUserYoukaiSkill>(await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_youkai_skill") ?? "");
                var userBonus = new TableParser<YwpUserYoukaiBonusEffect>(await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_youkai_bonus_effect") ?? "");
                var userDict = new TableParser<YwpUserDictionary>(await UDM.GetYwpUserAsync<string>(gdkey, "ywp_user_dictionary") ?? "");
                foreach (var id in yokaiList)
                {
                    if (YoukaiManager.GetYoukaiIndex(userYokai, id) != -1)
                    {
                        result.Notes.Add($"already owns Yo-kai {id}");
                        continue;
                    }
                    await YoukaiManager.AddYoukai(userYokai, id, userSkill, userBonus, gdkey);
                    DictionaryManager.EditDictionary(ref userDict, id, true, true);
                }
                await UDM.SetYwpUserAsync(gdkey, "ywp_user_youkai", userYokai.ToString());
                await UDM.SetYwpUserAsync(gdkey, "ywp_user_youkai_skill", userSkill.ToString());
                await UDM.SetYwpUserAsync(gdkey, "ywp_user_youkai_bonus_effect", userBonus.ToString());
                await UDM.SetYwpUserAsync(gdkey, "ywp_user_dictionary", userDict.ToString());
            }

            result.YMoney = userData.YMoney;
            result.Hitodama = userData.Hitodama;
            result.FreeHitodama = userData.FreeHitodama;
            return result;
        }

        // Applies every server-wide gift this save hasn't received yet. Returns the messages of the
        // gifts that were applied.
        public static async Task<List<string>> ClaimPendingGiftsAsync(string gdkey)
        {
            var applied = new List<string>();
            var gifts = ServerStateStore.Current.Gifts.ToList();
            if (gifts.Count == 0) return applied;
            var claimed = await UDM.GetYwpUserAsync<List<string>>(gdkey, CLAIMED_GIFTS_TABLE) ?? new List<string>();
            bool changed = false;
            foreach (var g in gifts)
            {
                if (claimed.Contains(g.Id)) continue;
                try
                {
                    await GrantAsync(gdkey, g.YMoney, g.Spirits, g.Items, g.Youkai);
                    applied.Add(g.Message);
                    Metrics.Incr("gifts_claimed");
                }
                catch (Exception ex)
                {
                    // Save not ready yet (e.g. still in the tutorial): try again next login.
                    Log.Warn("gifts", $"Gift {g.Id} not applied to {gdkey}: {ex.Message}");
                    continue;
                }
                claimed.Add(g.Id);
                changed = true;
            }
            if (changed) await UDM.SetYwpUserAsync(gdkey, CLAIMED_GIFTS_TABLE, claimed);
            return applied;
        }
    }
}
