using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;

namespace Puniemu.Src.Server.GameServer.Requests.InitCrystal.DataClasses
{
    public class InitCrystalResponse : CommonResponse
    {
        private JObject? _jo;

        public static async Task<InitCrystalResponse> BuildAsync(InitCrystalRequest request)
        {
            var inst = new InitCrystalResponse();
            
            // Create the base structure
            var jo = new JObject();
            
            // Fill in the (static) master data
            await PopulateMasterData(jo);
            
            // Update the timestamp
            jo["serverDt"] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            
            // Fill ywp_user_data from the database
            await PopulateUserData(jo, request);
            
            // Initialize the user event tables as empty
            InitializeUserEventTables(jo);
            
            inst._jo = jo;
            return inst;
        }

        private static async Task PopulateMasterData(JObject jo)
        {
            // Master table - Crystal menu data
            jo["ywp_mst_crystal_menu"] = CreateCrystalMenuMaster();
            
            // Autres champs statiques
            jo["shopSaleList"] = new JArray();
            jo["ywpToken"] = "";
            jo["ymoneyShopSaleList"] = new JArray();
            jo["mstVersionMaster"] = int.Parse(DataManager.Logic.DataManager.GameDataManager.GamedataCache["mstVersionMaster"]);
            jo["resultCode"] = 0;
            jo["ywp_user_icon_budge"] = "9000|5*5|1*2000|0*10|1*2007|1*2008|1*2009|1*2010|0*9004|21*9003|0*9007|0*9008|0*9009|0*10959|0*20959|0";
            jo["nextScreenType"] = 0;
            jo["dialogMsg"] = "";
            jo["hitodamaShopSaleList"] = new JArray();
            jo["webServerIp"] = "";
            jo["storeUrl"] = "";
            jo["dialogTitle"] = "";
            jo["resultType"] = 0;
        }

