using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Buffers;
using Newtonsoft.Json;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.FriendRequestAccept.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.FriendRequestAccept.Logic;
using Puniemu.Src.Server.GameServer.Requests.FriendDelete.DataClasses;

namespace Puniemu.Src.Server.GameServer.Requests.FriendDelete.Logic
{
    public class FriendDeleteHandler
    {
        public static async Task HandleAsync(HttpContext ctx)
        {
            ctx.Request.EnableBuffering();
            var readResult = await ctx.Request.BodyReader.ReadAsync();
            var encRequest = Encoding.UTF8.GetString(readResult.Buffer.ToArray());
            ctx.Request.BodyReader.AdvanceTo(readResult.Buffer.End);

            // Decrypt like in the other handlers
            var requestJsonString = NHNCrypt.Logic.NHNCrypt.DecryptRequest(encRequest);
            var deserialized = JsonConvert.DeserializeObject<FriendDeleteRequest>(requestJsonString!);

            var res = new FriendDeleteResponse { ResponseCode = 1 }; // default = failure

            if (deserialized == null || string.IsNullOrEmpty(deserialized.Level5UserID) || string.IsNullOrEmpty(deserialized.TargetUserId))
            {
                // Invalid request
                var outErr = NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(res));
                await ctx.Response.WriteAsync(outErr);
                return;
            }

            // Load my data (requester)
            var myData = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<YwpUserData>(
                deserialized.Level5UserID, "ywp_user_data"
            );

            if (myData == null || string.IsNullOrEmpty(myData.UserID))
            {
                // Could not find the requester
                res.ResponseCode = 1;
                var outErr = NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(res));
                await ctx.Response.WriteAsync(outErr);
                return;
            }

            string myUserId = myData.UserID;
            string myGdkey = deserialized.Level5UserID;
            string targetUserId = deserialized.TargetUserId;

            // Look up the target player's gdkey
            string? targetGdkey = null;
            try
            {
                targetGdkey = await UserDataManager.Logic.UserDataManager.GetGdkeyFromUserId(targetUserId);
            }
            catch
            {
                targetGdkey = null;
            }

            // --- Removal on the requester's side ---
            var myFriends = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendEntry>>(
                myGdkey, "ywp_user_friend"
            ) ?? new List<FriendEntry>();

            // remove targetUserId from my friend list
            myFriends.RemoveAll(f => f != null && f.UserId == targetUserId);

            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(myGdkey, "ywp_user_friend", myFriends);

            // Also remove it from my other tables (rank / star_rank / request_recv)
            await RemoveFriendFromAllTables(myGdkey, targetUserId);

            // --- Removal on the target's side (if we have their gdkey) ---
            if (!string.IsNullOrEmpty(targetGdkey))
            {
                var targetFriends = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendEntry>>(
                    targetGdkey, "ywp_user_friend"
                ) ?? new List<FriendEntry>();

                // remove my UserID from their friend list (NOTE: compared against myData.UserID)
                targetFriends.RemoveAll(f => f != null && f.UserId == myUserId);

                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(targetGdkey, "ywp_user_friend", targetFriends);

                // Also remove it from their other tables
                await RemoveFriendFromAllTables(targetGdkey, myUserId);
            }

            // Reload the requester's updated tables (for the response)
            res.YwpUserData = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<YwpUserData>(myGdkey, "ywp_user_data");
            res.YwpUserFriend = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendEntry>>(myGdkey, "ywp_user_friend") ?? new List<FriendEntry>();
            res.YwpUserFriendRank = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendEntry>>(myGdkey, "ywp_user_friend_rank") ?? new List<FriendEntry>();
            res.YwpUserFriendStarRank = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendRankEntry>>(myGdkey, "ywp_user_friend_star_rank") ?? new List<FriendRankEntry>();
            res.YwpUserFriendRequestRecv = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendRequestEntry>>(myGdkey, "ywp_user_friend_request_recv") ?? new List<FriendRequestEntry>();

            res.ResponseCode = 0;

            var outDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(res));
            var outResponse = NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(outDict));
            await ctx.Response.WriteAsync(outResponse);
        }

        private static async Task RemoveFriendFromAllTables(string gdkey, string removedUserId)
        {
            // ywp_user_friend (FriendEntry)
            var friends = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendEntry>>(gdkey, "ywp_user_friend") ?? new List<FriendEntry>();
            friends.RemoveAll(f => f != null && f.UserId == removedUserId);
            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_friend", friends);

            // ywp_user_friend_rank (FriendEntry)
            var rank = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendEntry>>(gdkey, "ywp_user_friend_rank") ?? new List<FriendEntry>();
            rank.RemoveAll(f => f != null && f.UserId == removedUserId);
            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_friend_rank", rank);

            // ywp_user_friend_star_rank (FriendStarRankEntry from FriendRequestAccept.Logic)
            var starRank = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendRankEntry>>(gdkey, "ywp_user_friend_star_rank") ?? new List<FriendRankEntry>();
            starRank.RemoveAll(f => f != null && f.UserId == removedUserId);
            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_friend_star_rank", starRank);

            // ywp_user_friend_request_recv (FriendRequestEntry)
            var recv = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<List<FriendRequestEntry>>(gdkey, "ywp_user_friend_request_recv") ?? new List<FriendRequestEntry>();
            recv.RemoveAll(f => f != null && f.UserId == removedUserId);
            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_friend_request_recv", recv);

            // Note: the ywp_user_data object itself is not deleted.
        }
    }
}
