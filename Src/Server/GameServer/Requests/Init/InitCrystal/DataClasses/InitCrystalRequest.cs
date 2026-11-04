using Newtonsoft.Json;

namespace Puniemu.Src.Server.GameServer.Requests.InitCrystal.DataClasses
{
    public class InitCrystalRequest
    {
        [JsonProperty("activeDeckId")] public int ActiveDeckId { get; set; }
        [JsonProperty("appVer")] public string? AppVer { get; set; }
        [JsonProperty("deviceId")] public string? DeviceId { get; set; }
        [JsonProperty("level5UserId")] public string? Level5UserId { get; set; }
        [JsonProperty("userId")] public string? UserId { get; set; }
        [JsonProperty("ywpToken")] public string? YwpToken { get; set; }
        [JsonProperty("mstVersionVer")] public int MstVersionVer { get; set; }
        [JsonProperty("osType")] public int OsType { get; set; }
    }
}
