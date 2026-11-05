using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.Infrastructure;
using System.Text;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Translation
{
    // Translates the game's master data (Yo-kai names, item names, map texts, missions, dialogue...)
    // before it is sent to the client, so the Japanese Puni Puni data can be played in English.
    //
    // Set "Language": "en" in appsettings.json. Every *.json file in Translations/en/ is loaded:
    //   * a plain object {"ジバニャン": "Jibanyan", ...} replaces whole text cells that match exactly,
    //   * files named phrases*.json are applied as substring replacements afterwards (longest first).
    // Tools/build_translation.py builds these dictionaries automatically from the English Wibble Wobble
    // data and can export every untranslated string for community translation.
    public static class TranslationManager
    {
        private static readonly Regex HIGHLIGHT = new("</?[A-Z]>", RegexOptions.Compiled);
        private static readonly Regex JAPANESE = new(@"[぀-ヿ㐀-鿿ｦ-ﾟ]", RegexOptions.Compiled);

        private static Dictionary<string, string> _exact = new();
        // machine*.json: only used when neither the dictionaries nor the patterns give a full translation
        private static Dictionary<string, string> _machine = new();
        private static List<KeyValuePair<string, string>> _phrases = new();
        private static List<KeyValuePair<Regex, string>> _patterns = new();
        [ThreadStatic] private static int _patternDepth;
        // Texts that are still Japanese after translation (text -> how often it occurs), for the export.
        private static Dictionary<string, int> _untranslated = new();
        [ThreadStatic] private static Dictionary<string, int>? _collect;
        private static int _romaji;
        public static IReadOnlyDictionary<string, int> Untranslated => _untranslated;

        public static int LoadedEntries => _exact.Count + _machine.Count + _phrases.Count + _patterns.Count;
        public static int TranslatedCells { get; private set; }
        public static int RemainingJapaneseCells { get; private set; }
        public static int RomajiCells { get; private set; }
        // Katakana names nobody translated are written in romaji (RomajiFallback, on by default)
        public static bool RomajiFallback { get; set; } = true;
        [ThreadStatic] private static bool _usedFallback;
        public static bool Enabled => !string.IsNullOrEmpty(DataManager.Logic.DataManager.Language);

        public static void Initialize()
        {
            if (!Enabled) return;
            var lang = DataManager.Logic.DataManager.Language;
            var dir = Path.Combine(DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.TranslationsPath), lang);
            if (!Directory.Exists(dir))
            {
                Log.Warn("translation", $"Language '{lang}' selected but {dir} does not exist. Game data is served untranslated.");
                return;
            }

            var exact = new Dictionary<string, string>();
            var machine = new Dictionary<string, string>();
            var phrases = new Dictionary<string, string>();
            var patterns = new List<KeyValuePair<Regex, string>>();
            // Files are applied in alphabetical order, later files win. Machine translations
            // (machine*.json, see Tools/machine_translate.py) come first so any real translation beats them.
            foreach (var file in Directory.EnumerateFiles(dir, "*.json")
                         .OrderBy(x => Path.GetFileName(x).StartsWith("machine", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(x => x, StringComparer.Ordinal))
            {
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file, Encoding.UTF8)) ?? new();
                    // patterns*.json: regular expressions for numbered texts ("ステージ12" -> "Stage 12")
                    if (Path.GetFileName(file).StartsWith("patterns", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var kv in dict)
                        {
                            if (kv.Key.StartsWith("_") || string.IsNullOrEmpty(kv.Value)) continue;
                            try { patterns.Add(new(new Regex(kv.Key, RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromMilliseconds(50)), Sanitize(kv.Value))); }
                            catch (Exception ex) { Log.Warn("translation", $"Bad pattern '{kv.Key}': {ex.Message}"); }
                        }
                        continue;
                    }
                    var name = Path.GetFileName(file);
                    var target = name.StartsWith("phrases", StringComparison.OrdinalIgnoreCase) ? phrases
                        : name.StartsWith("machine", StringComparison.OrdinalIgnoreCase) ? machine : exact;
                    foreach (var kv in dict)
                    {
                        if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrWhiteSpace(kv.Value) || kv.Key.StartsWith("_")) continue;
                        target[kv.Key] = Sanitize(kv.Value);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("translation", $"Could not read {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            _exact = exact;
            _machine = machine;
            _phrases = phrases.OrderByDescending(x => x.Key.Length).ToList();
            _patterns = patterns;

            ApplyToGameData();
            Log.Info("translation", $"Language '{lang}': {exact.Count} strings + {machine.Count} machine translations + {phrases.Count} phrases + {patterns.Count} patterns loaded, {TranslatedCells} texts translated ({RomajiCells} of them as romaji names), {RemainingJapaneseCells} still Japanese.");
        }

        // The table format uses | and * as separators, a translation must never contain them.
        private static string Sanitize(string s) => s.Replace("|", "/").Replace("*", "＊");

        public static string Translate(string text, out bool changed)
        {
            changed = false;
            if (string.IsNullOrEmpty(text) || !JAPANESE.IsMatch(text)) return text;
            var result = text;
            if (_exact.TryGetValue(text, out var hit))
            {
                result = hit;
            }
            else
            {
                // Some cells differ only by line breaks.
                var flat = text.Replace("\r", "").Replace("\n", "");
                if (flat != text && _exact.TryGetValue(flat, out hit)) result = hit;
            }
            // Full-width letters / digits ("ＢＧダイヤニャン", "ステージ１２") are matched in their normal form
            if (result == text)
            {
                var nfkc = text.Normalize(System.Text.NormalizationForm.FormKC);
                if (nfkc != text && _exact.TryGetValue(nfkc, out var nhit)) result = nhit;
            }
            if (_patterns.Count > 0 && JAPANESE.IsMatch(result))
            {
                var matchText = result == text ? text.Normalize(System.Text.NormalizationForm.FormKC) : result;
                foreach (var p in _patterns)
                {
                    try
                    {
                        // Captured parts are translated too ("ボスステージ13のジバニャンを倒そう")
                        // ignore line breaks and the <R>..</R> highlight tags when matching
                        var m = p.Key.Match(HIGHLIGHT.Replace(matchText.Replace("\r", "").Replace("\n", ""), ""));
                        if (!m.Success) continue;
                        var repl = p.Value;
                        for (int g = m.Groups.Count - 1; g >= 1; g--)
                        {
                            var part = m.Groups[g].Value;
                            // captured parts go through the dictionaries and patterns again ("【スコア35000以上】")
                            if (_patternDepth < 3)
                            {
                                _patternDepth++;
                                try { part = Translate(part, out _); }
                                finally { _patternDepth--; }
                            }
                            repl = repl.Replace("$" + g, part);
                        }
                        result = repl;
                        break;
                    }
                    catch (RegexMatchTimeoutException) { }
                }
            }
            if (_machine.Count > 0 && JAPANESE.IsMatch(result))
            {
                var flat = text.Replace("\r", "");
                if (_machine.TryGetValue(text, out var mhit) || _machine.TryGetValue(flat, out mhit) ||
                    _machine.TryGetValue(flat.Normalize(System.Text.NormalizationForm.FormKC), out mhit))
                    result = mhit;
            }
            if (_phrases.Count > 0 && JAPANESE.IsMatch(result))
            {
                foreach (var p in _phrases)
                {
                    if (result.Contains(p.Key)) result = result.Replace(p.Key, p.Value);
                }
            }
            if (RomajiFallback && _patternDepth == 0 && result == text && Romaji.IsNameLike(text))
            {
                result = Romaji.Convert(text);
                _usedFallback = true;
            }
            changed = result != text;
            return result;
        }

        private static void ApplyToGameData()
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            int translated = 0, remaining = 0;
            _collect = new Dictionary<string, int>();
            _romaji = 0;
            foreach (var key in cache.Keys.ToList())
            {
                var raw = cache[key];
                if (string.IsNullOrEmpty(raw) || !JAPANESE.IsMatch(raw)) continue;
                try
                {
                    var newText = TranslateTableText(raw, ref translated, ref remaining);
                    if (!ReferenceEquals(newText, raw) && newText != raw)
                    {
                        cache[key] = newText;
                        Balance.MasterVersions.MarkChanged(cache, key);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("translation", $"Skipped {key}: {ex.Message}");
                }
            }
            TranslatedCells = translated;
            RemainingJapaneseCells = remaining;
            RomajiCells = _romaji;
            _untranslated = _collect;
            _collect = null;
        }

        // Translates one table's text (used for tables added after startup, e.g. by event packs).
        public static string TranslateText(string raw)
        {
            if (!Enabled || LoadedEntries == 0 || string.IsNullOrEmpty(raw) || !JAPANESE.IsMatch(raw)) return raw;
            int a = 0, b = 0;
            return TranslateTableText(raw, ref a, ref b);
        }

        private static string TranslateTableText(string raw, ref int translated, ref int remaining)
        {
            var t = raw.TrimStart();
            if (t.StartsWith("{") || t.StartsWith("["))
            {
                JToken tok;
                try { tok = JToken.Parse(raw); }
                catch { return TranslatePipe(raw, ref translated, ref remaining); }
                bool any = false;
                int tr = translated, rem = remaining;
                Walk(tok, ref any, ref tr, ref rem);
                translated = tr; remaining = rem;
                return any ? tok.ToString(Formatting.None) : raw;
            }
            return TranslatePipe(raw, ref translated, ref remaining);
        }

        private static void Walk(JToken tok, ref bool any, ref int translated, ref int remaining)
        {
            switch (tok)
            {
                case JObject o:
                    foreach (var p in o.Properties().ToList())
                    {
                        if (p.Value.Type == JTokenType.String)
                        {
                            var s = p.Value.ToString();
                            // "tableData" holds a pipe/asterisk table
                            var ns = p.Name == "tableData" ? TranslatePipe(s, ref translated, ref remaining) : TranslateCell(s, ref translated, ref remaining);
                            if (!ReferenceEquals(ns, s) && ns != s) { p.Value = ns; any = true; }
                        }
                        else Walk(p.Value, ref any, ref translated, ref remaining);
                    }
                    break;
                case JArray a:
                    for (int i = 0; i < a.Count; i++)
                    {
                        if (a[i].Type == JTokenType.String)
                        {
                            var s = a[i].ToString();
                            var ns = TranslateCell(s, ref translated, ref remaining);
                            if (ns != s) { a[i] = ns; any = true; }
                        }
                        else Walk(a[i], ref any, ref translated, ref remaining);
                    }
                    break;
            }
        }

        private static string TranslateCell(string s, ref int translated, ref int remaining)
        {
            if (!JAPANESE.IsMatch(s)) return s;
            // A JSON string that itself contains a pipe table
            if (s.Contains('|') || s.Contains('*')) return TranslatePipe(s, ref translated, ref remaining);
            _usedFallback = false;
            var r = Translate(s, out var changed);
            if (changed) translated++;
            if (JAPANESE.IsMatch(r)) { remaining++; Note(s); }
            else if (_usedFallback) { _romaji++; Note(s); }
            return changed ? r : s;
        }

        private static readonly Regex HIRAGANA_ONLY = new(@"^[ぁ-ゟー]+$", RegexOptions.Compiled);

        // Columns in which (nearly) every value is plain hiragana hold the reading of another column.
        private static HashSet<int> ReadingColumns(string[] rows)
        {
            var result = new HashSet<int>();
            if (rows.Length < 20) return result;
            var hira = new Dictionary<int, int>();
            var filled = new Dictionary<int, int>();
            var distinct = new Dictionary<int, HashSet<string>>();
            foreach (var row in rows)
            {
                if (row.Contains('^') && !row.Contains('|')) continue;
                var cells = row.Split('|');
                for (int c = 0; c < cells.Length; c++)
                {
                    if (cells[c].Length == 0 || cells[c] == "0") continue;
                    filled[c] = filled.GetValueOrDefault(c) + 1;
                    if (!HIRAGANA_ONLY.IsMatch(cells[c])) continue;
                    hira[c] = hira.GetValueOrDefault(c) + 1;
                    if (!distinct.TryGetValue(c, out var set)) distinct[c] = set = new HashSet<string>();
                    set.Add(cells[c]);
                }
            }
            foreach (var kv in hira)
                // many different values ("なし" in every row is a label, not a reading)
                if (kv.Value >= 20 && kv.Value >= filled[kv.Key] * 0.9 && distinct[kv.Key].Count >= kv.Value / 2) result.Add(kv.Key);
            return result;
        }

        private static void Note(string original)
        {
            if (_collect == null) return;
            var key = original.Replace("\r", "");
            _collect[key] = _collect.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        // Every text that is still Japanese, most frequent first, as a dictionary file with empty values:
        // translate the values and put the file in Translations/<lang>/.
        public static string ExportUntranslatedJson()
        {
            var o = new JObject { ["_info"] = $"{_untranslated.Count} untranslated texts. Fill in the values and keep this file in Translations/{DataManager.Logic.DataManager.Language}/ - empty values are ignored." };
            foreach (var kv in _untranslated.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal))
                o[kv.Key] = "";
            return o.ToString(Formatting.Indented);
        }

        private static string TranslatePipe(string text, ref int translated, ref int remaining)
        {
            if (!JAPANESE.IsMatch(text)) return text;
            bool any = false;
            var rows = text.Split('*');
            var readings = ReadingColumns(rows);
            for (int r = 0; r < rows.Length; r++)
            {
                if (!JAPANESE.IsMatch(rows[r])) continue;
                // Most tables separate cells with |, a few (ywp_mst_stage_condition) with ^
                char sep = rows[r].Contains('|') || !rows[r].Contains('^') ? '|' : '^';
                var cells = rows[r].Split(sep);
                bool rowChanged = false;
                for (int c = 0; c < cells.Length; c++)
                {
                    if (!JAPANESE.IsMatch(cells[c])) continue;
                    // kana readings of the names ("じばにゃん"), used for sorting - not shown, left alone
                    if (readings.Contains(c) && HIRAGANA_ONLY.IsMatch(cells[c])) continue;
                    var orig = cells[c];
                    _usedFallback = false;
                    var nc = Translate(orig, out var changed);
                    if (changed) { cells[c] = nc; rowChanged = true; translated++; }
                    if (JAPANESE.IsMatch(nc)) { remaining++; Note(orig); }
                    else if (_usedFallback) { _romaji++; Note(orig); }
                }
                if (rowChanged) { rows[r] = string.Join(sep, cells); any = true; }
            }
            return any ? string.Join("*", rows) : text;
        }
    }
}
