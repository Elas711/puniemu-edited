using Newtonsoft.Json;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.GameEndScoreAttack.DataClasses;
using Puniemu.Src.Utils.GeneralUtils;
using System.Buffers;
using System.Text;

namespace Puniemu.Src.Server.GameServer.Requests.GameEndScoreAttack.Logic
{
    public static class GameEndScoreAttackHandler
    {
        public static async Task HandleAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Request.EnableBuffering();
            var buffer = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(buffer);
            buffer.Seek(0, SeekOrigin.Begin);

            string? requestJsonString;
            using (var reader = new StreamReader(buffer, Encoding.UTF8))
            {
                var readResult = await reader.ReadToEndAsync();
                requestJsonString = NHNCrypt.Logic.NHNCrypt.DecryptRequest(readResult);
            }

            var deserialized = JsonConvert.DeserializeObject<GameEndScoreAttackRequest>(requestJsonString!);
            if (deserialized == null)
            {
                await GeneralUtils.SendBadRequest(ctx);
                return;
            }

            // Check the session
            var reqId = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(deserialized.Level5UserId!, "ywp_user_requestid");
            if (reqId == null || deserialized.RequestId == null || reqId == "" || deserialized.RequestId == "" || reqId != deserialized.RequestId)
            {
                var errSession = new MsgBoxResponse("This session is invalid", "INVALID SESSION");
                await ctx.Response.WriteAsync(NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(errSession)));
                return;
            }

            var userData = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<YwpUserData>(deserialized.Level5UserId!, "ywp_user_data");
            if (userData == null)
            {
                await GeneralUtils.SendBadRequest(ctx);
                return;
            }

            var res = new GameEndScoreAttackResponse();

            // Compute the score attack result data
            res.UserGameResultData.Score = deserialized.Score;
            res.UserGameResultData.Exp = Math.Min(deserialized.Score / 400, 500);
            res.UserGameResultData.Money = Math.Min(deserialized.Score / 2000, 100);




            // Edit total best score
            var hist_total = new TableParser.Logic.TableParser(await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(deserialized.Level5UserId!, "ywp_user_hist_total"));
            // The stored best used to be overwritten with a hardcoded value here, which reset the
            // record every run. Only repair it when it is missing or not a number.
            if (!long.TryParse(hist_total.Table[0][22], out var total_bestScore))
            {
                total_bestScore = 0;
                hist_total.Table[0][22] = "0";
            }
            bool isNewRecord = false;
            if (deserialized.Score > total_bestScore)
            {
                isNewRecord = true;
                hist_total.Table[0][22] = deserialized.Score.ToString();
                hist_total.Table[0][21] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                total_bestScore = deserialized.Score;
            }
            res.UserGameResultData.TotalHighScore = total_bestScore;

