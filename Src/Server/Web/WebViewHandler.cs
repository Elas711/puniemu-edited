using System.Net;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Web
{
    // In-game web views (notice pages). Based on the WebView feature from the BobFlar fork, with the
    // page id restricted to safe characters so it can't be used to read files outside the Web folder.
    public static class WebViewHandler
    {
        private static readonly Regex SAFE_ID = new("^[A-Za-z0-9_\\-]{1,64}$", RegexOptions.Compiled);

        private static string PageRoot => Path.Combine(DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.WebPath), "Page");

        public static async Task HandleNoticeBannerAsync(HttpContext ctx)
        {
            var filePath = Path.Combine(PageRoot, "NoticeBannerPage", "noticeBannerPage.html");
            ctx.Response.ContentType = "text/html; charset=utf-8";
            if (File.Exists(filePath))
            {
                var html = await File.ReadAllTextAsync(filePath);
                await ctx.Response.WriteAsync(ApplyPlaceholders(html, ctx));
            }
            else
            {
                await ctx.Response.WriteAsync(DefaultNoticePage(ctx));
            }
        }

        public static async Task HandleViewPageAsync(HttpContext ctx)
        {
            string webviewId = ctx.Request.Query["webviewId"].ToString();
            string pageNo = ctx.Request.Query["pageNo"].ToString();
            ctx.Response.ContentType = "text/html; charset=utf-8";

            if (string.IsNullOrEmpty(webviewId))
            {
                await ctx.Response.WriteAsync(DefaultNoticePage(ctx));
                return;
            }
            if (!SAFE_ID.IsMatch(webviewId) || (!string.IsNullOrEmpty(pageNo) && !int.TryParse(pageNo, out _)))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Invalid page id");
                return;
            }

            string folderPath = Path.Combine(PageRoot, webviewId);
            string fileName = string.IsNullOrEmpty(pageNo) || pageNo == "1" ? "viewpage.html" : $"viewpage{int.Parse(pageNo)}.html";
            string filePath = Path.Combine(folderPath, fileName);

            if (File.Exists(filePath))
            {
                var html = await File.ReadAllTextAsync(filePath);
                await ctx.Response.WriteAsync(ApplyPlaceholders(html, ctx));
            }
            else
            {
                await ctx.Response.WriteAsync(DefaultNoticePage(ctx));
            }
        }

        // {{SERVER_NAME}}, {{MOTD}} and {{PORTAL_URL}} can be used inside the HTML files.
        private static string ApplyPlaceholders(string html, HttpContext ctx)
        {
            return html
                .Replace("{{SERVER_NAME}}", WebUtility.HtmlEncode(DataManager.Logic.DataManager.ServerName ?? "Puniemu"))
                .Replace("{{MOTD}}", WebUtility.HtmlEncode(Infrastructure.ServerStateStore.Current.Motd).Replace("\n", "<br>"))
                .Replace("{{PORTAL_URL}}", "/web/portal.nhn?userId=" + WebUtility.UrlEncode(ctx.Request.Query["userId"].ToString()));
        }

        private static string DefaultNoticePage(HttpContext ctx)
        {
            var name = WebUtility.HtmlEncode(DataManager.Logic.DataManager.ServerName ?? "Puniemu");
            var motd = WebUtility.HtmlEncode(Infrastructure.ServerStateStore.Current.Motd).Replace("\n", "<br>");
            var events = Events.EventManager.DescribeActiveEvents(null);
            var eventHtml = events.Count == 0 ? "<li>No special events right now.</li>" :
                string.Join("", events.Select(e => $"<li>{WebUtility.HtmlEncode(e)}</li>"));
            return $@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>{name}</title><style>
body{{margin:0;font-family:sans-serif;background:#fff7e6;color:#3b2a1a}}
header{{background:#e8590c;color:#fff;padding:14px 16px;font-weight:bold;font-size:18px}}
main{{padding:16px}} .card{{background:#fff;border-radius:12px;padding:14px;margin-bottom:12px;box-shadow:0 1px 4px rgba(0,0,0,.12)}}
h2{{margin:0 0 8px;font-size:16px;color:#e8590c}}</style></head>
<body><header>{name}</header><main>
<div class=""card""><h2>News</h2><p>{motd}</p></div>
<div class=""card""><h2>Events</h2><ul>{eventHtml}</ul>
<p>Open <b>Help</b> in the game menu and tap <b>Events &amp; Server</b> to choose which events you want to play,
or <b>Event Guide</b> to read about every event of the game.</p></div>
</main></body></html>";
        }
    }
}
