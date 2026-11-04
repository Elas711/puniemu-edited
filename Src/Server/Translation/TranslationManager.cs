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
        private static readonly Regex JAPANESE = new(@"[぀-ヿ㐀-鿿ｦ-ﾟ]", RegexOptions.Compiled);

        private static Dictionary<string, string> _exact = new();
        private static List<KeyValuePair<string, string>> _phrases = new();

        public static int LoadedEntries => _exact.Count + _phrases.Count;
        public static int TranslatedCells { get; private set; }
        public static int RemainingJapaneseCells { get; private set; }
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
            var phrases = new Dictionary<string, string>();
            foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x))
            {
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file, Encoding.UTF8)) ?? new();
                    var target = Path.GetFileName(file).StartsWith("phrases", StringComparison.OrdinalIgnoreCase) ? phrases : exact;
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
            _phrases = phrases.OrderByDescending(x => x.Key.Length).ToList();

            ApplyToGameData();
            Log.Info("translation", $"Language '{lang}': {exact.Count} strings + {phrases.Count} phrases loaded, {TranslatedCells} texts translated, {RemainingJapaneseCells} still Japanese.");
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
            if (_phrases.Count > 0 && JAPANESE.IsMatch(result))
            {
                foreach (var p in _phrases)
                {
                    if (result.Contains(p.Key)) result = result.Replace(p.Key, p.Value);
                }
            }
            changed = result != text;
            return result;
        }

        private static void ApplyToGameData()
        {
            var cache = DataManager.Logic.DataManager.GameDataManager.GamedataCache;
            int translated = 0, remaining = 0;
            foreach (var key in cache.Keys.ToList())
            {
                var raw = cache[key];
                if (string.IsNullOrEmpty(raw) || !JAPANESE.IsMatch(raw)) continue;
                try
                {
                    var newText = TranslateTableText(raw, ref translated, ref remaining);
                    if (!ReferenceEquals(newText, raw)) cache[key] = newText;
                }
                catch (Exception ex)
                {
                    Log.Warn("translation", $"Skipped {key}: {ex.Message}");
                }
            }
            TranslatedCells = translated;
            RemainingJapaneseCells = remaining;
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
            var r = Translate(s, out var changed);
            if (changed) translated++;
            if (JAPANESE.IsMatch(r)) remaining++;
            return changed ? r : s;
        }

        private static string TranslatePipe(string text, ref int translated, ref int remaining)
        {
            if (!JAPANESE.IsMatch(text)) return text;
            bool any = false;
            var rows = text.Split('*');
            for (int r = 0; r < rows.Length; r++)
            {
                if (!JAPANESE.IsMatch(rows[r])) continue;
                var cells = rows[r].Split('|');
                bool rowChanged = false;
                for (int c = 0; c < cells.Length; c++)
                {
                    if (!JAPANESE.IsMatch(cells[c])) continue;
                    var nc = Translate(cells[c], out var changed);
                    if (changed) { cells[c] = nc; rowChanged = true; translated++; }
                    if (JAPANESE.IsMatch(nc)) remaining++;
                }
                if (rowChanged) { rows[r] = string.Join("|", cells); any = true; }
            }
            return any ? string.Join("*", rows) : text;
        }
    }
}