            // Edit weekly best score
            var hist_weekly = new TableParser.Logic.TableParser(await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(deserialized.Level5UserId!, "ywp_user_hist_puzzle_weekly"));
            if (!long.TryParse(hist_weekly.Table[3][0], out var weekly_bestScore)) weekly_bestScore = 0;
            if (deserialized.Score > weekly_bestScore)
            {
                hist_weekly.Table[3][0] = deserialized.Score.ToString();
                weekly_bestScore = deserialized.Score;
            }
            res.UserGameResultData.WeekHighScore = weekly_bestScore;

            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, "ywp_user_hist_puzzle_weekly", hist_weekly.ToString());
            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, "ywp_user_hist_total", hist_total.ToString());

            try
            {

                // Flag that there is a new score for GetRanking to process
                var pendingScore = new Dictionary<string, object>
                {
                    ["score"] = deserialized.Score,
                    ["best_score"] = total_bestScore,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ["is_new_record"] = isNewRecord,
                    ["use_current_score"] = isNewRecord // If it is a new record, show it in the ranking
                };
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, "ywp_pending_score", pendingScore);

                // Update res.UserGameResultData
                res.UserGameResultData.ScoreUpdateFlg = isNewRecord ? 1 : 0;
                // Handle the Yo-kai that were used (existing code)


                var userYoukai = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(deserialized.Level5UserId!, "ywp_user_youkai");
                var userYoukaiTable = new TableParser.Logic.TableParser(userYoukai!);
                var YoukaiMstTable = new TableParser.Logic.TableParser(JsonConvert.DeserializeObject<Dictionary<string, string>>(DataManager.Logic.DataManager.GameDataManager!.GamedataCache["ywp_mst_youkai"]!)!["tableData"]);
                var YoukaiLevelMstTable = new TableParser.Logic.TableParser(JsonConvert.DeserializeObject<Dictionary<string, string>>(DataManager.Logic.DataManager.GameDataManager.GamedataCache["ywp_mst_youkai_level"]!)!["tableData"]);

                foreach (var yokaiResult in deserialized.UserYoukaiResultList)
                {
                    var youkaiIdx = userYoukaiTable.FindIndex([yokaiResult.YoukaiId.ToString()]);
                    if (youkaiIdx != -1)
                    {
                        var YoukaiMstIndex = YoukaiMstTable.FindIndex([yokaiResult.YoukaiId.ToString()]);
                        if (YoukaiMstIndex != -1)
                        {
                            var levelType = int.Parse(YoukaiMstTable.Table[YoukaiMstIndex][5]);

                            var resultItem = new UserYoukaiScoreAttackResultRes();
                            resultItem.HaveFlg = false;
                            resultItem.CanEvolve = false;
                            resultItem.IsLockLevel = false;
                            resultItem.IsMaxLevel = false;
                            resultItem.YoukaiId = yokaiResult.YoukaiId;

                            // State before
                            resultItem.Before.Level = int.Parse(userYoukaiTable.Table[youkaiIdx][1]);
                            resultItem.Before.Exp = int.Parse(userYoukaiTable.Table[youkaiIdx][2]);
                            resultItem.Before.ExpBar.Denominator = int.Parse(userYoukaiTable.Table[youkaiIdx][5]);
                            resultItem.Before.ExpBar.Numerator = int.Parse(userYoukaiTable.Table[youkaiIdx][6]);
                            resultItem.Before.ExpBar.Percentage = int.Parse(userYoukaiTable.Table[youkaiIdx][7]);

                            // Compute the exp earned from the damage dealt
                            var expGain = Math.Min(yokaiResult.DamageTotal / 10, 200);
                            resultItem.After.Exp = resultItem.Before.Exp + expGain;

                            // Compute the new level and the exp bar
                            int newLevel = resultItem.Before.Level;
                            int index = 1;
                            bool levelFound = false;

                            while (!levelFound)
                            {
                                var levelIndex = -1;
                                for (int tmpIdx = 0; tmpIdx < YoukaiLevelMstTable.Table.Count; tmpIdx++)
                                {
                                    if (YoukaiLevelMstTable.Table[tmpIdx][0] == levelType.ToString() && YoukaiLevelMstTable.Table[tmpIdx][1] == index.ToString())
                                    {
                                        levelIndex = tmpIdx;
                                        break;
                                    }
                                }

                                if (levelIndex != -1)
                                {
                                    int minExp = int.Parse(YoukaiLevelMstTable.Table[levelIndex][2]);
                                    int maxExp = int.Parse(YoukaiLevelMstTable.Table[levelIndex][3]);

                                    if (resultItem.After.Exp >= minExp && resultItem.After.Exp <= maxExp)
                                    {
                                        newLevel = int.Parse(YoukaiLevelMstTable.Table[levelIndex][1]);
                                        resultItem.After.ExpBar.Denominator = (maxExp + 1) - minExp;
                                        resultItem.After.ExpBar.Numerator = resultItem.After.Exp - minExp;
                                        resultItem.After.ExpBar.Percentage = (int)(((double)resultItem.After.ExpBar.Numerator / resultItem.After.ExpBar.Denominator) * 100);
                                        levelFound = true;
                                    }
                                    index++;
                                }
                                else
                                {
                                    // Niveau max atteint
                                    resultItem.IsMaxLevel = true;
                                    levelFound = true;
                                }
                            }

                            resultItem.After.Level = newLevel;

                            // Check whether the level changed
                            if (resultItem.Before.Level != resultItem.After.Level)
                            {
                                resultItem.HaveFlg = false; // Level up flag
                            }

                            // Update the Yo-kai data
                            userYoukaiTable.Table[youkaiIdx][1] = resultItem.After.Level.ToString();
                            userYoukaiTable.Table[youkaiIdx][2] = resultItem.After.Exp.ToString();
                            userYoukaiTable.Table[youkaiIdx][5] = resultItem.After.ExpBar.Denominator.ToString();
                            userYoukaiTable.Table[youkaiIdx][6] = resultItem.After.ExpBar.Numerator.ToString();
                            userYoukaiTable.Table[youkaiIdx][7] = resultItem.After.ExpBar.Percentage.ToString();

                            res.UserYoukaiResultList.Add(resultItem);
                        }
                    }
                }

                // Give the money and exp to the user
                userData.YMoney += (int)res.UserGameResultData.Money;

                // Clear the requestId and save the data
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, "ywp_user_requestid", "");
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, "ywp_user_youkai", userYoukaiTable.ToString());
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, "ywp_user_data", userData);

                // Reset the continue counter for this score attack
                string scoreAttackKey = $"sa_continues_{deserialized.RequestId}";
                try
                {
                    await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(deserialized.Level5UserId!, scoreAttackKey, 0);
                }
                catch
                {
                    // Ignore if the key does not exist
                }

                var resDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(res));
                if (resDict == null)
                {
                    await GeneralUtils.SendBadRequest(ctx);
                    return;
                }

                // Tables required for score attack end
                List<string> tables = new()
                {
                    "ywp_user_youkai",
                    "ywp_user_tutorial_list",
                    "ywp_user_youkai_bonus_effect",
                    "ywp_user_event",
                    "ywp_user_hist_youkai_daily",
                    "ywp_user_youkai_strong_skill",
                    "ywp_user_hist_youkai_total",
                    "ywp_user_hist_puzzle_weekly",
                    "ywp_user_league_rank",
                    "ywp_user_dictionary",
                    "ywp_user_map",
                    "ywp_user_hist_total",
                    "ywp_user_self_rank",
                    "ywp_user_hist_puzzle_daily",
                    "ywp_user_stage_relation_progress",
                    "ywp_user_youkai_skill",
                    "ywp_user_icon_budge",
                    "ywp_mst_event",
                    "ywp_user_stage",
                    "ywp_user_steal_progress",
                    "ywp_user_data",
                    "ywp_user_score_attack_reward",
                    "ywp_user_shop_item_unlock",
                    "ywp_user_event_ranking_reward",
                    "ywp_user_friend_star_rank",
                    "ywp_user_friend_rank"
                };

                await GeneralUtils.AddTablesToResponse(tables, resDict!, true, deserialized.Level5UserId!);

                var encryptedRes = NHNCrypt.Logic.NHNCrypt.EncryptResponse(JsonConvert.SerializeObject(resDict));
                await ctx.Response.WriteAsync(encryptedRes);

                Console.WriteLine($"[GameEndScoreAttack] User {deserialized.Level5UserId} finished score attack with score {deserialized.Score} (best: {total_bestScore}, new record: {isNewRecord})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameEndScoreAttack] Error for user {deserialized.Level5UserId}: {ex}");
                await GeneralUtils.SendBadRequest(ctx);
            }
        }
    }
}