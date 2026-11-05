using Newtonsoft.Json;
using Puniemu.Src.Server.Infrastructure;

namespace Puniemu.Src.Server.Gacha
{
    // What a player chose on the in-game Crank-a-kai page (stored with their save).
    public class PlayerGachaChoice
    {
        [JsonProperty("mix")]
        public List<int> Mix { get; set; } = new();

        [JsonProperty("coinDay")]
        public string CoinDay { get; set; } = "";

        [JsonProperty("coinsToday")]
        public int CoinsToday { get; set; }
    }

    public static class GachaChoice
    {
        public const string TABLE = "puniemu_gacha_choice";
        public const int MAX_MIX = 20;

        public static async Task<PlayerGachaChoice> GetAsync(string gdkey)
        {
            try { return await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<PlayerGachaChoice>(gdkey, TABLE) ?? new(); }
            catch { return new(); }
        }

        public static Task SetAsync(string gdkey, PlayerGachaChoice c) => UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, TABLE, c);

        // The machines a player's Y-Money crank-a-kai draws from (their own mix, else the server's).
        public static async Task<List<int>> EffectiveMixAsync(string gdkey)
        {
            var s = ServerStateStore.Current.Gacha;
            if (!s.AllowMix) return s.ServerMix;
            var c = await GetAsync(gdkey);
            return c.Mix.Count > 0 ? c.Mix : s.ServerMix;
        }
    }
}
