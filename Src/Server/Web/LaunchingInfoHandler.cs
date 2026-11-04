using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Puniemu.Src.Server.Web
{
    // The HSP "launching info" the client asks for at boot. Without it some builds stay stuck on the
    // maintenance screen. Combines the approaches of the marlonnblox fork (generated payload) and the
    // WibWobPS port (template from Resources/hspLaunchingInfos.txt with the host substituted).
    public static class LaunchingInfoHandler
    {
        private const string TEMPLATE_TABLE = "hspLaunchingInfos";
        private const string TEMPLATE_PLACEHOLDER = "http://youtube.com";

        public static string PublicBase(HttpContext ctx)
        {
            if (!string.IsNullOrWhiteSpace(DataManager.Logic.DataManager.PublicServerURL))
                return DataManager.Logic.DataManager.PublicServerURL!;
            return $"{ctx.Request.Scheme}://{ctx.Request.Host.Value}";
        }

        public static async Task HandleAsync(HttpContext ctx)
        {
            var baseUrl = PublicBase(ctx);
            string body;
            if (DataManager.Logic.DataManager.GameDataManager.GamedataCache.TryGetValue(TEMPLATE_TABLE, out var template))
            {
                body = template.Replace(TEMPLATE_PLACEHOLDER, baseUrl);
                try
                {
                    var jo = JObject.Parse(body);
                    jo["currentTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (jo["lncNotices"] != null) jo["lncNotices"] = new JArray();
                    body = jo.ToString(Formatting.None);
                }
                catch
                {
                    // Not JSON: send the template as is.
                }
            }
            else
            {
                body = JsonConvert.SerializeObject(Generate(baseUrl));
            }
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(body);
        }

        private static object Generate(string baseUrl)
        {
            return new
            {
                state = "00",
                stateMessage = "",
                loginable = "Y",
                playable = "Y",
                currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                heartBeatInterval = 60,
                cacheTTLSec = 3600,
                brandType = "TOASTGAME",
                serviceDomain = "GLOBAL",
                serverInfos = new Dictionary<string, string>
                {
                    { "LOGIN", baseUrl }, { "GAMESVR", baseUrl }, { "CS", baseUrl }, { "NOTICE", baseUrl },
                    { "IMAGE", baseUrl }, { "IMAGE_RESIZE", baseUrl }, { "MFS", baseUrl }, { "LCS", baseUrl },
                    { "NOMAD", baseUrl }, { "PHOTO", baseUrl }, { "SILOS", baseUrl }, { "GAME_PLUS", baseUrl },
                    { "GAME_INDICATOR_BIP", baseUrl }, { "RTA_BIP", baseUrl }, { "STABILITY_BIP", baseUrl },
                    { "GAME_SERVER", baseUrl }
                },
                loginUrl = new
                {
                    hangameLoginUrl_JP = baseUrl,
                    hangamejp = baseUrl,
                    hangameOAuthUrl = $"{baseUrl}/authorize.nhn",
                    hangameEmailOAuthUrl = $"{baseUrl}/egm/login.nhn"
                },
                userAuthentication = new
                {
                    checkAuthUrl = $"{baseUrl}/hsp/checkAuth.nhn",
                    reAuthUrl = $"{baseUrl}/hsp/reAuth.nhn"
                },
                idpInfo = new
                {
                    hangame = new { selected = "Y", loginable = "Y", consumerKey = "DUMMY_KEY", consumerSecret = "DUMMY_SECRET", redirectionUrl = baseUrl },
                    hangamejp = new { selected = "Y", loginable = "Y", consumerKey = "DUMMY_KEY", consumerSecret = "DUMMY_SECRET", redirectionUrl = baseUrl },
                    toast = new { selected = "Y", loginable = "Y", consumerKey = "DUMMY_KEY", consumerSecret = "DUMMY_SECRET", redirectionUrl = baseUrl },
                    guest = new { selected = "Y", loginable = "Y", consumerKey = "", consumerSecret = "", redirectionUrl = "" },
                    google = new { selected = "N", loginable = "N", consumerKey = "", consumerSecret = "", redirectionUrl = "" },
                    facebook = new { selected = "N", loginable = "N", consumerKey = "", consumerSecret = "", redirectionUrl = "" }
                },
                lncNotices = Array.Empty<object>(),
                serviceInfo = new { maxRankingSize = 100 },
                clientAttributes = new { ENFORCED_IDP_LOGIN = "N", HOLD_IDP_LOGIN = "N" },
                payment = new { selected = "N" },
                logncrash = new { appKey = "DUMMY_APPKEY", logLevel = "ERROR", logtype = "NELO2" },
                stabilityStatus = "Y",
                toastCloudAppInfo = new { Push = new { appKey = "DUMMY_PUSH_APPKEY" } },
                indicatorLogServer = baseUrl,
                loginUrlSimple = $"{baseUrl}/login.nhn",
                termsUrl = $"{baseUrl}/help/inquiry/top.nhn",
                csUrl = $"{baseUrl}/help/inquiry/top.nhn",
                personalInfoCollectionUrl = $"{baseUrl}/help/inquiry/top.nhn",
                punishReasonUrl = $"{baseUrl}/help/inquiry/top.nhn",
                LGCConfigKey = "",
                LGCGameUrl = "",
                LGCGcmProjectNum = ""
            };
        }
    }
}
