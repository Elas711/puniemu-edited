using Newtonsoft.Json;

namespace Puniemu.Src.Server.GameServer.Requests.GetRanking.DataClasses
{
    public class GetRankingRequest
    {
        [JsonProperty("appVer")]
        public string? AppVer { get; set; }

        [JsonProperty("deviceId")]
        public string? DeviceId { get; set; }

        // gdkey (level5 user id) -- matches "level5UserId" in the requests
        [JsonProperty("level5UserId")]
        public string? Level5UserId { get; set; }

        [JsonProperty("userId")]
        public string? UserId { get; set; }

        [JsonProperty("rankType")]
        public int RankType { get; set; }

        [JsonProperty("rowStart")]
        public int RowStart { get; set; }

        [JsonProperty("rowCnt")]
        public int RowCnt { get; set; }

        [JsonProperty("mstVersionVer")]
        public int MstVersionVer { get; set; }

        [JsonProperty("osType")]
        public int OsType { get; set; }

        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("ywpToken")]
        public string? YwpToken { get; set; }

        // some requests include extra fields like activeDeckId, stageNo, score... keep them optional if present
        [JsonProperty("activeDeckId")]
        public int? ActiveDeckId { get; set; }

        [JsonProperty("stageNo")]
        public int? StageNo { get; set; }

        [JsonProperty("score")]
        public long? Score { get; set; }
    }
}