        private static async Task PopulateUserData(JObject jo, InitCrystalRequest request)
        {
            // Initialize with default data
            jo["ywp_user_data"] = new JObject
            {
                ["birthday"] = "",
                ["freeHitodama"] = 3,
                ["friendMaxCnt"] = 10,
                ["plateId"] = 1,
                ["medalPoint"] = 0,
                ["webCode"] = "y1zy12nfflmpxw6q",
                ["nowStageId"] = 1001006,
                ["titleId"] = 1,
                ["gokuCollectCnt"] = 0,
                ["ymoney"] = 3285,
                ["reviewFlg"] = 0,
                ["moveReason"] = 0,
                ["chargeYmoney"] = 0,
                ["effectId"] = 1,
                ["crystalCollectCnt"] = 0,
                ["eventPointUpItemId"] = 0,
                ["characterId"] = "3qjiw6vi",
                ["iconId"] = 1,
                ["limitTimeSaleRemainSec"] = 63780,
                ["totMedalPoint"] = 0,
                ["eventPointUpItemRemainSec"] = 0,
                ["playerName"] = "superog",
                ["todaysRemainSec"] = 63781,
                ["weeklyFreeFlg"] = 1,
                ["hitodama"] = 0,
                ["lastRenameDt"] = 0,
                ["codenameId"] = 1,
                ["usingItemList"] = new JArray(),
                ["hitodamaRecoverSec"] = 240,
                ["limitTimeSaleEndDt"] = "2025-09-15 23:59:59",
                ["equipWatchId"] = 10101
            };

            // Try to load the real user data from the database
            try
            {
                if (!string.IsNullOrEmpty(request?.DeviceId))
                {
                    var gdkeys = await Puniemu.Src.UserDataManager.Logic.UserDataManager.GetGdkeysFromUdkeyAsync(request.DeviceId);
                    if (gdkeys != null && gdkeys.Count > 0)
                    {
                        var tables = (await Puniemu.Src.UserDataManager.Logic.UserDataManager.GetEntireUserData(Puniemu.Src.Utils.GeneralUtils.GeneralUtils.PickSave(gdkeys, request?.Level5UserId)))!;
                        if (tables != null && tables.ContainsKey("ywp_user_data"))
                        {
                            if (tables["ywp_user_data"] != null)
                            {
                                jo["ywp_user_data"] = JObject.FromObject(tables["ywp_user_data"]!);
                            } else
                            {
                                jo["ywp_user_data"] = null;
                            }
                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue with the default data
                Console.WriteLine($"Error while loading the user data: {ex.Message}");
            }
        }

        private static void InitializeUserEventTables(JObject jo)
        {
            // User event tables - initialized empty for a new user
            jo["ywp_user_crystal_menu"] = new JArray();
        }

        private static JArray CreateCrystalMenuMaster()
        {
            var menus = new JArray();
            
            var menuData = new[]
            {
                new { youkaiId = 2927000, crystalMenuId = 1, webViewName = "CGet_bushinyan", openDt = CreateDateTime(2018, 3, 1) },
                new { youkaiId = 9000294, crystalMenuId = 2, webViewName = "CGet_yamabukioni", openDt = CreateDateTime(2018, 7, 17) },
                new { youkaiId = 9000263, crystalMenuId = 3, webViewName = "CGet_netabarerina", openDt = CreateDateTime(2018, 6, 16) },
                new { youkaiId = 9000313, crystalMenuId = 4, webViewName = "CGet_hanasakajii", openDt = CreateDateTime(2018, 9, 17) },
                new { youkaiId = 9000379, crystalMenuId = 5, webViewName = "CGet_ikemenken", openDt = CreateDateTime(2019, 1, 17) },
                new { youkaiId = 9000380, crystalMenuId = 6, webViewName = "CGet_yamatan", openDt = CreateDateTime(2019, 1, 17) },
                new { youkaiId = 9000312, crystalMenuId = 7, webViewName = "CGet_shurakoma", openDt = CreateDateTime(2018, 9, 17) },
                new { youkaiId = 9000394, crystalMenuId = 8, webViewName = "CGet_unchikuma", openDt = CreateDateTime(2019, 2, 15) }
            };

            var closeDt = CreateDateTime(8099, 12, 31, 23, 59, 59);

            foreach (var menu in menuData)
            {
                menus.Add(new JObject
                {
                    ["youkaiId"] = menu.youkaiId,
                    ["openDt"] = menu.openDt,
                    ["crystalMenuId"] = menu.crystalMenuId,
                    ["webViewName"] = menu.webViewName,
                    ["closeDt"] = closeDt
                });
            }

            return menus;
        }

        private static JObject CreateDateTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        {
            long unixTime;
            int dayOfWeek;
            
            // For impossible future dates (like 8099) a hardcoded value is used
            if (year >= 3000)
            {
                unixTime = 253402268399000; // Value from the original JSON for 8099
                dayOfWeek = 5; // Friday (value from the original JSON)
            }
            else
            {
                var dt = new DateTime(year, month, day, hour, minute, second);
                unixTime = ((DateTimeOffset)dt).ToUnixTimeMilliseconds();
                dayOfWeek = (int)dt.DayOfWeek;
            }
            
            return new JObject
            {
                ["date"] = day,
                ["day"] = dayOfWeek,
                ["hours"] = hour,
                ["minutes"] = minute,
                ["month"] = month - 1, // JavaScript months are 0-indexed
                ["seconds"] = second,
                ["time"] = unixTime,
                ["timezoneOffset"] = -540, // JST timezone offset
                ["year"] = year - 1900 // JavaScript years are offset by 1900
            };
        }

        public async Task<Dictionary<string, object>> ToDictionary(string gdkey)
        {
            // Convert the JObject to a Dictionary suitable for serialisation/chiffrement
            return _jo!.ToObject<Dictionary<string, object>>()!;
        }
    }
}