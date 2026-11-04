using Puniemu.Src.Server.L5ID.Requests.CreateGDKey.DataClasses;
using Puniemu.Src.Server.L5ID.DataClasses;
using Newtonsoft.Json;

namespace Puniemu.Src.Server.L5ID.Requests.CreateGDKey.Logic
{
    public class CreateGDKeyHandler
    {
        public async static Task HandleAsync(HttpContext ctx)
        {
            var query = ctx.Request.Query.ToDictionary();
            ctx.Response.Headers.ContentType = "application/json";
            if (!query.ContainsKey("udkey"))
            {
                var badResponse = new BadL5IDResponse(L5IDErr.UNKNOWN_UDKEY, "Unknown UDKey");
                var json = JsonConvert.SerializeObject(badResponse);
                await ctx.Response.WriteAsync(json);
            }
            else
            {
                string udkey = query["udkey"]!;
                if (string.IsNullOrWhiteSpace(udkey) || udkey.Length > 128)
                {
                    await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new BadL5IDResponse(L5IDErr.UNKNOWN_UDKEY, "Unknown UDKey")));
                    return;
                }
                // Without a device row the new save would be orphaned (nothing points to it)
                if (!await UserDataManager.Logic.UserDataManager.IsDeviceExists(udkey))
                {
                    await UserDataManager.Logic.UserDataManager.NewDeviceAsync(udkey);
                }
                // The client is told max_gdkeys; actually enforce it so a device can't create endless saves
                var existing = await UserDataManager.Logic.UserDataManager.GetDeviceGdkeysPublicAsync(udkey);
                if (existing.Count >= DataManager.Logic.DataManager.MaxGdkeysPerDevice)
                {
                    await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new BadL5IDResponse(L5IDErr.INTERNAL_SERVER_ERR, "Too many saves on this device")));
                    return;
                }
                var gdkey = await UserDataManager.Logic.UserDataManager.NewAccountAsync();
                await UserDataManager.Logic.UserDataManager.AddAccountToDevice(udkey, gdkey);
                var res = new CreateGDKeyGoodResponse(gdkey);
                var json = JsonConvert.SerializeObject(res);
                await ctx.Response.WriteAsync(json);
            }
        }
    }
}
