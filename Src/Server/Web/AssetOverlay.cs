using Puniemu.Src.Server.Infrastructure;

namespace Puniemu.Src.Server.Web
{
    // Replaces data download files (images, sounds, effects...) with your own versions, for example
    // the English images of the YWP-Translation project (github.com/Lit8tech/YWP-Translation).
    //
    // Put the files in AssetOverlay/ (any folder layout - files are matched by name). At startup:
    //   * ywp_mst_version_resource gets a new version for every overlaid file (and its real size),
    //     so clients download it again,
    //   * the version of that table (ywp_mst_version_master) and mstVersionMaster are raised, so
    //     clients that already have the data notice the change,
    //   * requests for those files are answered with the overlay copy.
    // This only works when the client downloads its assets from this server (ServeDataDownload).
    public static class AssetOverlay
    {
        public const string OVERLAY_VERSION = "20991231";
        private static readonly string[] VERSION_TABLES = { "ywp_mst_version_resource", "ywp_mst_version_resource_unchecked" };

        private static Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public static int Count => _files.Count;
        public static int Applied { get; private set; }

        public static bool TryGet(string fileName, out string path) => _files.TryGetValue(fileName, out path!);

        public static void Initialize()
        {
            var dir = DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.AssetOverlayPath);
            if (!Directory.Exists(dir)) return;

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DateTime newest = DateTime.MinValue;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)) continue;
                var name = Path.GetFileName(f);
                if (name.StartsWith('.') || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                if (files.TryAdd(name, f))
                {
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > newest) newest = t;
                }
            }

            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            int applied = 0;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in VERSION_TABLES)
            {
                if (!cache.TryGetValue(table, out var raw)) continue;
                var rows = Events.EventManager.ReadPipeRows(raw, out var wrap);
                if (rows.Count == 0) continue;
                bool changed = false;
                foreach (var r in rows)
                {
                    // id | file name | folder | version | ? | app version | size
                    if (r.Length < 4 || !files.TryGetValue(r[1], out var path)) continue;
                    r[3] = OVERLAY_VERSION;
                    if (r.Length > 6 && r[6].Length > 0) r[6] = new FileInfo(path).Length.ToString();
                    used.Add(r[1]);
                    changed = true;
                    if (table == VERSION_TABLES[0]) applied++;
                }
                if (!changed) continue;
                var text = string.Join("*", rows.Select(x => string.Join("|", x)));
                if (wrap != null)
                {
                    wrap["tableData"] = text;
                    cache[table] = wrap.ToString(Newtonsoft.Json.Formatting.None);
                }
                else cache[table] = text;
            }

            if (applied > 0)
            {
                // Stable across restarts, grows whenever newer files are added to the overlay
                int bump = 1 + (int)Math.Max(0, (newest - new DateTime(2020, 1, 1)).TotalDays);
                BumpTableVersion(cache, "ywp_mst_version_resource", bump);
                if (cache.TryGetValue("mstVersionMaster", out var mv) && int.TryParse(mv.Trim(), out var v))
                    cache["mstVersionMaster"] = (v + bump).ToString();
            }

            _files = files.Where(kv => used.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            Applied = applied;
            Log.Info("overlay", $"Asset overlay: {files.Count} files found, {applied} replace data download files");
        }

        private static void BumpTableVersion(System.Collections.Concurrent.ConcurrentDictionary<string, string> cache, string table, int bump)
        {
            if (!cache.TryGetValue("ywp_mst_version_master", out var raw)) return;
            var rows = Events.EventManager.ReadPipeRows(raw, out var wrap);
            foreach (var r in rows)
                if (r.Length >= 2 && r[0] == table && int.TryParse(r[1], out var v)) r[1] = (v + bump).ToString();
            var text = string.Join("*", rows.Select(x => string.Join("|", x)));
            if (wrap != null)
            {
                wrap["tableData"] = text;
                cache["ywp_mst_version_master"] = wrap.ToString(Newtonsoft.Json.Formatting.None);
            }
            else cache["ywp_mst_version_master"] = text;
        }
    }
}
