using Newtonsoft.Json;
using Puniemu.Src.Server.Events;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.ExecuteGacha.DataClasses;
using Puniemu.Src.Server.GameServer.Requests.ExecuteGacha.Logic;
using Puniemu.Src.Server.Infrastructure;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Gacha
{
    public class MachineInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public long CoinItemId { get; set; }
        public string CoinName { get; set; } = "";
        public int PriceType { get; set; }  // 1 = Y-Money, 2 = item (coin), 4 = other
        public int Price { get; set; }
        public int Pulls { get; set; } = 1;
        public string Start { get; set; } = "";
        // "exact": the real pool from gacha_pool; "lineup": its pick list; "built": rebuilt from the
        // Yo-kai released when the machine came out
        public string Source { get; set; } = "built";
        public List<long> Featured { get; set; } = new();
        // the machine takes a coin item (not Y Points, which are a currency)
        public bool HasCoin { get; set; }
    }

    // Gives every crank-a-kai of the master data a pool.
    //
    // The official servers keep the drop pools to themselves; gacha_pool only has the few that were
    // captured. For the other machines the lineup is rebuilt from what the master data does show:
    //   * ywp_mst_gacha_youkai_choice - the "pick a Yo-kai" list of a machine (its full lineup),
    //   * the Yo-kai released in the days after the machine came out (collab / event coins: the
    //     Monster Strike coin of 2019-08 gets the Monster Strike characters),
    // put on top of the pool of a machine that costs the same (the regular Yo-kai of each rank).
    // Featured Yo-kai get a rate-up, like the "rate up" banners of the real game.
    //
    // Players can also "mix" machines: their Y-Money crank-a-kai then draws from the merged pools.
    public static class GachaLineup
    {
        private static readonly Regex DATE = new(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d$", RegexOptions.Compiled);
        private const double FEATURED_RATE_UP = 4.0;
        private const int MAX_FEATURED = 120;

        private static readonly object _lock = new();
        private static Dictionary<int, MachineInfo> _machines = new();
        private static bool _built;
        private static readonly ConcurrentDictionary<string, int> _mixIds = new();
        private static readonly ConcurrentDictionary<int, GachaPoolItem> _mixPools = new();
        private static int _nextMixId = -1000;

        public static IReadOnlyDictionary<int, MachineInfo> Machines
        {
            get { GachaPoolManager.EnsureLoaded(); return _machines; }
        }

        public static bool TryGetMixPool(int id, out GachaPoolItem pool) => _mixPools.TryGetValue(id, out pool!);

        // Called by GachaPoolManager.EnsureLoaded once gacha_pool is read.
        public static void EnsureBuilt()
        {
            if (_built) return;
            lock (_lock)
            {
                if (_built) return;
                try { Build(); }
                catch (Exception ex) { Log.Error("gacha", $"Could not build the crank-a-kai lineups: {ex.Message}"); }
                _built = true;
            }
        }

        private static List<string[]> Rows(string table)
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            return cache.TryGetValue(table, out var raw) ? EventManager.ReadPipeRows(raw, out _) : new List<string[]>();
        }

        private static void Build()
        {
            var pools = GachaPoolManager.Gachas;
            var exact = pools.Keys.ToHashSet();
            var itemNames = new Dictionary<string, string>();
            var itemType = new Dictionary<string, string>();
            foreach (var r in Rows("ywp_mst_item"))
                if (r.Length > 2) { itemNames.TryAdd(r[0], r[2]); itemType.TryAdd(r[0], r[1]); }

            // Yo-kai: rank (column 3) and release date (the date column, where there is one)
            var rank = new Dictionary<long, int>();
            var released = new List<(DateTime when, long id)>();
            foreach (var r in Rows("ywp_mst_youkai"))
            {
                if (r.Length < 4 || !long.TryParse(r[0], out var id) || !int.TryParse(r[3], out var rk) || rk <= 0 || rk > 13) continue;
                rank[id] = rk;
                var d = r.FirstOrDefault(c => DATE.IsMatch(c));
                if (d != null && DateTime.TryParseExact(d, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
                    released.Add((when, id));
            }
            released.Sort((a, b) => a.when.CompareTo(b.when));

            var choice = new Dictionary<string, List<long>>();
            foreach (var r in Rows("ywp_mst_gacha_youkai_choice"))
                if (r.Length > 1 && long.TryParse(r[1], out var yid))
                    (choice.TryGetValue(r[0], out var l) ? l : choice[r[0]] = new List<long>()).Add(yid);

            var machines = new Dictionary<int, MachineInfo>();
            var rows = Rows("ywp_mst_gacha");
            string PriceKey(string[] r) => r.Length > 4 ? r[3] + ":" + r[4] : "";
            var priceOf = rows.Where(r => r.Length > 4).GroupBy(r => r[0]).ToDictionary(g => g.Key, g => PriceKey(g.First()));
            var largest = exact.Count == 0 ? -1 : exact.OrderByDescending(k => pools[k].Yokais?.Values.Sum(l => l.Count) ?? 0).First();
            int built = 0, lineups = 0;

            foreach (var r in rows)
            {
                if (r.Length < 12 || !int.TryParse(r[0], out var id) || machines.ContainsKey(id)) continue;
                var info = new MachineInfo
                {
                    Id = id,
                    PriceType = int.TryParse(r[3], out var pt) ? pt : 0,
                    CoinItemId = long.TryParse(r[4], out var coin) ? coin : 0,
                    Price = int.TryParse(r[5], out var pr) ? pr : 0,
                    Pulls = int.TryParse(r[11], out var pulls) && pulls > 0 ? pulls : 1,
                    Start = r[1]
                };
                info.CoinName = itemNames.TryGetValue(r[4], out var cn) ? cn : "";
                info.HasCoin = info.PriceType == 2 && info.CoinItemId > 0 && itemType.TryGetValue(r[4], out var ty) && ty != "81";
                info.Name = r.Length > 14 && r[14].Length > 0 ? r[14]
                          : info.CoinName.Length > 0 ? info.CoinName
                          : info.PriceType == 1 ? "Yo-kai Crank-a-kai" : "Crank-a-kai " + id;
                info.Name = Regex.Replace(info.Name, @"^(?:A|An|The) (?=\S)", "");
                info.CoinName = Regex.Replace(info.CoinName, @"^(?:A|An|The) (?=\S)", "");
                machines[id] = info;

                if (exact.Contains(id))
                {
                    info.Source = "exact";
                    continue;
                }
                if (largest < 0) continue;

                // the machine's own lineup, if it has a pick list
                var featured = new List<long>();
                foreach (var key in new[] { r[0], r.Length > 20 ? r[20] : "" })
                    if (key.Length > 0 && choice.TryGetValue(key, out var l)) featured.AddRange(l);
                info.Source = featured.Count > 0 ? "lineup" : "built";
                // Yo-kai that came out with the machine (3 days before to 3 weeks after it opened)
                if (featured.Count == 0 && DateTime.TryParseExact(r[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var opened)
                    && opened.Year >= 2016)
                {
                    var from = opened.AddDays(-3);
                    var to = opened.AddDays(21);
                    featured.AddRange(released.Where(x => x.when >= from && x.when <= to).Select(x => x.id));
                }
                featured = featured.Where(rank.ContainsKey).Distinct().Take(MAX_FEATURED).ToList();
                info.Featured = featured;

                // base: an exact pool of the same price, else the biggest one
                var basePool = exact.FirstOrDefault(k => priceOf.TryGetValue(k.ToString(), out var p) && p == PriceKey(r), largest);
                pools[id] = WithFeatured(pools[basePool], featured, rank, id);
                if (info.Source == "lineup") lineups++; else built++;
            }
            _machines = machines;
            Log.Info("gacha", $"{machines.Count} crank-a-kai machines: {machines.Values.Count(m => m.Source == "exact")} with their real pool, {lineups} from their lineup, {built} rebuilt from the Yo-kai released with them.");
        }

        private static GachaPoolItem Clone(GachaPoolItem p) => JsonConvert.DeserializeObject<GachaPoolItem>(JsonConvert.SerializeObject(p))!;

        private static GachaPoolItem WithFeatured(GachaPoolItem basePool, List<long> featured, Dictionary<long, int> rank, int id)
        {
            var pool = Clone(basePool);
            pool.GachaId = id;
            pool.Yokais ??= new();
            pool.Weights ??= new();
            pool.Items ??= new();
            pool.ConvertItem ??= new();
            pool.RateUp ??= new();
            double maxWeight = pool.Weights.Values.DefaultIfEmpty(1).Max();
            foreach (var y in featured)
            {
                var key = rank[y].ToString();
                if (!pool.Yokais.TryGetValue(key, out var list))
                {
                    pool.Yokais[key] = list = new List<long>();
                    // a rank the base machine does not give: rare, like the top rank of a real machine
                    pool.Weights[key] = Math.Max(0.5, Math.Round(maxWeight * 0.08, 2));
                }
                if (!list.Contains(y)) list.Add(y);
                pool.RateUp[y] = FEATURED_RATE_UP;
            }
            FillConvertItems(pool);
            return pool;
        }

        // A maxed Yo-kai turns into the convert item of its rank: every rank needs one.
        private static void FillConvertItems(GachaPoolItem pool)
        {
            if (pool.ConvertItem.Count == 0) return;
            var best = pool.ConvertItem.OrderByDescending(kv => int.TryParse(kv.Key, out var n) ? n : 0).First().Value;
            foreach (var key in pool.Yokais.Keys)
                if (!pool.ConvertItem.ContainsKey(key)) pool.ConvertItem[key] = best;
        }

        // A pool that draws from all the given machines (each machine counts the same).
        public static int MixPoolId(IEnumerable<int> machineIds)
        {
            GachaPoolManager.EnsureLoaded();
            var ids = machineIds.Where(i => GachaPoolManager.Gachas.ContainsKey(i)).Distinct().OrderBy(i => i).ToList();
            if (ids.Count == 0) return -1;
            if (ids.Count == 1) return ids[0];
            var key = string.Join(",", ids);
            return _mixIds.GetOrAdd(key, _ =>
            {
                var mix = new GachaPoolItem { Weights = new(), Yokais = new(), Items = new(), ConvertItem = new(), RateUp = new() };
                foreach (var gid in ids)
                {
                    var p = GachaPoolManager.Gachas[gid];
                    var sum = p.Weights.Values.Sum();
                    if (sum <= 0) continue;
                    foreach (var w in p.Weights)
                        mix.Weights[w.Key] = mix.Weights.GetValueOrDefault(w.Key) + w.Value / sum * 100.0 / ids.Count;
                    foreach (var kv in p.Yokais ?? new())
                    {
                        if (!mix.Yokais.TryGetValue(kv.Key, out var l)) mix.Yokais[kv.Key] = l = new List<long>();
                        foreach (var y in kv.Value) if (!l.Contains(y)) l.Add(y);
                    }
                    foreach (var kv in p.Items ?? new())
                    {
                        if (!mix.Items.TryGetValue(kv.Key, out var l)) mix.Items[kv.Key] = l = new List<long>();
                        foreach (var it in kv.Value) if (!l.Contains(it)) l.Add(it);
                    }
                    foreach (var kv in p.ConvertItem ?? new()) mix.ConvertItem.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in p.RateUp ?? new()) mix.RateUp[kv.Key] = Math.Max(mix.RateUp.GetValueOrDefault(kv.Key), kv.Value);
                }
                FillConvertItems(mix);
                var mixId = Interlocked.Decrement(ref _nextMixId);
                mix.GachaId = mixId;
                _mixPools[mixId] = mix;
                return mixId;
            });
        }
    }
}
