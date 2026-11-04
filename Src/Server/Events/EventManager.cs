using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Infrastructure;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Events
{
    // Event switcher.
    //
    // The official servers only ever shipped the events that were running at that moment: an event is
    // "on" when it has a row in ywp_mst_event and its maps/quests are inside their open/close dates.
    // The data dumps the community has still contain the leftovers of many past events (maps, quests,
    // assist bonuses...). This class:
    //   1. builds a catalog of every event referenced anywhere in the loaded master data, plus the
    //      events defined by drop-in packs in the Events/ folder,
    //   2. serves the event related tables re-written for the selected events: the selected events get
    //      a ywp_mst_event row and their maps/quests are re-opened around "now", the others are closed.
    //
    // The selection is global (admin panel / server_state.json) and can be overridden per player from
    // the in-game portal page. With mode "default" and no player choice nothing is changed at all.
    public static class EventManager
    {
        private class PipeRule
        {
            public int EventIdCol;
            public int[] StartCols = Array.Empty<int>();
            public int[] EndCols = Array.Empty<int>();
        }

        // Pipe ("tableData") tables that carry an event id, and which of their columns are dates.
        private static readonly Dictionary<string, PipeRule> PIPE_RULES = new()
        {
            ["ywp_mst_event_quest"] = new PipeRule { EventIdCol = 0, EndCols = new[] { 7 } },
            ["ywp_mst_event_quest_sub"] = new PipeRule { EventIdCol = 0, StartCols = new[] { 6 } },
            ["ywp_mst_event_youkai_assist_disp"] = new PipeRule { EventIdCol = 1, StartCols = new[] { 3 }, EndCols = new[] { 4 } },
            ["ywp_mst_event_tutorial"] = new PipeRule { EventIdCol = 0 },
            ["ywp_mst_event_tutorial_message"] = new PipeRule { EventIdCol = 0 },
            ["ywp_mst_event_point_buff"] = new PipeRule { EventIdCol = 0 },
            ["ywp_mst_event_point_buff_lv"] = new PipeRule { EventIdCol = 0 },
            ["ywp_mst_event_point_trade_release"] = new PipeRule { EventIdCol = 0 },
            ["ywp_mst_event_match"] = new PipeRule { EventIdCol = 0 },
        };

        public const string PLAYER_CHOICE_TABLE = "puniemu_events";
        private const string EVENT_TABLE = "ywp_mst_event";
        private const string MAP_TABLE = "ywp_mst_map";
        private const string GACHA_TABLE = "ywp_mst_gacha";

        private static readonly Regex DATE_DASH = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", RegexOptions.Compiled);
        private static readonly Regex DATE_COMPACT = new(@"^\d{14}$", RegexOptions.Compiled);

        private static readonly object _catalogLock = new();
        private static Dictionary<long, EventInfo> _catalog = new();
        private static HashSet<string> _transformTables = new();
        private static readonly ConcurrentDictionary<string, string> _servedCache = new();

        public static IReadOnlyCollection<EventInfo> Catalog
        {
            get { lock (_catalogLock) return _catalog.Values.OrderBy(x => x.EventId).ToList(); }
        }

        public static void Initialize()
        {
            Reload();
            ServerStateStore.Changed += () => _servedCache.Clear();
        }

        // Re-scans the master data and the Events/ folder.
        public static void Reload()
        {
            var catalog = new Dictionary<long, EventInfo>();
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;

            EventInfo Get(long id)
            {
                if (!catalog.TryGetValue(id, out var e))
                {
                    e = new EventInfo { EventId = id, Name = $"Event #{id}" };
                    catalog[id] = e;
                }
                return e;
            }

            // 1. ywp_mst_event rows
            if (cache.TryGetValue(EVENT_TABLE, out var evRaw))
            {
                foreach (var row in ReadJsonRows(evRaw, out _))
                {
                    var id = row.Value<long?>("eventId") ?? 0;
                    if (id == 0) continue;
                    var e = Get(id);
                    e.Sources.Add("master");
                    e.MstEventRow = (JObject)row.DeepClone();
                    e.EventType = row.Value<int?>("eventType") ?? 0;
                    var desc = row.Value<string>("description");
                    if (!string.IsNullOrWhiteSpace(desc)) e.Name = desc!;
                }
            }

            // 2. maps
            if (cache.TryGetValue(MAP_TABLE, out var mapRaw))
            {
                foreach (var row in ReadJsonRows(mapRaw, out _))
                {
                    var id = row.Value<long?>("eventId") ?? 0;
                    if (id == 0) continue;
                    var e = Get(id);
                    e.MapIds.Add(row.Value<long?>("mapId") ?? 0);
                    e.Tables.Add(MAP_TABLE);
                    e.Sources.Add("found");
                    if (e.Name.StartsWith("Event #") && !string.IsNullOrWhiteSpace(row.Value<string>("mapName")))
                        e.Name = row.Value<string>("mapName")!;
                    var open = row.Value<string>("openDt");
                    var close = row.Value<string>("closeDt");
                    if (string.IsNullOrEmpty(e.Period) && !string.IsNullOrEmpty(open))
                        e.Period = $"{open} ~ {close}";
                    // Normal story maps (mapType 1) that happen to carry an event id are never switched.
                    if ((row.Value<int?>("mapType") ?? 1) != 1) e.Selectable = true;
                }
            }

            // 3. other event tables
            foreach (var kv in cache)
            {
                if (!kv.Key.StartsWith("ywp_mst_event_")) continue;
                if (PIPE_RULES.TryGetValue(kv.Key, out var rule))
                {
                    foreach (var row in ReadPipeRows(kv.Value, out _))
                    {
                        if (row.Length > rule.EventIdCol && long.TryParse(row[rule.EventIdCol], out var id) && id != 0)
                        {
                            var e = Get(id);
                            e.Tables.Add(kv.Key);
                            e.Sources.Add("found");
                            e.Selectable = true;
                            // Original dates, so the lists can show when the event ran
                            if (string.IsNullOrEmpty(e.Period))
                            {
                                var dates = rule.StartCols.Concat(rule.EndCols).Where(c => c < row.Length)
                                    .Select(c => PrettyDate(row[c])).Where(d => d != null).ToList();
                                if (dates.Count > 0) e.Period = rule.StartCols.Length > 0 && rule.EndCols.Length > 0 && dates.Count > 1
                                    ? $"{dates[0]} ~ {dates[1]}" : (rule.EndCols.Length > 0 ? "until " : "from ") + dates[0];
                            }
                            // Characters that talk in the event's story: a good hint of what the event was
                            if (kv.Key == "ywp_mst_event_tutorial_message" && row.Length > 6 && row[6].Trim().Any(char.IsLetterOrDigit) && e.Hints.Count < 4 && !e.Hints.Contains(row[6].Trim()))
                                e.Hints.Add(row[6].Trim());
                        }
                    }
                }
                else
                {
                    foreach (var row in ReadJsonRows(kv.Value, out _))
                    {
                        var id = row.Value<long?>("eventId") ?? 0;
                        if (id == 0) continue;
                        var e = Get(id);
                        e.Tables.Add(kv.Key);
                        e.Sources.Add("found");
                        e.Selectable = true;
                    }
                }
            }

            // 4. event packs
            var transformTables = new HashSet<string> { EVENT_TABLE, MAP_TABLE };
            foreach (var t in PIPE_RULES.Keys) transformTables.Add(t);
            var dir = DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.EventsPath);
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.Equals("names.json", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var packs = new List<EventPack>();
                        var tok = JToken.Parse(File.ReadAllText(file));
                        if (tok is JArray arr) packs.AddRange(arr.ToObject<List<EventPack>>()!);
                        else packs.Add(tok.ToObject<EventPack>()!);
                        foreach (var pack in packs)
                        {
                            if (pack.EventId == 0) continue;
                            pack.FileName = fileName;
                            var e = Get(pack.EventId);
                            e.Pack = pack;
                            e.Sources.Add("pack");
                            e.Selectable = true;
                            if (!string.IsNullOrWhiteSpace(pack.Name)) e.Name = pack.Name!;
                            if (!string.IsNullOrWhiteSpace(pack.Description)) e.Description = pack.Description!;
                            if (!string.IsNullOrWhiteSpace(pack.Period)) e.Period = pack.Period!;
                            if (pack.MstEvent != null) e.EventType = pack.MstEvent.Value<int?>("eventType") ?? e.EventType;
                            foreach (var t in pack.Tables.Keys)
                            {
                                e.Tables.Add(t);
                                transformTables.Add(t);
                            }
                            if (pack.GachaIds.Count > 0) transformTables.Add(GACHA_TABLE);
                            if (pack.Tables.TryGetValue(MAP_TABLE, out var maps) && maps is JArray mapArr)
                                foreach (var m in mapArr.OfType<JObject>())
                                    e.MapIds.Add(m.Value<long?>("mapId") ?? 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("events", $"Could not load event pack {fileName}: {ex.Message}");
                    }
                }

                // Optional friendly names: { "1097": "Code Geass collaboration", ... }
                var namesFile = Path.Combine(dir, "names.json");
                if (File.Exists(namesFile))
                {
                    try
                    {
                        var names = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(namesFile)) ?? new();
                        foreach (var n in names)
                            if (long.TryParse(n.Key, out var id) && catalog.TryGetValue(id, out var e))
                                e.Name = n.Value;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("events", $"Could not read names.json: {ex.Message}");
                    }
                }
            }

            foreach (var e in catalog.Values)
            {
                e.MapIds = e.MapIds.Distinct().Where(x => x != 0).ToList();
                if (string.IsNullOrEmpty(e.Description) && e.Hints.Count > 0)
                    e.Description = "Characters: " + string.Join(", ", e.Hints);
            }

            lock (_catalogLock)
            {
                _catalog = catalog;
                _transformTables = transformTables;
            }
            _servedCache.Clear();
            Log.Info("events", $"Event catalog: {catalog.Count} events ({catalog.Values.Count(x => x.Selectable)} switchable, {catalog.Values.Count(x => x.Pack != null)} from packs)");
        }

        // ---- selection ---------------------------------------------------

        public static EventSelection GlobalSelection()
        {
            var s = ServerStateStore.Current.Events;
            if (s.Mode == "rotation") return RotationSelection(s);
            if (s.Mode != "custom") return EventSelection.None;
            return new EventSelection
            {
                Enabled = true,
                ActiveIds = s.ActiveEventIds.ToHashSet(),
                HideInactiveMaps = s.HideInactiveEventMaps,
                DurationDays = Math.Clamp(s.EventDurationDays, 1, 3650)
            };
        }

        // "rotation" mode: cycles through the playable events (or the ticked ones, if any) like the live
        // game did. RotationCount events are on at once and change every RotationDays days.
        private static EventSelection RotationSelection(EventSettings s)
        {
            var pool = (s.ActiveEventIds.Count > 0 ? s.ActiveEventIds
                       : Catalog.Where(x => x.Selectable && x.Playable).Select(x => x.EventId)).OrderBy(x => x).ToList();
            var sel = new EventSelection
            {
                Enabled = true,
                HideInactiveMaps = s.HideInactiveEventMaps,
                DurationDays = Math.Max(1, s.RotationDays)
            };
            if (pool.Count == 0) return sel;
            var days = Math.Max(1, s.RotationDays);
            var count = Math.Clamp(s.RotationCount, 1, pool.Count);
            long slot = (long)Math.Floor((DateTime.Now - new DateTime(2020, 1, 1)).TotalDays) / days;
            for (int i = 0; i < count; i++) sel.ActiveIds.Add(pool[(int)((slot * count + i) % pool.Count)]);
            // Events end when the rotation slot ends, not "now + N days"
            sel.DurationDays = (int)Math.Max(1, days - ((DateTime.Now - new DateTime(2020, 1, 1)).TotalDays % days));
            return sel;
        }

        public static async Task<PlayerEventChoice?> GetPlayerChoiceAsync(string gdkey)
        {
            try
            {
                return await UserDataManager.Logic.UserDataManager.GetYwpUserAsync<PlayerEventChoice>(gdkey, PLAYER_CHOICE_TABLE);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<EventSelection> GetSelectionAsync(string? gdkey)
        {
            var global = GlobalSelection();
            var s = ServerStateStore.Current.Events;
            if (string.IsNullOrEmpty(gdkey) || !s.AllowPlayerChoice) return global;
            var choice = await GetPlayerChoiceAsync(gdkey);
            if (choice == null || choice.Mode != "custom") return global;
            return new EventSelection
            {
                Enabled = true,
                ActiveIds = choice.Ids.ToHashSet(),
                HideInactiveMaps = s.HideInactiveEventMaps,
                DurationDays = Math.Clamp(s.EventDurationDays, 1, 3650)
            };
        }

        // Names of the events that are currently on for a player (or the server when gdkey is null).
        public static List<string> DescribeActiveEvents(EventSelection? sel)
        {
            sel ??= GlobalSelection();
            var catalog = Catalog;
            if (!sel.Enabled)
                return catalog.Where(x => x.Selectable && x.Sources.Contains("master")).Select(x => x.Name).ToList();
            return catalog.Where(x => sel.ActiveIds.Contains(x.EventId)).Select(x => x.Name).ToList();
        }

        // ---- serving -----------------------------------------------------

        // Returns the table text to send to this player.
        public static async Task<string> ServeAsync(string table, string raw, string? gdkey)
        {
            bool relevant;
            lock (_catalogLock) relevant = _transformTables.Contains(table);
            if (!relevant) return raw;
            var sel = await GetSelectionAsync(gdkey);
            return Serve(table, raw, sel);
        }

        public static string Serve(string table, string raw, EventSelection sel)
        {
            if (!sel.Enabled) return raw;
            bool relevant;
            lock (_catalogLock) relevant = _transformTables.Contains(table);
            if (!relevant) return raw;

            // Dates are relative to "now", so cached results are only valid for the current hour.
            var key = $"{table}|{sel.Key}|{DateTime.Now:yyyyMMddHH}";
            if (_servedCache.TryGetValue(key, out var cached)) return cached;
            string result;
            try
            {
                result = Transform(table, raw, sel);
            }
            catch (Exception ex)
            {
                Log.Error("events", $"Could not apply the event selection to {table}: {ex.Message}");
                result = raw;
            }
            if (_servedCache.Count > 4000) _servedCache.Clear();
            _servedCache[key] = result;
            return result;
        }

        private static string Transform(string table, string raw, EventSelection sel)
        {
            Dictionary<long, EventInfo> catalog;
            lock (_catalogLock) catalog = _catalog;
            var now = DateTime.Now;
            var start = now.AddDays(-1);
            var end = now.AddDays(sel.DurationDays);
            bool IsSwitchable(long id) => catalog.TryGetValue(id, out var e) && e.Selectable;
            bool IsActive(long id) => sel.ActiveIds.Contains(id);
            var activePacks = sel.ActiveIds.Where(id => catalog.TryGetValue(id, out var e) && e.Pack != null)
                                           .Select(id => catalog[id].Pack!).ToList();

            if (table == EVENT_TABLE)
            {
                if (!IsJsonRowTable(raw)) return raw; // never replace a table we can't read with an empty one
                var rows = ReadJsonRows(raw, out var wrap);
                var output = new JArray();
                var present = new HashSet<long>();
                JObject? template = rows.FirstOrDefault(r => (r.Value<int?>("eventType") ?? 0) == 1) ?? rows.FirstOrDefault();
                foreach (var row in rows)
                {
                    var id = row.Value<long?>("eventId") ?? 0;
                    if (IsSwitchable(id) && !IsActive(id)) continue;
                    var copy = (JObject)row.DeepClone();
                    if (IsActive(id))
                    {
                        if (copy["eventStatus"] != null) copy["eventStatus"] = 1;
                        if (copy["eventButtonHiddenFlg"] != null) copy["eventButtonHiddenFlg"] = 0;
                    }
                    output.Add(copy);
                    present.Add(id);
                }
                foreach (var id in sel.ActiveIds)
                {
                    if (present.Contains(id) || !catalog.TryGetValue(id, out var e)) continue;
                    JObject? row = e.Pack?.MstEvent != null ? (JObject)e.Pack.MstEvent.DeepClone()
                                 : e.MstEventRow != null ? (JObject)e.MstEventRow.DeepClone()
                                 : null;
                    // Only invent a row for events that have content to show.
                    if (row == null && template != null && e.Playable)
                    {
                        row = (JObject)template.DeepClone();
                        row["description"] = e.Name;
                        if (e.EventType != 0) row["eventType"] = e.EventType;
                    }
                    if (row == null) continue;
                    row["eventId"] = id;
                    if (row["eventStatus"] != null || e.Pack?.MstEvent == null) row["eventStatus"] = 1;
                    output.Add(row);
                }
                foreach (var p in activePacks) AppendPackRows(output, p, table);
                return WriteJsonRows(output, wrap);
            }

            if (table == MAP_TABLE)
            {
                if (!IsJsonRowTable(raw)) return raw; // never replace a table we can't read with an empty one
                var rows = ReadJsonRows(raw, out var wrap);
                var output = new JArray();
                foreach (var row in rows)
                {
                    var copy = (JObject)row.DeepClone();
                    var id = copy.Value<long?>("eventId") ?? 0;
                    var mapType = copy.Value<int?>("mapType") ?? 1;
                    if (id != 0 && mapType != 1 && IsSwitchable(id))
                    {
                        if (IsActive(id))
                        {
                            copy["openDt"] = Format(start, "yyyy-MM-dd HH:mm:ss");
                            copy["closeDt"] = Format(end, "yyyy-MM-dd HH:mm:ss");
                        }
                        else if (sel.HideInactiveMaps)
                        {
                            copy["openDt"] = "2000-01-01 00:00:00";
                            copy["closeDt"] = "2000-01-02 00:00:00";
                        }
                    }
                    output.Add(copy);
                }
                foreach (var p in activePacks)
                {
                    var before = output.Count;
                    AppendPackRows(output, p, table);
                    for (int i = before; i < output.Count; i++)
                    {
                        if (output[i] is JObject m)
                        {
                            m["openDt"] = Format(start, "yyyy-MM-dd HH:mm:ss");
                            m["closeDt"] = Format(end, "yyyy-MM-dd HH:mm:ss");
                        }
                    }
                }
                return WriteJsonRows(output, wrap);
            }

            if (table == GACHA_TABLE)
            {
                var gachas = activePacks.SelectMany(p => p.GachaIds).Select(x => x.ToString()).ToHashSet();
                var rows = ReadPipeRows(raw, out var wrap);
                foreach (var r in rows)
                {
                    if (r.Length > 2 && gachas.Contains(r[0]))
                    {
                        r[1] = ShiftDate(r[1], start);
                        r[2] = ShiftDate(r[2], end);
                    }
                }
                var outRows = rows.Select(r => string.Join("|", r)).ToList();
                foreach (var p in activePacks) AppendPackPipeRows(outRows, p, table);
                return WritePipeRows(outRows, wrap);
            }

            if (PIPE_RULES.TryGetValue(table, out var rule) || activePacks.Any(p => p.Tables.ContainsKey(table)))
            {
                // Pipe tables with an event id column (or tables a pack adds rows to).
                if (LooksLikePipeTable(raw))
                {
                    var rows = ReadPipeRows(raw, out var wrap);
                    if (rule != null)
                    {
                        foreach (var r in rows)
                        {
                            if (r.Length <= rule.EventIdCol || !long.TryParse(r[rule.EventIdCol], out var id) || !IsActive(id)) continue;
                            foreach (var c in rule.StartCols) if (c < r.Length) r[c] = ShiftDate(r[c], start);
                            foreach (var c in rule.EndCols) if (c < r.Length) r[c] = ShiftDate(r[c], end);
                        }
                    }
                    var outRows = rows.Select(r => string.Join("|", r)).ToList();
                    foreach (var p in activePacks) AppendPackPipeRows(outRows, p, table);
                    return WritePipeRows(outRows, wrap);
                }
                else
                {
                    var rows = ReadJsonRows(raw, out var wrap);
                    var output = new JArray();
                    foreach (var row in rows)
                    {
                        var copy = (JObject)row.DeepClone();
                        var id = copy.Value<long?>("eventId") ?? 0;
                        if (IsActive(id)) ShiftJsonDates(copy, start, end);
                        output.Add(copy);
                    }
                    foreach (var p in activePacks) AppendPackRows(output, p, table);
                    return WriteJsonRows(output, wrap);
                }
            }
            return raw;
        }

        // ---- helpers -----------------------------------------------------

        private static void AppendPackRows(JArray output, EventPack pack, string table)
        {
            if (!pack.Tables.TryGetValue(table, out var tok)) return;
            if (tok is JArray arr) foreach (var r in arr) output.Add(r.DeepClone());
        }

        private static void AppendPackPipeRows(List<string> rows, EventPack pack, string table)
        {
            if (!pack.Tables.TryGetValue(table, out var tok)) return;
            if (tok.Type == JTokenType.String)
            {
                var s = tok.ToString();
                if (!string.IsNullOrEmpty(s)) rows.AddRange(s.Split('*'));
            }
            else if (tok is JArray arr)
            {
                rows.AddRange(arr.Select(x => x.Type == JTokenType.Array ? string.Join("|", x.Select(c => c.ToString())) : x.ToString()));
            }
        }

        private static void ShiftJsonDates(JObject row, DateTime start, DateTime end)
        {
            foreach (var p in row.Properties().ToList())
            {
                if (p.Value.Type != JTokenType.String) continue;
                var v = p.Value.ToString();
                if (!DATE_DASH.IsMatch(v) && !DATE_COMPACT.IsMatch(v)) continue;
                var n = p.Name.ToLowerInvariant();
                if (n.Contains("start") || n.Contains("open") || n.Contains("begin")) p.Value = ShiftDate(v, start);
                else if (n.Contains("end") || n.Contains("close") || n.Contains("limit")) p.Value = ShiftDate(v, end);
            }
        }

        private static string? PrettyDate(string v)
        {
            if (DATE_DASH.IsMatch(v)) return v.Substring(0, 10);
            if (DATE_COMPACT.IsMatch(v)) return $"{v.Substring(0, 4)}-{v.Substring(4, 2)}-{v.Substring(6, 2)}";
            return null;
        }

        private static string ShiftDate(string original, DateTime target)
        {
            if (DATE_DASH.IsMatch(original)) return Format(target, "yyyy-MM-dd HH:mm:ss");
            if (DATE_COMPACT.IsMatch(original)) return Format(target, "yyyyMMddHHmmss");
            return original;
        }

        private static string Format(DateTime dt, string fmt) => dt.ToString(fmt, CultureInfo.InvariantCulture);

        private static bool LooksLikePipeTable(string raw)
        {
            var t = raw.TrimStart();
            if (!t.StartsWith("{") && !t.StartsWith("[")) return true;
            try
            {
                var tok = JToken.Parse(raw);
                return tok is JObject o && o["tableData"] != null;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsJsonRowTable(string raw)
        {
            try
            {
                var tok = JToken.Parse(raw);
                return tok is JArray || (tok is JObject o && o["data"] is JArray);
            }
            catch
            {
                return false;
            }
        }

        // Reads rows of a JSON table. Supports a bare array or {"data": [...]} (wrap remembers which).
        internal static List<JObject> ReadJsonRows(string raw, out JObject? wrap)
        {
            wrap = null;
            try
            {
                var tok = JToken.Parse(raw);
                JArray? arr = null;
                if (tok is JArray a) arr = a;
                else if (tok is JObject o && o["data"] is JArray d)
                {
                    wrap = o;
                    arr = d;
                }
                return arr?.OfType<JObject>().ToList() ?? new List<JObject>();
            }
            catch
            {
                return new List<JObject>();
            }
        }

        private static string WriteJsonRows(JArray rows, JObject? wrap)
        {
            if (wrap == null) return rows.ToString(Formatting.None);
            var copy = (JObject)wrap.DeepClone();
            copy["data"] = rows;
            return copy.ToString(Formatting.None);
        }

        // Reads a pipe/asterisk table. Supports raw text or {"tableData": "..."}.
        internal static List<string[]> ReadPipeRows(string raw, out JObject? wrap)
        {
            wrap = null;
            string text = raw;
            var t = raw.TrimStart();
            if (t.StartsWith("{"))
            {
                try
                {
                    var o = JObject.Parse(raw);
                    if (o["tableData"] == null) return new List<string[]>();
                    wrap = o;
                    text = o["tableData"]!.ToString();
                }
                catch
                {
                    return new List<string[]>();
                }
            }
            else if (t.StartsWith("["))
            {
                return new List<string[]>();
            }
            if (string.IsNullOrEmpty(text)) return new List<string[]>();
            return text.Split('*').Select(r => r.Split('|')).ToList();
        }

        private static string WritePipeRows(List<string> rows, JObject? wrap)
        {
            var text = string.Join("*", rows.Where(r => r.Length > 0));
            if (wrap == null) return text;
            var copy = (JObject)wrap.DeepClone();
            copy["tableData"] = text;
            return copy.ToString(Formatting.None);
        }

        // Saves a player's own event choice.
        public static async Task SetPlayerChoiceAsync(string gdkey, PlayerEventChoice choice)
        {
            await UserDataManager.Logic.UserDataManager.SetYwpUserAsync(gdkey, PLAYER_CHOICE_TABLE, choice);
        }
    }
}
