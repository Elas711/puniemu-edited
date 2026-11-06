using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;

namespace Puniemu.Src.Utils.GeneralUtils
{
    public static class GeneralUtils
    {
        public static async Task SendBadRequest(HttpContext ctx)
        {
            ctx.Response.Headers.ContentType = "text/plain";
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("Bad request");
        }

        // Picks the save a request is about: the one the client named, if the device owns it,
        // otherwise the device's first save.
        // The saves a request may use: the device's saves, or - when the device is unknown (lost
        // device row, request without deviceId) - the save the request names, if it exists.
        // Without this the menus fell back to a placeholder player ("superog").
        public static async Task<List<string>> SavesForRequestAsync(string? deviceId, string? level5UserId)
        {
            var gdkeys = string.IsNullOrEmpty(deviceId) ? new List<string>()
                : await UserDataManager.Logic.UserDataManager.GetGdkeysFromUdkeyAsync(deviceId);
            if ((gdkeys == null || gdkeys.Count == 0) && !string.IsNullOrEmpty(level5UserId)
                && await UserDataManager.Logic.UserDataManager.GetAccountFromGdkeyAsync(level5UserId) != null)
                return new List<string> { level5UserId };
            return gdkeys ?? new List<string>();
        }

        // A string for a <script> block: JSON with < > & ' " escaped, so a value like
        // "</script><script>..." from the URL can't end the script (reflected XSS).
        public static string JsString(string? value) =>
            Newtonsoft.Json.JsonConvert.SerializeObject((value ?? "").Length > 64 ? "" : value ?? "",
                new Newtonsoft.Json.JsonSerializerSettings { StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.EscapeHtml });

        public static string PickSave(List<string> gdkeys, string? preferred)
        {
            if (!string.IsNullOrEmpty(preferred) && gdkeys.Contains(preferred)) return preferred;
            return gdkeys[0];
        }

        // Removes the characters that delimit NHN tables (| and *) and control characters from
        // player supplied text, and caps its length.
        public static string SanitizePlayerText(string? text, int maxLength = 24)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (c == '|' || c == '*' || char.IsControl(c)) continue;
                sb.Append(c);
            }
            var s = sb.ToString().Trim();
            return s.Length > maxLength ? s.Substring(0, maxLength) : s;
        }

        public static T DeserializeGameDataToTypeAndCheckValidity<T>(string gamedataName)
        {
            T? output = JsonConvert.DeserializeObject<T>(DataManager.Logic.DataManager.GameDataManager!.GamedataCache[gamedataName]);
            if (output == null) throw new FormatException($"{gamedataName} static gamedata should be a(n) {typeof(T).FullName}");
            return output;
        }

        public static async Task AddTablesToResponse(IEnumerable<string> tables, Dictionary<string,object?> resultDictionary, bool isDownloadOnce, string gdkey = "")
        {
            Dictionary<string, object>? userTables = null;

            if(isDownloadOnce && gdkey != string.Empty)
            {
                userTables = (await UserDataManager.Logic.UserDataManager.GetEntireUserData(gdkey))!;
            }
            foreach(var table in tables)
            {
                string? tableText = null!;
                object? tableObj = new();
                if (table.StartsWith("ywp_user"))
                {
                    if (gdkey == "" || gdkey == null) continue;
                    if(isDownloadOnce && userTables != null)
                    {
                        tableObj = userTables[table];
                    }
                    else
                    {
                        tableObj = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<object>(gdkey, table);
                    }
                }
                //Meaning it's constant
                else if (DataManager.Logic.DataManager.GameDataManager!.GamedataCache.TryGetValue(table, out string outData)) 
                {
                    // Apply the event selection (no-op unless events are switched)
                    tableText = await Puniemu.Src.Server.Events.EventManager.ServeAsync(table, outData, string.IsNullOrEmpty(gdkey) ? null : gdkey);
                }
                else
                {
                    Console.WriteLine("Table not found: " + table);
                    continue;
                }

                if (tableText != null)
                {
                    //if we can't deserialize json it means it's not a json and we store it as is
                    try
                    {
                        // If was cud structure, only send the data
                        tableObj = JsonConvert.DeserializeObject<object>(tableText);
                        try
                        {
                            var dict = ((JObject)tableObj!).ToObject<Dictionary<string, object>>()!;

                            if (dict!.ContainsKey("data"))
                            {
                                tableObj = dict["data"];
                            }
                            else if (dict.ContainsKey("tableData"))
                            {
                                tableObj = dict["tableData"];
                            }

                        }
                        catch
                        {
                            //Just continue everything as normal if it's not a dict
                        }
                        
                    }
                    catch
                    {
                        tableObj = tableText;
                    }
                }
                resultDictionary[table] = tableObj!;
            }
        }
    }
}
