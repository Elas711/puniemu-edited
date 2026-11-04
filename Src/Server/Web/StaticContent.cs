using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Puniemu.Src.Server.Infrastructure;
using System.Net;

namespace Puniemu.Src.Server.Web
{
    // Serves the game's data download (asset files) and the in-game web pages directly from Puniemu,
    // so no separate file host is required.
    public static class StaticContent
    {
        private static Dictionary<string, string>? _fileIndex;

        public static FileExtensionContentTypeProvider CreateContentTypeProvider()
        {
            var provider = new FileExtensionContentTypeProvider();
            // Custom game extensions (otherwise ASP.NET refuses to serve them)
            foreach (var ext in new[] { ".ez", ".ayd", ".dat", ".ojk", ".pfa", ".strings", ".ejs", ".pmd", ".fontsize", ".bin", ".xi", ".cfg", ".lua", ".unity3d", ".assetbundle" })
                provider.Mappings[ext] = "application/octet-stream";
            provider.Mappings[".m4"] = "audio/mp4";
            provider.Mappings[".m4a"] = "audio/mp4";
            provider.Mappings[".og"] = "audio/ogg";
            provider.Mappings[".plist"] = "text/xml";
            return provider;
        }

        public static void Configure(WebApplication app)
        {
            var provider = CreateContentTypeProvider();

            // wwwroot (layout used by most guides: wwwroot/dataDownload/...)
            var wwwroot = DataManager.Logic.DataManager.ResolvePath("wwwroot");
            if (Directory.Exists(wwwroot))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(wwwroot),
                    ContentTypeProvider = provider,
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream"
                });
            }

            // Explicit data download folder, served under /dataDownload
            var dd = DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.DataDownloadPath);
            if (!Directory.Exists(dd)) dd = DataManager.Logic.DataManager.ResolvePath("dataDownload");
            if (DataManager.Logic.DataManager.ServeDataDownload && Directory.Exists(dd))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(dd),
                    RequestPath = "/dataDownload",
                    ContentTypeProvider = provider,
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream"
                });

                // Fallback: the client sometimes requests assets with a path layout that differs from the
                // folder structure on disk, so index every file by name and serve by file name.
                _fileIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.EnumerateFiles(dd, "*", SearchOption.AllDirectories))
                    _fileIndex.TryAdd(Path.GetFileName(file), file);
                Log.Info("download", $"Serving {_fileIndex.Count} data download files from {dd}");

                app.Use(async (ctx, next) =>
                {
                    var path = ctx.Request.Path.Value;
                    if (_fileIndex != null && HttpMethods.IsGet(ctx.Request.Method) && path != null &&
                        !path.Contains(".nhn") && !path.StartsWith("/admin") && !path.StartsWith("/web/"))
                    {
                        var name = Path.GetFileName(path.TrimEnd('/'));
                        if (name.Length > 0 && _fileIndex.TryGetValue(name, out var full))
                        {
                            if (!provider.TryGetContentType(full, out var ct)) ct = "application/octet-stream";
                            ctx.Response.ContentType = ct;
                            await ctx.Response.SendFileAsync(full);
                            return;
                        }
                    }
                    await next();
                });
            }
            else if (DataManager.Logic.DataManager.ServeDataDownload)
            {
                Log.Warn("download", $"No data download folder found ({dd}). Assets must be hosted elsewhere (DataDownloadURL).");
            }

            // In-game web pages and their images/css/js under /web/static
            var web = DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.WebPath);
            if (Directory.Exists(web))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(web),
                    RequestPath = "/web/static",
                    ContentTypeProvider = provider
                });
            }
        }

        // help.html ships with the data download; look in the usual places.
        public static string? FindHelpPage()
        {
            var dm = DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.DataDownloadPath);
            var candidates = new[]
            {
                Path.Combine(dm, "help.html"),
                DataManager.Logic.DataManager.ResolvePath("dataDownload/help.html"),
                DataManager.Logic.DataManager.ResolvePath("wwwroot/dataDownload/help.html"),
                DataManager.Logic.DataManager.ResolvePath("wwwroot/eal/help.html"),
                DataManager.Logic.DataManager.ResolvePath(Path.Combine(DataManager.Logic.DataManager.WebPath, "help.html"))
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        public const string FallbackHelpPage = @"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Help</title><style>body{font-family:sans-serif;background:#fff7e6;color:#333;padding:16px}h1{color:#e8590c}</style></head>
<body><h1>Help</h1><p>The help page (help.html) was not found on this server.</p>
<p>Ask the server owner to copy <b>help.html</b> from the data download into the <b>dataDownload</b> folder.</p></body></html>";

        // Floating button added to the in-game Help page that opens the Puniemu player portal.
        public static string PortalButton(string userId)
        {
            var href = "/web/portal.nhn?userId=" + WebUtility.UrlEncode(userId ?? "");
            return $@"
<a href=""{WebUtility.HtmlEncode(href)}"" id=""puniemu-portal-btn"" style=""position:fixed;right:12px;bottom:12px;z-index:99999;
background:#e8590c;color:#fff;font:bold 15px sans-serif;padding:12px 16px;border-radius:24px;text-decoration:none;
box-shadow:0 2px 8px rgba(0,0,0,.3)"">&#9733; Events &amp; Server</a>";
        }
    }
}
