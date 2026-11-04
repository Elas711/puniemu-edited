using Newtonsoft.Json;

namespace Puniemu.Src.Server.GameServer.Requests.UpdateCrystalMenu.DataClasses
{
    public class UpdateCrystalMenuRequest
    {
        [JsonProperty("appVer")]
        public string? AppVer { get; set; }

        [JsonProperty("deviceId")]
        public string? DeviceId { get; set; }

        [JsonProperty("level5UserId")]
        public string? Level5UserId { get; set; }

        [JsonProperty("mstVersionVer")]
        public int MstVersionVer { get; set; }

        [JsonProperty("osType")]
        public int OsType { get; set; }

        [JsonProperty("crystalMenuId")]
        public int? CrystalMenuId { get; set; }   // added

        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("userId")]
        public string? UserId { get; set; }
    }
}
