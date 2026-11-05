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

            // Asset overlay first: replacement files win over the data download
            if (AssetOverlay.Count > 0)
            {
                app.Use(async (ctx, next) =>
                {
                    var path = ctx.Request.Path.Value;
                    if (HttpMethods.IsGet(ctx.Request.Method) && path != null && !path.Contains(".nhn") &&
                        !path.StartsWith("/admin") && !path.StartsWith("/web/") &&
                        AssetOverlay.TryGet(Path.GetFileName(path.TrimEnd('/')), out var file))
                    {
                        if (!provider.TryGetContentType(file, out var ct)) ct = "application/octet-stream";
                        ctx.Response.ContentType = ct;
                        await ctx.Response.SendFileAsync(file);
                        return;
                    }
                    await next();
                });
            }

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
                // The same file name exists in several version folders (1.0.0/20160120/x.png,
                // 4.0.6/20260714/x.png...): keep the newest copy, not whichever is found first.
                _fileIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var ddRoot = Path.GetFullPath(dd).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var file in Directory.EnumerateFiles(dd, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    if (!_fileIndex.TryGetValue(name, out var existing) || CompareVersionPaths(ddRoot, file, existing) > 0)
                        _fileIndex[name] = file;
                }
                Log.Info("download", $"Serving {_fileIndex.Count} data download files from {dd}");

                app.Use(async (ctx, next) =>
                {
                    var path = ctx.Request.Path.Value;
                    if (_fileIndex != null && HttpMethods.IsGet(ctx.Request.Method) && path != null &&
                        !path.Contains(".nhn") && !path.StartsWith("/admin") && !path.StartsWith("/web/"))
                    {
                        // 1. exact location (e.g. /eal/4.0.6/20260714/file.png -> dataDownload/4.0.6/20260714/file.png)
                        string? full = null;
                        var rel = path.Trim('/');
                        foreach (var prefix in new[] { "eal/", "dataDownload/" })
                            if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { rel = rel.Substring(prefix.Length); break; }
                        if (rel.Length > 0)
                        {
                            var candidate = Path.GetFullPath(Path.Combine(ddRoot, rel));
                            if (candidate.StartsWith(ddRoot, StringComparison.Ordinal) && File.Exists(candidate)) full = candidate;
                        }
                        // 2. by file name (newest copy)
                        var name = Path.GetFileName(path.TrimEnd('/'));
                        if (full != null || (name.Length > 0 && _fileIndex.TryGetValue(name, out full)))
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

        private static readonly System.Text.RegularExpressions.Regex SAFE_ASSET = new(@"^[A-Za-z0-9_\-]{1,80}\.(png|jpg|webp)$");

        // Finds a data download picture by file name (asset overlay first). Used by the in-game wiki.
        public static bool TryFindAsset(string name, out string path)
        {
            path = "";
            if (!SAFE_ASSET.IsMatch(name)) return false;
            if (AssetOverlay.TryGet(name, out path)) return true;
            return _fileIndex != null && _fileIndex.TryGetValue(name, out path!);
        }

        // A picture that is a plain file, or a PNG packed in an .ayd file of the same name.
        public static bool HasAsset(string name) =>
            TryFindAsset(name, out _) || (name.EndsWith(".png") && TryFindPacked(name, out _));

        private static bool TryFindPacked(string name, out string aydPath)
        {
            aydPath = "";
            if (!SAFE_ASSET.IsMatch(name) || _fileIndex == null) return false;
            return _fileIndex.TryGetValue(PackedAssets.ContainerName(name), out aydPath!);
        }

        public static async Task HandleAssetAsync(HttpContext ctx)
        {
            var name = (string?)ctx.Request.RouteValues["name"] ?? "";
            if (TryFindAsset(name, out var path))
            {
                ctx.Response.ContentType = name.EndsWith(".png") ? "image/png" : name.EndsWith(".webp") ? "image/webp" : "image/jpeg";
                ctx.Response.Headers["Cache-Control"] = "public, max-age=86400";
                await ctx.Response.SendFileAsync(path);
                return;
            }
            if (name.EndsWith(".png") && TryFindPacked(name, out var ayd) && PackedAssets.Extract(ayd, name) is { } png)
            {
                ctx.Response.ContentType = "image/png";
                ctx.Response.Headers["Cache-Control"] = "public, max-age=86400";
                await ctx.Response.Body.WriteAsync(png);
                return;
            }
            ctx.Response.StatusCode = 404;
        }

        // Orders two copies of a file by the version folders they are in (app version, then date).
        private static int CompareVersionPaths(string root, string a, string b)
        {
            static (Version, long, string) Key(string root, string p)
            {
                var parts = Path.GetRelativePath(root, p).Split(Path.DirectorySeparatorChar);
                var v = parts.Length > 1 && Version.TryParse(parts[0], out var ver) ? ver : new Version(0, 0);
                var d = parts.Length > 2 && long.TryParse(parts[1], out var date) ? date : 0;
                return (v, d, p);
            }
            var ka = Key(root, a); var kb = Key(root, b);
            int c = ka.Item1.CompareTo(kb.Item1);
            if (c != 0) return c;
            c = ka.Item2.CompareTo(kb.Item2);
            return c != 0 ? c : string.CompareOrdinal(ka.Item3, kb.Item3);
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
            var guide = "/web/guide.nhn?userId=" + WebUtility.UrlEncode(userId ?? "");
            return $@"
<div style=""position:fixed;right:12px;bottom:12px;z-index:99999;display:flex;flex-direction:column;gap:8px;align-items:flex-end"">
<a href=""{WebUtility.HtmlEncode("/web/wiki.nhn?userId=" + WebUtility.UrlEncode(userId ?? ""))}"" id=""puniemu-wiki-btn"" style=""background:#1c7ed6;color:#fff;font:bold 15px sans-serif;padding:12px 16px;border-radius:24px;text-decoration:none;
box-shadow:0 2px 8px rgba(0,0,0,.3)"">&#128218; Wiki</a>
<a href=""{WebUtility.HtmlEncode(guide)}"" id=""puniemu-guide-btn"" style=""background:#7048e8;color:#fff;font:bold 15px sans-serif;padding:12px 16px;border-radius:24px;text-decoration:none;
box-shadow:0 2px 8px rgba(0,0,0,.3)"">&#128214; Event Guide</a>
<a href=""{WebUtility.HtmlEncode(href)}"" id=""puniemu-portal-btn"" style=""background:#e8590c;color:#fff;font:bold 15px sans-serif;padding:12px 16px;border-radius:24px;text-decoration:none;
box-shadow:0 2px 8px rgba(0,0,0,.3)"">&#9733; Events &amp; Server</a></div>";
        }
    }
}
