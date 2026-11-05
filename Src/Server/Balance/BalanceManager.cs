using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Events;
using Puniemu.Src.Server.Infrastructure;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Puniemu.Src.Server.Balance
{
    // Optional rebalancing of the Yo-kai stats ("BalanceStrength" in appsettings.json, 0 = off).
    //
    // In the master data every Yo-kai of a rank has the same total stats (max HP + max ATK): all Z
    // are 1933, all UZ 2900, all UZ+ 3330. The power creep comes from new, higher ranks (ZZ 2020,
    // ZZZ 2021, UZ 2023, UZ+ 2025), so old favourites are stuck far below. BalanceStrength moves
    // every rank toward the top rank: 0.5 closes half of the gap, 1 gives every Yo-kai the stats of
    // the top rank. HP and ATK keep their ratio; enemies (separate tables) are not touched.
    public static class BalanceManager
    {
        private const string TABLE = "ywp_mst_youkai";
        private const int RANK = 3, BASE_HP = 8, MAX_HP = 9, BASE_ATK = 10, MAX_ATK = 11;

        public static double Strength { get; private set; }
        public static Dictionary<int, double> Factors { get; private set; } = new();

        public static void Initialize(double strength)
        {
            Strength = Math.Clamp(strength, 0, 1);
            if (Strength <= 0) return;
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            if (!cache.TryGetValue(TABLE, out var raw)) return;
            var rows = EventManager.ReadPipeRows(raw, out var wrap);
            static int I(string[] r, int c) => r.Length > c && int.TryParse(r[c], out var v) ? v : 0;

            // median total per rank
            var totals = rows.Where(r => r.Length > MAX_ATK && I(r, RANK) > 0 && I(r, MAX_HP) > 0)
                             .GroupBy(r => I(r, RANK))
                             .ToDictionary(g => g.Key, g => g.Select(r => I(r, MAX_HP) + I(r, MAX_ATK)).OrderBy(x => x).ElementAt(g.Count() / 2));
            if (totals.Count == 0) return;
            var top = totals.Values.Max();
            Factors = totals.ToDictionary(kv => kv.Key, kv => (kv.Value + Strength * (top - kv.Value)) / kv.Value);

            int changed = 0;
            foreach (var r in rows)
            {
                if (r.Length <= MAX_ATK || !Factors.TryGetValue(I(r, RANK), out var f) || f <= 1.0001) continue;
                foreach (var c in new[] { BASE_HP, MAX_HP, BASE_ATK, MAX_ATK })
                    if (I(r, c) > 0) r[c] = ((int)Math.Round(I(r, c) * f)).ToString();
                changed++;
            }
            var text = string.Join("*", rows.Select(x => string.Join("|", x)));
            if (wrap != null) { wrap["tableData"] = text; cache[TABLE] = wrap.ToString(Newtonsoft.Json.Formatting.None); }
            else cache[TABLE] = text;
            MasterVersions.MarkChanged(cache, TABLE);
            Log.Info("balance", $"BalanceStrength {Strength:0.##}: stats of {changed} Yo-kai raised (" +
                string.Join(", ", Factors.OrderBy(k => k.Key).Where(k => k.Value > 1.0001).Select(k => $"rank {k.Key} x{k.Value:0.00}")) + ")");
        }
    }

    // Clients keep the master tables cached and download them again when the server's version
    // (mstVersionMaster, and the table's row in ywp_mst_version_master when the data has one) goes
    // up. Tables the server changes itself (translation, balance) are registered here; after
    // start-up Apply() raises the version by one whenever their content differs from the last run
    // (kept in master_version.json next to the server state), so the version only ever grows.
    public static class MasterVersions
    {
        private static readonly HashSet<string> _changed = new();
        private class Saved { public string Signature { get; set; } = ""; public int Extra { get; set; } }

        public static void MarkChanged(ConcurrentDictionary<string, string> cache, string table)
        {
            lock (_changed) _changed.Add(table);
        }

        public static void Apply()
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            List<string> tables;
            lock (_changed) tables = _changed.OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (tables.Count == 0 || !cache.TryGetValue("mstVersionMaster", out var mv) || !int.TryParse(mv.Trim(), out var version)) return;
            using var sha = SHA1.Create();
            foreach (var t in tables)
            {
                var b = Encoding.UTF8.GetBytes(t + "\n" + (cache.TryGetValue(t, out var c) ? c : ""));
                sha.TransformBlock(b, 0, b.Length, null, 0);
            }
            var tail = Encoding.UTF8.GetBytes(version.ToString());
            sha.TransformFinalBlock(tail, 0, tail.Length);
            var sig = Convert.ToHexString(sha.Hash!);

            var path = Path.Combine(Path.GetDirectoryName(DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.ServerStatePath)) ?? ".", "master_version.json");
            Saved saved = new();
            try { if (File.Exists(path)) saved = Newtonsoft.Json.JsonConvert.DeserializeObject<Saved>(File.ReadAllText(path)) ?? new(); } catch { }
            if (saved.Signature != sig)
            {
                saved = new Saved { Signature = sig, Extra = saved.Extra + 1 };
                try { File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(saved)); }
                catch (Exception ex) { Log.Warn("master", $"Could not save {path}: {ex.Message}"); }
            }
            cache["mstVersionMaster"] = (version + saved.Extra).ToString();

            // per-table versions, where the data has them
            if (cache.TryGetValue("ywp_mst_version_master", out var raw) && raw.Trim() != "null" && raw.Contains('|'))
            {
                var rows = EventManager.ReadPipeRows(raw, out var wrap);
                foreach (var r in rows)
                    if (r.Length >= 2 && tables.Contains(r[0]) && int.TryParse(r[1], out var v)) r[1] = (v + saved.Extra).ToString();
                var text = string.Join("*", rows.Select(x => string.Join("|", x)));
                if (wrap != null) { wrap["tableData"] = text; cache["ywp_mst_version_master"] = wrap.ToString(Newtonsoft.Json.Formatting.None); }
                else cache["ywp_mst_version_master"] = text;
            }
            Log.Info("master", $"{tables.Count} master tables changed by the server; mstVersionMaster {version} -> {version + saved.Extra}");
        }
    }
}
