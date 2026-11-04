using Newtonsoft.Json;

namespace Puniemu.Src.Server.GameServer.Requests.UpdateGokuMenu.DataClasses
{
    public class UpdateGokuMenuRequest
    {
        [JsonProperty("activeDeckId")]
        public int ActiveDeckId { get; set; }

        [JsonProperty("appVer")]
        public string? AppVer { get; set; }

        // UDKey / device id sent by the client
        [JsonProperty("deviceId")]
        public string? DeviceId { get; set; }

        [JsonProperty("gokuMenuId")]
        public int GokuMenuId { get; set; }

        [JsonProperty("level5UserId")]
        public string? Level5UserId { get; set; }

        [JsonProperty("mstVersionVer")]
        public int MstVersionVer { get; set; }

        [JsonProperty("osType")]
        public int OsType { get; set; }

        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("userId")]
        public string? UserId { get; set; }

        [JsonProperty("ywpToken")]
        public string? YwpToken { get; set; }
    }
}
