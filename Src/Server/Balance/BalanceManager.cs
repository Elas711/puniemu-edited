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
        // median total stats per rank in the original data, and the top one
        private static Dictionary<int, int> _totals = new();
        private static int _top;
        private static Dictionary<long, int> _rankOf = new();
        public static int RankOf(long youkaiId) => _rankOf.TryGetValue(youkaiId, out var r) ? r : 0;

        public static double FactorFor(int rank, double strength) =>
            _totals.TryGetValue(rank, out var t) && t > 0 ? (t + Math.Clamp(strength, 0, 1) * (_top - t)) / t : 1.0;

        public static void Initialize(double strength)
        {
            Strength = Math.Clamp(strength, 0, 1);
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            if (!cache.TryGetValue(TABLE, out var raw)) return;
            var rows = EventManager.ReadPipeRows(raw, out var wrap);
            static int I(string[] r, int c) => r.Length > c && int.TryParse(r[c], out var v) ? v : 0;
            _rankOf = rows.Where(r => r.Length > RANK && long.TryParse(r[0], out _)).GroupBy(r => long.Parse(r[0])).ToDictionary(g => g.Key, g => I(g.First(), RANK));

            // median total per rank
            var totals = rows.Where(r => r.Length > MAX_ATK && I(r, RANK) > 0 && I(r, MAX_HP) > 0)
                             .GroupBy(r => I(r, RANK))
                             .ToDictionary(g => g.Key, g => g.Select(r => I(r, MAX_HP) + I(r, MAX_ATK)).OrderBy(x => x).ElementAt(g.Count() / 2));
            if (totals.Count == 0) return;
            var top = totals.Values.Max();
            _totals = totals; _top = top;
            if (Strength <= 0) return;
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

    // Yo-kai a player already owns keep the stats they had when they were befriended (levels only
    // add to them), so at login they are scaled from the strength they were last adjusted to
    // (stored with the save) to the current one. Works both ways, keeps limit-break bonuses.
    public static class OwnedStats
    {
        private const string MARKER = "puniemu_balance";

        // New saves get their Yo-kai from the (balanced) master data already.
        public static Task MarkCurrentAsync(string gdkey) =>
            UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, MARKER, new Dictionary<string, double> { ["strength"] = BalanceManager.Strength });

        public static async Task SyncAsync(string gdkey)
        {
            try
            {
                double before = 0;
                var marker = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<Dictionary<string, double>>(gdkey, MARKER);
                if (marker != null && marker.TryGetValue("strength", out var b)) before = b;
                var now = BalanceManager.Strength;
                if (Math.Abs(before - now) < 0.0001) return;
                var raw = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(gdkey, "ywp_user_youkai");
                if (raw == null) return;
                var table = new Puniemu.Src.TableParser.Logic.TableParser<Puniemu.Src.TableParser.DataClasses.YwpUserYoukai>(raw);
                int n = 0;
                foreach (var y in table.Items)
                {
                    var rank = BalanceManager.RankOf(y.YoukaiId);
                    var ratio = BalanceManager.FactorFor(rank, now) / BalanceManager.FactorFor(rank, before);
                    if (Math.Abs(ratio - 1) < 0.0001) continue;
                    y.Hp = (int)Math.Round(y.Hp * ratio);
                    y.Atk = (int)Math.Round(y.Atk * ratio);
                    n++;
                }
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_youkai", table.ToString());
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, MARKER, new Dictionary<string, double> { ["strength"] = now });
                if (n > 0) Log.Info("balance", $"{gdkey}: {n} owned Yo-kai adjusted from strength {before:0.##} to {now:0.##}");
            }
            catch (Exception ex)
            {
                Log.Warn("balance", $"Could not adjust the Yo-kai of {gdkey}: {ex.Message}");
            }
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
