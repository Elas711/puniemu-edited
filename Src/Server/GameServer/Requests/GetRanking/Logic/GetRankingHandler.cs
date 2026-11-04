using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.FriendRequestAccept.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.FriendRequestAccept.Logic;
using Puniemu.Src.Server.GameServer.Requests.GetRanking.DataClasses;
using Puniemu.Src.Utils.GeneralUtils;

namespace Puniemu.Src.Server.GameServer.Requests.GetRanking.Logic
{
    public static class GetRankingHandler
    {
        public static async Task HandleAsync(HttpContext ctx)
        {
            ctx.Request.EnableBuffering();
            var readResult = await ctx.Request.BodyReader.ReadAsync();
            var encRequest = System.Text.Encoding.UTF8.GetString(readResult.Buffer.ToArray());
            ctx.Request.BodyReader.AdvanceTo(readResult.Buffer.End);
            // Decrypt (same technique as the other handlers)
            var requestJsonString = NHNCrypt.Logic.NHNCrypt.DecryptRequest(encRequest);
            var req = JsonConvert.DeserializeObject<GetRankingRequest>(requestJsonString!);
            var res = new GetRankingResponse
            {
                RankType = req?.RankType ?? 0,
            };
            var resdict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(res))!;
            List<string> tables = new();
            tables.Add("ywp_user_data");
            tables.Add("ywp_user_icon_budge");
            tables.Add("ywp_user_score_attack_reward");
            tables.Add("ywp_user_self_rank");
            resdict["ywp_mst_score_attack_league"] = JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(DataManager.Logic.DataManager.GameDataManager!.GamedataCache["ywp_mst_score_attack_league"])!["data"]!;
            tables.Add("ywp_mst_score_attack_league");
            res.ResponseCode = 0;
            switch (req!.RankType)
            {
                case 3:
                    tables.Add("ywp_user_friend_star_rank");
                    break;
                case 4:
                    tables.Add("ywp_user_friend_star_rank");
                    tables.Add("ywp_user_friend_rank");
                    tables.Add("ywp_user_league_rank");
                    break;

                case 5:
                    tables.Add("ywp_user_friend_dictionary_rank");
                    break;

                case 8:
                    tables.Add("ywp_user_all_rank");
                    break;

                default:
                    tables.Add("ywp_user_all_rank");
                    tables.Add("ywp_user_friend_dictionary_rank");
                    tables.Add("ywp_user_friend_star_rank");
                    tables.Add("ywp_user_friend_rank");
                    tables.Add("ywp_user_league_rank");
                    break;
            }
            await GeneralUtils.AddTablesToResponse(tables, resdict!, false, req!.Level5UserId!);
            var marshalledResponse = JsonConvert.SerializeObject(resdict);
            var encryptedResponse = NHNCrypt.Logic.NHNCrypt.EncryptResponse(marshalledResponse);
            await ctx.Response.WriteAsync(encryptedResponse);
        }
    }
}
