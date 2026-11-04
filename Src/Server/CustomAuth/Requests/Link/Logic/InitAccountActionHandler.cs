using Puniemu.Src.Server.GameServer.Requests.GameEnd;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;

namespace Puniemu.Src.Server.CustomAuth.Requests.Link.Logic
{
    public class InitAccountActionHandler
    {
        public static async Task HandleAsync(HttpContext ctx, bool isLink)
        {
            ctx.Response.ContentType = "application/json";

            var userid = ctx.Request.Query["userId"].ToString();
            var email = ctx.Request.Query["email"].ToString();

            if (string.IsNullOrEmpty(userid) || string.IsNullOrEmpty(email) || !AuthManager.IsValidEmail(email))
            {
                ctx.Response.StatusCode = 400;
                return;
            }
            if (string.IsNullOrEmpty(DataManager.Logic.DataManager.EmailForAuthMessages) || string.IsNullOrEmpty(DataManager.Logic.DataManager.AppPasswordForAuthMessages))
            {
                Puniemu.Src.Server.Infrastructure.Log.Warn("auth", "Account linking requested but EmailForAuthMessages / AppPasswordForAuthMessages are not configured");
                ctx.Response.StatusCode = 503;
                return;
            }

            var gdkey = await UserDataManager.Logic.UserDataManager.GetGdkeyFromUserId(userid);

            if (gdkey == string.Empty)
            {
                ctx.Response.StatusCode = 500;
                return;
            }

            var acc = await UserDataManager.Logic.UserDataManager.GetAccountFromGdkeyAsync(gdkey);

            if (acc == null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            var udkey = acc.Udkey;

            // Generate the code
            int code = AuthManager.NewCode();

            try
            {
                await AuthManager.SendCodeEmail(email, code);
            }
            catch (Exception ex)
            {
                Puniemu.Src.Server.Infrastructure.Log.Error("auth", $"Could not send the code e-mail: {ex.Message}");
                ctx.Response.StatusCode = 502;
                return;
            }

            // Store for 15 minutes
            AuthManager.CodeCache[code] = (
                email,
                isLink,
                udkey,
                DateTime.UtcNow.AddMinutes(15)
            );

            ctx.Response.StatusCode = 200;
        }      
    }
}