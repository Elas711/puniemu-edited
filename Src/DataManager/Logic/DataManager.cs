using System.Data.Common;

namespace Puniemu.Src.DataManager.Logic
{
    public static class DataManager
    {
        public static GameDataManager GameDataManager = new GameDataManager();

        public static string? SupabaseKey { get; private set; }

        public static string? SupabaseURL { get; private set; }

        public static string? PostgresConnectionString { get; private set; }

        // Kestrel cap on simultaneous open connections
        public static int MaxConnections { get; private set; }

        // Account cache ceiling. New players are refused while the cache is at capacity.
        public static int MaxCachedAccounts { get; private set; }

        // If false, the data download will be downloaded from a different server rather than the Supabase storage. Supabase storage method isnt implemented yet
        public static bool IsDataDownloadFromSupabase { get; private set; }

        //0 if the data is retrieved from Supabase
        public static string? DataDownloadURL { get; private set; }

        public static string? GameVersion { get; private set; }

        public static string EmailForAuthMessages { get; private set; } = "";

        public static string AppPasswordForAuthMessages { get; private set; } = "";
        //Can be displayed in text boxes
        public static string? ServerName { get; private set; }

        //Specifies if the current run is meant for WibWob or Puni
        public static bool IsWibWob { get; set; }

        // ---------------------------------------------------------------
        // Extended settings. Every one of these is optional and has a safe default,
        // so an old appsettings.json keeps working unchanged.
        // ---------------------------------------------------------------

        // When false, any client version is accepted (handy when several APK builds are in use).
        public static bool StrictVersionCheck { get; private set; } = true;

        // Extra versions accepted on top of GameVersion (comma separated in config).
        public static HashSet<string> AllowedGameVersions { get; private set; } = new();

        // Public base URL of this server, e.g. "https://my-server.example.com". Used by the
        // HSP launching endpoint and as a fallback for the data download URL.
        public static string? PublicServerURL { get; private set; }

        // Serve the dataDownload folder directly from this server (no separate file host needed).
        public static bool ServeDataDownload { get; private set; } = true;

        // Folder that holds the dataDownload files. Relative paths are resolved against the content root.
        public static string DataDownloadPath { get; private set; } = "wwwroot/dataDownload";

        // Token protecting the admin panel (/admin). The panel is disabled while this is empty.
        public static string? AdminToken { get; private set; }

        // Language overlay applied to the game's master data. "" disables it, "en" loads Translations/en/*.
        public static string Language { get; private set; } = "";

        // Where translation dictionaries live.
        public static string TranslationsPath { get; private set; } = "Translations";

        // Where event packs (Events/*.json) live.
        public static string EventsPath { get; private set; } = "Events";

        // Replacement data download files (e.g. translated images), matched by file name.
        public static string AssetOverlayPath { get; private set; } = "AssetOverlay";

        // Where in-game web pages live (notice pages, event selector, ...).
        public static string WebPath { get; private set; } = "Web";

        // Where runtime state (event selection, bans, rates, maintenance...) is persisted.
        public static string ServerStatePath { get; private set; } = "server_state.json";

        // off | log | enforce: check that the device sending a request owns the save it names.
        public static string AccountOwnershipMode { get; private set; } = "log";

        // Requests per minute allowed per IP address. 0 disables rate limiting.
        public static int RateLimitPerMinute { get; private set; } = 900;

        // Trust X-Forwarded-For / X-Forwarded-Proto (only enable behind a reverse proxy you control).
        public static bool TrustProxyHeaders { get; private set; } = false;

        // New saves skip the opening tutorial (from the Washaaaan fork).
        public static bool SkipOpeningTutorial { get; private set; } = false;

        // Maximum saves that can be created from a single device.
        public static int MaxGdkeysPerDevice { get; private set; } = 3;

