using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Events;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Web.Wiki
{
    // Builds the in-game wiki's encyclopedia from the master data the server has loaded (already
    // translated when a Language is set), so it always matches the server's own game data.
    public class WikiData
    {
        public static readonly string[] RANKS = { "", "E", "D", "C", "B", "A", "S", "SS", "SSS", "Z", "ZZ", "ZZZ", "UZ", "UZ+" };
        public static readonly string[] TRIBES = { "", "Brave", "Mysterious", "Tough", "Charming", "Heartful", "Shady", "Eerie", "Slippery", "Enma", "Wicked" };
        private static readonly Regex TAGS = new("</?[A-Za-z]+>", RegexOptions.Compiled);

        public class Yokai
        {
            public string Id = "";
            public string Name = "";
            public int Rank;
            public int Tribe;
            public int Food;
            public int MaxLevel;
            public int BaseHp, MaxHp, BaseAtk, MaxAtk;
            public string EvolvesTo = "";
            public int EvolveLevel;
            public string Description = "";
            public string SkillName = "";
            public string SkillText = "";
            public string Bonus = "";
            public string? Image;
            public List<string> Groups = new();
        }

        public class Stage
        {
            public long Id;
            public long MapId;
            public int No;
            public string Name = "";
            public bool Boss;
            public bool Hidden;
            public int RecommendLevel;
            public List<string> Stars = new();
            public List<string> Enemies = new(); // Yo-kai ids
            public string Unlock = "";
        }

        public Dictionary<string, Yokai> YokaiById = new();
        public List<Yokai> YokaiList = new();
        public List<JObject> Maps = new();
        public Dictionary<long, List<Stage>> StagesByMap = new();
        public Dictionary<string, List<long>> StagesByYokai = new();
        public Dictionary<long, Stage> StageById = new();
        public List<object> Items = new();
        public Dictionary<string, string> ItemNames = new();
        public List<object> Missions = new();
        public List<object> Gachas = new();

        private static WikiData? _current;
        private static readonly object _lock = new();

        public static WikiData Current
        {
            get
            {
                lock (_lock) return _current ??= Build();
            }
        }

        // Drop the cache (after an event rescan or data change).
        public static void Invalidate()
        {
            lock (_lock) _current = null;
        }

        public static string Clean(string s) => TAGS.Replace(s ?? "", "").Replace("\r", "").Trim();

        private static List<string[]> Pipe(string table)
        {
            var c = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            return c.TryGetValue(table, out var raw) ? EventManager.ReadPipeRows(raw, out _) : new List<string[]>();
        }

        private static List<JObject> Json(string table)
        {
            var c = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            return c.TryGetValue(table, out var raw) ? EventManager.ReadJsonRows(raw, out _) : new List<JObject>();
        }

        private static int Int(string[] r, int i) => r.Length > i && int.TryParse(r[i], out var v) ? v : 0;
        private static string Str(string[] r, int i) => r.Length > i ? r[i] : "";

        private static WikiData Build()
        {
            var w = new WikiData();

            // Yo-kai: id | name | type | rank | tribe | levelType | food | maxLevel | baseHp | maxHp | baseAtk | maxAtk | evolveTo | evolveLv | dictId | description ...
            foreach (var r in Pipe("ywp_mst_youkai"))
            {
                if (r.Length < 16 || r[0].Length == 0) continue;
                var y = new Yokai
                {
                    Id = r[0], Name = Clean(r[1]), Rank = Int(r, 3), Tribe = Int(r, 4), Food = Int(r, 6), MaxLevel = Int(r, 7),
                    BaseHp = Int(r, 8), MaxHp = Int(r, 9), BaseAtk = Int(r, 10), MaxAtk = Int(r, 11),
                    EvolvesTo = Str(r, 12) == "0" ? "" : Str(r, 12), EvolveLevel = Int(r, 13), Description = Clean(r[15])
                };
                // Rank 0 rows are items/placeholders, not real Yo-kai
                if (y.Rank <= 0 || y.Rank >= RANKS.Length || y.Name.Length == 0) continue;
                // puni icon, big puni, then the character art (mypuni_* is only a decoration layer)
                foreach (var candidate in new[] { $"bl_{y.Id}.png", $"title2_bl_{y.Id}.png", $"bu_{y.Id}_01.png" })
                    if (StaticContent.TryFindAsset(candidate, out _)) { y.Image = candidate; break; }
                w.YokaiById[y.Id] = y;
            }
            // Soultimates: youkaiId | name | ? | description
            foreach (var r in Pipe("ywp_mst_youkai_skill"))
                if (r.Length > 3 && w.YokaiById.TryGetValue(r[0], out var y)) { y.SkillName = Clean(r[1]); y.SkillText = Clean(r[3]); }
            // Bonus effects: youkaiId | level | text
            foreach (var r in Pipe("ywp_mst_youkai_bonus_effect"))
                if (r.Length > 2 && w.YokaiById.TryGetValue(r[0], out var y) && y.Bonus.Length == 0) y.Bonus = Clean(r[2]);
            w.YokaiList = w.YokaiById.Values.OrderByDescending(x => x.Rank).ThenBy(x => x.Name).ToList();

            // Items: id | type | name | ? | ? | description | icon
            foreach (var r in Pipe("ywp_mst_item"))
            {
                if (r.Length < 6) continue;
                var name = Clean(r[2]);
                if (name.Length == 0) continue;
                w.ItemNames[r[0]] = name;
                w.Items.Add(new { id = r[0], type = Int(r, 1), name, text = Clean(r[5]) });
            }

            // Enemies: enemy param id -> Yo-kai id; enemy set -> params
            var paramToYokai = new Dictionary<string, string>();
            foreach (var r in Pipe("ywp_mst_youkai_enemy_param"))
                if (r.Length > 1) paramToYokai[r[0]] = r[1];
            var setToYokai = new Dictionary<string, List<string>>();
            foreach (var r in Pipe("ywp_mst_youkai_enemy_set"))
            {
                if (r.Length < 2) continue;
                var list = r.Skip(1).Where(paramToYokai.ContainsKey).Select(p => paramToYokai[p]).Distinct().ToList();
                setToYokai[r[0]] = list;
            }

            // Star / unlock conditions
            var cond = new Dictionary<long, string>();
            var unlocks = new Dictionary<long, string>();
            foreach (var c in Json("ywp_mst_stage_condition"))
            {
                var id = c.Value<long?>("conditionId") ?? 0;
                var d = Clean(c.Value<string>("description") ?? "");
                cond[id] = d;
                var open = c.Value<string>("openStageIdList") ?? "";
                foreach (var s in open.Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    if (long.TryParse(s, out var sid)) unlocks[sid] = d;
            }

            // Maps and stages
            w.Maps = Json("ywp_mst_map").OrderBy(m => m.Value<int?>("mapType") ?? 0).ThenBy(m => m.Value<int?>("sort") ?? 0).ToList();
            foreach (var s in Json("ywp_mst_stage"))
            {
                var st = new Stage
                {
                    Id = s.Value<long?>("stageId") ?? 0,
                    MapId = s.Value<long?>("mapId") ?? 0,
                    No = s.Value<int?>("stageNo") ?? 0,
                    Name = Clean(s.Value<string>("stageName") ?? ""),
                    Boss = (s.Value<int?>("bossFlg") ?? 0) != 0,
                    Hidden = !string.IsNullOrEmpty(s.Value<string>("hideStageRelationCode")),
                    RecommendLevel = s.Value<int?>("recommendLevel") ?? 0
                };
                foreach (var k in new[] { "starGetConditionId1", "starGetConditionId2", "starGetConditionId3" })
                    if (cond.TryGetValue(s.Value<long?>(k) ?? 0, out var d) && d.Length > 0) st.Stars.Add(d);
                if (setToYokai.TryGetValue((s.Value<long?>("enemySetId") ?? 0).ToString(), out var en)) st.Enemies = en;
                if (unlocks.TryGetValue(st.Id, out var u)) { st.Unlock = u; st.Hidden = true; }
                if (st.MapId == 0) continue;
                if (!w.StagesByMap.TryGetValue(st.MapId, out var l)) w.StagesByMap[st.MapId] = l = new List<Stage>();
                l.Add(st);
                w.StageById[st.Id] = st;
                foreach (var y in st.Enemies)
                {
                    if (!w.StagesByYokai.TryGetValue(y, out var sl)) w.StagesByYokai[y] = sl = new List<long>();
                    if (sl.Count < 40) sl.Add(st.Id);
                }
            }
            foreach (var l in w.StagesByMap.Values) l.Sort((a, b) => a.No != b.No ? a.No.CompareTo(b.No) : a.Id.CompareTo(b.Id));

            // Hidden stage hints from Puniemu's stage_data (locked_stage)
            try
            {
                var c = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
                if (c.TryGetValue("stage_data", out var sd))
                {
                    var tok = JObject.Parse(Regex.Replace(sd, @",\s*([}\]])", "$1"));
                    foreach (var p in tok.Properties())
                    {
                        if (!long.TryParse(p.Name, out var sid) || !w.StageById.TryGetValue(sid, out var st)) continue;
                        if (p.Value["locked_stage"] is JArray ls && ls.Count > 0)
                        {
                            st.Hidden = true;
                            st.Unlock = string.Join(" / ", ls.Select(x => Clean(x.Value<string>("description") ?? "")).Where(x => x.Length > 0));
                        }
                    }
                }
            }
            catch
            {
                // optional data
            }

            // Missions: id | ? | title | text | ... | reward (15)
            foreach (var r in Pipe("ywp_mst_mission"))
                if (r.Length > 3 && r[2].Length > 0)
                    w.Missions.Add(new { id = r[0], title = Clean(r[2]), text = Clean(r[3]), reward = Clean(Str(r, 15)) });

            // Crank-a-kai: price from ywp_mst_gacha, contents and odds from gacha_pool
            var prices = Pipe("ywp_mst_gacha").Where(r => r.Length > 5).GroupBy(r => r[0]).ToDictionary(g => g.Key, g => g.First());
            try
            {
                var c = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
                if (c.TryGetValue("gacha_pool", out var gp))
                {
                    foreach (var p in JObject.Parse(gp).Properties())
                    {
                        var weights = p.Value["weights"] as JObject;
                        var youkai = p.Value["youkai"] as JObject;
                        var tiers = new List<object>();
                        foreach (var t in (youkai?.Properties() ?? Enumerable.Empty<JProperty>()).OrderByDescending(x => int.TryParse(x.Name, out var n) ? n : 0))
                        {
                            int.TryParse(t.Name, out var rank);
                            var ids = (t.Value as JArray)?.Select(x => x.ToString()).ToList() ?? new();
                            tiers.Add(new
                            {
                                rank = rank > 0 && rank < RANKS.Length ? RANKS[rank] : t.Name,
                                chance = weights?.Value<double?>(t.Name),
                                yokai = ids.Select(id => new { id, name = w.YokaiById.TryGetValue(id, out var yy) ? yy.Name : "#" + id, image = w.YokaiById.TryGetValue(id, out var y2) ? y2.Image : null })
                            });
                        }
                        string price = "";
                        if (prices.TryGetValue(p.Name, out var pr))
                        {
                            var cost = Str(pr, 5);
                            price = Str(pr, 3) == "1" ? cost + " Y-Money" : cost + " x " + (w.ItemNames.TryGetValue(Str(pr, 4), out var itn) ? itn : "item " + Str(pr, 4));
                        }
                        w.Gachas.Add(new { id = p.Name, price, tiers });
                    }
                }
            }
            catch
            {
                // optional data
            }
            return w;
        }
    }
}
