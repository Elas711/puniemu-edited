using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.DataClasses.Mission;
using Puniemu.Src.Server.GameServer.Logic;
using Puniemu.Src.TableParser.DataClasses;
using Puniemu.Src.TableParser.Logic;
using Puniemu.Src.Utils.GeneralUtils;
using System.Text;

namespace Puniemu.Src.Server.GameServer.Requests.Extra
{
    // Endpoints added from the request / response layouts in docs/PROTOCOL.md.
    public static class ExtraHandlers
    {
        private static async Task<JObject> ReadRequest(HttpContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            return JObject.Parse(NHNCrypt.Logic.NHNCrypt.DecryptRequest((await reader.ReadToEndAsync()).Trim())!);
        }

        private static string Gdkey(JObject req) => req.Value<string>("level5UserID") ?? req.Value<string>("level5UserId") ?? "";

        private static async Task<Dictionary<string, object?>> Common()
        {
            var res = await new CommonResponse().ToDictionary();
            res["resultCode"] = 0; res["resultType"] = 0; res["nextScreenType"] = 0;
            return res.ToDictionary(k => k.Key, v => (object?)v.Value);
        }

        private static async Task Send(HttpContext ctx, object res)
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(res)));
        }

        // getEventMaster.nhn: the event table (ywp_mst_event, with the player's event selection
        // applied) plus the usual user data.
        public static async Task GetEventMasterAsync(HttpContext ctx)
        {
            var req = await ReadRequest(ctx);
            var gdkey = Gdkey(req);
            var res = await Common();
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            if (cache.TryGetValue("ywp_mst_event", out var ev))
            {
                var served = await Events.EventManager.ServeAsync("ywp_mst_event", ev, gdkey);
                try { res["ywp_mst_event"] = JToken.Parse(served); } catch { res["ywp_mst_event"] = served; }
            }
            await GeneralUtils.AddTablesToResponse(new[] { "ywp_user_data", "ywp_user_icon_budge" }, res, false, gdkey);
            await Send(ctx, res);
        }

        // missionRewardAll.nhn: claims every mission that is complete and waiting for its reward.
        // Each one goes through the normal missionReward.nhn logic (rewards, next mission unlocks);
        // the answer of the last claim carries all the updated tables.
        public static async Task MissionRewardAllAsync(HttpContext ctx)
        {
            var req = await ReadRequest(ctx);
            var gdkey = Gdkey(req);
            object? last = null;
            int claimed = 0;
            // claiming can complete / unlock further missions: repeat a few rounds
            for (int round = 0; round < 5; round++)
            {
                var missions = new TableParser<YwpUserMission>(await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(gdkey, "ywp_user_mission"));
                var ready = missions.Items.Where(m => m.MissionCompleteStatus == MissionCompleteStatus.CompletePendingReward).Select(m => m.MissionID).Distinct().ToList();
                if (ready.Count == 0) break;
                foreach (var id in ready)
                {
                    var body = (JObject)req.DeepClone();
                    body["missionId"] = id;
                    body["forceFlg"] = 0;
                    var inner = new DefaultHttpContext();
                    inner.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Encrypt(body.ToString(Formatting.None))));
                    var output = new MemoryStream();
                    inner.Response.Body = output;
                    await MissionReward.Logic.MissionRewardHandler.HandleAsync(inner);
                    var text = Encoding.UTF8.GetString(output.ToArray());
                    var json = JObject.Parse(DecryptResponse(text));
                    if (string.IsNullOrEmpty(json.Value<string>("dialogMsg"))) { last = json; claimed++; }
                }
            }
            if (last == null)
            {
                var res = await Common();
                await GeneralUtils.AddTablesToResponse(new[] { "ywp_user_data", "ywp_user_mission", "ywp_user_item", "ywp_user_youkai" }, res, false, gdkey);
                last = res;
            }
            Infrastructure.Log.Info("mission", $"{gdkey} claimed {claimed} mission reward(s) at once");
            await Send(ctx, last);
        }

        // The game encrypts requests the same way the server encrypts responses (see Tools/test_server.py):
        // AES-ECB(SHA1 digest + json); responses are gzipped first.
        private static readonly byte[] KEY = Convert.FromHexString("a865d7e5e2458f8ce1b5ecd087e54594");
        private static readonly byte[] SALT = Encoding.ASCII.GetBytes("0bk2kvtFE2");

        private static string Encrypt(string json)
        {
            var body = Encoding.UTF8.GetBytes(json);
            var d1 = System.Security.Cryptography.SHA1.HashData(SALT.Concat(new byte[] { 0x20 }).Concat(body).ToArray());
            var digest = System.Security.Cryptography.SHA1.HashData(SALT.Concat(d1).ToArray());
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = KEY;
            var enc = aes.EncryptEcb(digest.Concat(body).ToArray(), System.Security.Cryptography.PaddingMode.PKCS7);
            return Convert.ToBase64String(enc).Replace('+', '-').Replace('/', '_');
        }

        private static string DecryptResponse(string text)
        {
            var t = text.Trim().Replace('-', '+').Replace('_', '/');
            t += new string('=', (4 - t.Length % 4) % 4);
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = KEY;
            var plain = aes.DecryptEcb(Convert.FromBase64String(t), System.Security.Cryptography.PaddingMode.PKCS7);
            using var gz = new System.IO.Compression.GZipStream(new MemoryStream(plain, 20, plain.Length - 20), System.IO.Compression.CompressionMode.Decompress);
            using var sr = new StreamReader(gz, Encoding.UTF8);
            return sr.ReadToEnd();
        }
    }
}
