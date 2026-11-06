using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Events;
using Puniemu.Src.Server.Infrastructure;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Puniemu.Src.Server.Balance
{
    // Optional rebalancing of the Yo-kai ("BalanceMode" + "BalanceStrength" in appsettings.json).
    //
    // In the master data every Yo-kai of a rank has the same total stats (max HP + max ATK: all Z
    // 1933, all UZ 2900, all UZ+ 3330). The power creep comes from the ranks added over the years:
    // the top rank was SSS before 2019, Z in 2019, ZZ 2020, ZZZ 2021-22, UZ 2023-24, UZ+ 2025,
    // UZ++ 2026. Old favourites keep the low ranks of their time.
    //
    //  * "rerank" (recommended): every Yo-kai gets the rank its old rank stands for on today's
    //    ladder - its place on the ladder of its release year, stretched to E..UZ++ - and the stats
    //    of that rank. An old SSS (top of its time) climbs high, an old D stays low.
    //    BalanceStrength blends: 0 = original rank, 1 = fully re-ranked (0.5: S -> SSS, SSS -> ZZZ).
    //  * "stats": ranks stay, stats of every rank move toward the top rank's (0.5 closes half the gap).
    //
    // HP and ATK keep their ratio; enemies (separate tables) are not touched.
    public static class BalanceManager
    {
        private const string TABLE = "ywp_mst_youkai";
        private const int RANK = 3, BASE_HP = 8, MAX_HP = 9, BASE_ATK = 10, MAX_ATK = 11;
        private const int TOP_RANK = 14;
        // top rank of the Yo-kai released before release dates were recorded (2015-2018)
        private const int UNDATED_ERA_TOP = 8;

        public static string Mode { get; private set; } = "off";
        public static double Strength { get; private set; }
        public static Dictionary<int, double> Factors { get; private set; } = new();   // per original rank, for display
        public static Dictionary<string, int> RankMoves { get; private set; } = new(); // "S->SSS": count

        private static Dictionary<int, int> _totals = new();
        private static int _top;
        private static readonly Dictionary<long, (int rank, int eraTop)> _info = new();

        // ---- skills: attack power grows with the rank too (median max power: S 100, Z 123, UZ 220,
        // UZ++ 317), so a re-ranked Yo-kai's attack skill gets the power of its new rank.
        private const string SKILL_LEVEL = "ywp_mst_youkai_skill_level";
        private const int SK_ID = 0, SK_LEVEL = 1, SK_TEXT = 2, SK_VALUE = 4, SK_SCALE = 11;
        private static readonly string[] POWER_LABELS = { "いりょく", "玉のいりょく", "目安のいりょく" };
        private static readonly HashSet<string> _powerRows = new();      // "skillId|level" of attack skills
        private static readonly Dictionary<int, int> _skillPower = new(); // rank -> typical max power
        public static Dictionary<string, int> SkillsScaled { get; private set; } = new();

        // Called before the translation overlay: finds the attack skills by their Japanese label.
        public static void Prepare()
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            if (!cache.TryGetValue(SKILL_LEVEL, out var raw) || !cache.TryGetValue(TABLE, out var yraw)) return;
            var rank = new Dictionary<string, int>();
            foreach (var r in EventManager.ReadPipeRows(yraw, out _))
                if (r.Length > RANK && int.TryParse(r[RANK], out var k) && k > 0) rank.TryAdd(r[0], k);
            var maxBySkill = new Dictionary<string, (int level, int power)>();
            _powerRows.Clear();
            foreach (var r in EventManager.ReadPipeRows(raw, out _))
            {
                if (r.Length <= SK_SCALE) continue;
                var t = r[SK_TEXT];
                var label = t.Split(':', '：')[0].Trim();
                if (!POWER_LABELS.Contains(label)) continue;
                _powerRows.Add(r[SK_ID] + "|" + r[SK_LEVEL]);
                var m = System.Text.RegularExpressions.Regex.Match(t, @"\d+");
                if (m.Success && int.TryParse(r[SK_LEVEL], out var lv) && (!maxBySkill.TryGetValue(r[SK_ID], out var cur) || lv > cur.level))
                    maxBySkill[r[SK_ID]] = (lv, int.Parse(m.Value));
            }
            _skillPower.Clear();
            int run = 0;
            foreach (var g in maxBySkill.Where(kv => rank.ContainsKey(kv.Key)).GroupBy(kv => rank[kv.Key]).OrderBy(g => g.Key))
            {
                var v = g.Select(kv => kv.Value.power).OrderBy(x => x).ElementAt(g.Count() / 2);
                run = Math.Max(run, v); // few low-rank samples: keep the curve rising
                _skillPower[g.Key] = run;
            }
        }

        private static double Power(int rank)
        {
            if (_skillPower.TryGetValue(rank, out var p)) return p;
            var lower = _skillPower.Where(kv => kv.Key <= rank).OrderByDescending(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault();
            return lower;
        }

        // skill power multiplier of one Yo-kai
        public static double SkillFactorFor(long youkaiId, string mode, double strength)
        {
            if (!_info.TryGetValue(youkaiId, out var i) || Power(i.rank) <= 0) return 1.0;
            strength = Math.Clamp(strength, 0, 1);
            if (mode == "rerank")
            {
                var target = Power(NewRank(i.rank, i.eraTop, mode, strength));
                return target > 0 ? Math.Max(1.0, target / Power(i.rank)) : 1.0;
            }
            if (mode == "stats")
            {
                var top = _skillPower.Count > 0 ? _skillPower.Values.Max() : 0;
                return top > 0 ? (Power(i.rank) + strength * (top - Power(i.rank))) / Power(i.rank) : 1.0;
            }
            return 1.0;
        }

        private static void ScaleSkills(ConcurrentDictionary<string, string> cache)
        {
            if (_powerRows.Count == 0 || !cache.TryGetValue(SKILL_LEVEL, out var raw)) return;
            var rows = EventManager.ReadPipeRows(raw, out var wrap);
            var number = new System.Text.RegularExpressions.Regex(@"\d+");
            var scaled = new HashSet<string>();
            var byRank = new Dictionary<string, int>();
            foreach (var r in rows)
            {
                if (r.Length <= SK_SCALE || !_powerRows.Contains(r[SK_ID] + "|" + r[SK_LEVEL]) || !long.TryParse(r[SK_ID], out var id)) continue;
                var f = SkillFactorFor(id, Mode, Strength);
                if (Math.Abs(f - 1) < 0.0001) continue;
                // shown power (first number, whatever the language) and the scale values the battle uses
                r[SK_TEXT] = number.Replace(r[SK_TEXT], m => ((int)Math.Round(int.Parse(m.Value) * f)).ToString(), 1);
                // skills without a scale list keep their power in column 4 (x10 of the shown points)
                if (!r[SK_SCALE].Contains("scale") && int.TryParse(r[SK_VALUE], out var val) && val > 0)
                    r[SK_VALUE] = ((int)Math.Round(val * f)).ToString();
                r[SK_SCALE] = string.Join(",", r[SK_SCALE].Split(',').Select(kv =>
                {
                    var p = kv.Split(':');
                    return p.Length == 2 && p[0].StartsWith("scale") && p[0] != "scale0" && int.TryParse(p[1], out var v)
                        ? p[0] + ":" + (int)Math.Round(v * f) : kv;
                }));
                if (scaled.Add(r[SK_ID]))
                {
                    var key = "x" + Math.Round(f, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    byRank[key] = byRank.GetValueOrDefault(key) + 1;
                }
            }
            if (scaled.Count == 0) return;
            SkillsScaled = byRank.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            var text = string.Join("*", rows.Select(x => string.Join("|", x)));
            if (wrap != null) { wrap["tableData"] = text; cache[SKILL_LEVEL] = wrap.ToString(Newtonsoft.Json.Formatting.None); }
            else cache[SKILL_LEVEL] = text;
            MasterVersions.MarkChanged(cache, SKILL_LEVEL);
            Log.Info("balance", $"{scaled.Count} attack skills scaled with the new ranks");
        }

        public static int RankOf(long youkaiId) => _info.TryGetValue(youkaiId, out var i) ? i.rank : 0;

        public static int NewRank(int rank, int eraTop, string mode, double strength)
        {
            if (mode != "rerank" || rank <= 0 || eraTop <= 1) return rank;
            double era = 1 + (rank - 1) * (TOP_RANK - 1.0) / (eraTop - 1);
            var r = (int)Math.Round(rank + Math.Clamp(strength, 0, 1) * (era - rank));
            return Math.Clamp(r, rank, TOP_RANK);
        }

        private static double Total(int rank) => _totals.TryGetValue(rank, out var t) ? t : 0;

        // stats multiplier of one Yo-kai under a mode / strength
        public static double FactorFor(long youkaiId, string mode, double strength)
        {
            if (!_info.TryGetValue(youkaiId, out var i) || Total(i.rank) <= 0) return 1.0;
            strength = Math.Clamp(strength, 0, 1);
            if (mode == "rerank")
            {
                var target = Total(NewRank(i.rank, i.eraTop, mode, strength));
                return target > 0 ? target / Total(i.rank) : 1.0;
            }
            if (mode == "stats") return (Total(i.rank) + strength * (_top - Total(i.rank))) / Total(i.rank);
            return 1.0;
        }

        public static void Initialize(string mode, double strength)
        {
            Mode = mode is "rerank" or "stats" ? mode : "off";
            Strength = Math.Clamp(strength, 0, 1);
            if (Mode == "off") Strength = 0;
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            if (!cache.TryGetValue(TABLE, out var raw)) return;
            var rows = EventManager.ReadPipeRows(raw, out var wrap);
            static int I(string[] r, int c) => r.Length > c && int.TryParse(r[c], out var v) ? v : 0;
            static int Year(string[] r)
            {
                var d = r.FirstOrDefault(c => c.Length >= 19 && c[4] == '-' && c[7] == '-' && c[10] == ' ');
                return d != null && int.TryParse(d[..4], out var y) ? y : 0;
            }

            var totals = rows.Where(r => r.Length > MAX_ATK && I(r, RANK) > 0 && I(r, MAX_HP) > 0)
                             .GroupBy(r => I(r, RANK))
                             .ToDictionary(g => g.Key, g => g.Select(r => I(r, MAX_HP) + I(r, MAX_ATK)).OrderBy(x => x).ElementAt(g.Count() / 2));
            if (totals.Count == 0) return;
            _totals = totals; _top = totals.Values.Max();

            // top rank of each release year (and every year before it)
            var topByYear = rows.Where(r => Year(r) > 0 && I(r, RANK) > 0).GroupBy(Year).ToDictionary(g => g.Key, g => g.Max(r => I(r, RANK)));
            int EraTop(int year)
            {
                if (year == 0) return UNDATED_ERA_TOP;
                return Math.Max(UNDATED_ERA_TOP, topByYear.Where(kv => kv.Key <= year).Select(kv => kv.Value).DefaultIfEmpty(UNDATED_ERA_TOP).Max());
            }
            _info.Clear();
            foreach (var r in rows)
                if (r.Length > MAX_ATK && long.TryParse(r[0], out var id) && I(r, RANK) > 0)
                    _info.TryAdd(id, (I(r, RANK), EraTop(Year(r))));
            if (Strength <= 0) return;

            Factors = totals.Keys.ToDictionary(k => k, k => Mode == "stats" ? (Total(k) + Strength * (_top - Total(k))) / Total(k) : 1.0);
            var moves = new Dictionary<string, int>();
            var names = new[] { "", "E", "D", "C", "B", "A", "S", "SS", "SSS", "Z", "ZZ", "ZZZ", "UZ", "UZ+", "UZ++" };
            int changed = 0;
            foreach (var r in rows)
            {
                if (r.Length <= MAX_ATK || !long.TryParse(r[0], out var id) || !_info.TryGetValue(id, out var info)) continue;
                var f = FactorFor(id, Mode, Strength);
                if (Mode == "rerank")
                {
                    var nr = NewRank(info.rank, info.eraTop, Mode, Strength);
                    if (nr != info.rank)
                    {
                        r[RANK] = nr.ToString();
                        var key = $"{names[info.rank]}->{names[nr]}";
                        moves[key] = moves.GetValueOrDefault(key) + 1;
                    }
                }
                if (Math.Abs(f - 1) < 0.0001) continue;
                foreach (var c in new[] { BASE_HP, MAX_HP, BASE_ATK, MAX_ATK })
                    if (I(r, c) > 0) r[c] = ((int)Math.Round(I(r, c) * f)).ToString();
                changed++;
            }
            RankMoves = moves.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            var text = string.Join("*", rows.Select(x => string.Join("|", x)));
            if (wrap != null) { wrap["tableData"] = text; cache[TABLE] = wrap.ToString(Newtonsoft.Json.Formatting.None); }
            else cache[TABLE] = text;
            MasterVersions.MarkChanged(cache, TABLE);
            ScaleSkills(cache);
            Log.Info("balance", $"BalanceMode {Mode}, strength {Strength:0.##}: {changed} Yo-kai changed" +
                (Mode == "rerank" ? "; rank moves: " + string.Join(", ", RankMoves.Take(12).Select(kv => $"{kv.Key} x{kv.Value}")) : ""));
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
            UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, MARKER, Current());

        private static Dictionary<string, double> Current() => new()
        {
            ["strength"] = BalanceManager.Strength,
            ["mode"] = BalanceManager.Mode == "rerank" ? 2 : BalanceManager.Mode == "stats" ? 1 : 0
        };

        private static string ModeName(double code) => code >= 1.5 ? "rerank" : code >= 0.5 ? "stats" : "off";

        public static async Task SyncAsync(string gdkey)
        {
            try
            {
                double before = 0;
                string beforeMode = "off";
                var marker = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<Dictionary<string, double>>(gdkey, MARKER);
                if (marker != null && marker.TryGetValue("strength", out var bs)) before = bs;
                // saves marked before modes existed used the "stats" balance
                if (marker != null) beforeMode = marker.TryGetValue("mode", out var bm) ? ModeName(bm) : (before > 0 ? "stats" : "off");
                var now = BalanceManager.Strength;
                var nowMode = BalanceManager.Mode;
                if (Math.Abs(before - now) < 0.0001 && (beforeMode == nowMode || now == 0 && before == 0)) return;
                var raw = await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<string>(gdkey, "ywp_user_youkai");
                if (raw == null) return;
                var table = new Puniemu.Src.TableParser.Logic.TableParser<Puniemu.Src.TableParser.DataClasses.YwpUserYoukai>(raw);
                int n = 0;
                foreach (var y in table.Items)
                {
                    var ratio = BalanceManager.FactorFor(y.YoukaiId, nowMode, now) / BalanceManager.FactorFor(y.YoukaiId, beforeMode, before);
                    if (Math.Abs(ratio - 1) < 0.0001) continue;
                    y.Hp = (int)Math.Round(y.Hp * ratio);
                    y.Atk = (int)Math.Round(y.Atk * ratio);
                    n++;
                }
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, "ywp_user_youkai", table.ToString());
                await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, MARKER, Current());
                if (n > 0) Log.Info("balance", $"{gdkey}: {n} owned Yo-kai adjusted ({beforeMode} {before:0.##} -> {nowMode} {now:0.##})");
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