        public static void StaticInit(IConfiguration config)
        {
            SupabaseKey = config["SupabaseKey"];
            SupabaseURL = config["SupabaseURL"];
            PostgresConnectionString = config["PostgresConnectionString"];
            MaxConnections = int.TryParse(config["MaxConnections"], out var maxConnections) ? maxConnections : 1500;
            MaxCachedAccounts = int.TryParse(config["MaxCachedAccounts"], out var maxCachedAccounts) ? maxCachedAccounts : 2000;
            GameVersion = config["GameVersion"];
            ServerName = string.IsNullOrWhiteSpace(config["ServerName"]) ? "Puniemu" : config["ServerName"];
            EmailForAuthMessages = config["EmailForAuthMessages"] ?? "";
            AppPasswordForAuthMessages = config["AppPasswordForAuthMessages"] ?? "";
            if (bool.TryParse(config["IsWibWob"], out bool isWib))
            {
                IsWibWob = isWib;
            }
            else throw new InvalidDataException("Please specify 'IsWibWob' as true or false in appsettings.");
            //now check if the data download link is 0. if it is, it means we will draw the data download files from supabase storage
            if (int.TryParse(config["DataDownloadURL"], out int result) && result == 0)
            {
                IsDataDownloadFromSupabase = true;
            }
            else DataDownloadURL = config["DataDownloadURL"];

            StrictVersionCheck = GetBool(config, "StrictVersionCheck", true);
            AllowedGameVersions = (config["AllowedGameVersions"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet();
            PublicServerURL = config["PublicServerURL"]?.TrimEnd('/');
            ServeDataDownload = GetBool(config, "ServeDataDownload", true);
            DataDownloadPath = GetString(config, "DataDownloadPath", DataDownloadPath);
            AdminToken = config["AdminToken"];
            Language = (config["Language"] ?? "").Trim().ToLowerInvariant();
            TranslationsPath = GetString(config, "TranslationsPath", TranslationsPath);
            Puniemu.Src.Server.Translation.TranslationManager.RomajiFallback = GetBool(config, "RomajiFallback", true);
            EventsPath = GetString(config, "EventsPath", EventsPath);
            WebPath = GetString(config, "WebPath", WebPath);
            AssetOverlayPath = GetString(config, "AssetOverlayPath", AssetOverlayPath);
            ServerStatePath = GetString(config, "ServerStatePath", ServerStatePath);
            AccountOwnershipMode = GetString(config, "AccountOwnershipMode", AccountOwnershipMode).ToLowerInvariant();
            RateLimitPerMinute = int.TryParse(config["RateLimitPerMinute"], out var rl) ? rl : RateLimitPerMinute;
            TrustProxyHeaders = GetBool(config, "TrustProxyHeaders", false);
            SkipOpeningTutorial = GetBool(config, "SkipOpeningTutorial", false);
            MaxGdkeysPerDevice = int.TryParse(config["MaxGdkeysPerDevice"], out var mg) && mg > 0 ? mg : MaxGdkeysPerDevice;

            // If no external data download host was configured but a public URL was, point the
            // client at this server's own /dataDownload/ folder.
            if (string.IsNullOrWhiteSpace(DataDownloadURL) && !string.IsNullOrWhiteSpace(PublicServerURL))
            {
                DataDownloadURL = PublicServerURL + "/dataDownload/";
            }
        }

        // Returns true when a client reporting this version may connect.
        public static bool IsVersionAccepted(string? appVer)
        {
            if (!StrictVersionCheck) return true;
            if (appVer == null) return false;
            return appVer == GameVersion || AllowedGameVersions.Contains(appVer);
        }

        // Resolves a configured path against the content root.
        public static string ResolvePath(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.Combine(ContentRoot, path);
        }

        public static string ContentRoot { get; set; } = Directory.GetCurrentDirectory();

        private static bool GetBool(IConfiguration config, string key, bool def)
        {
            return bool.TryParse(config[key], out var b) ? b : def;
        }

        private static string GetString(IConfiguration config, string key, string def)
        {
            var v = config[key];
            return string.IsNullOrWhiteSpace(v) ? def : v;
        }
    }
}
