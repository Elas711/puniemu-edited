using System.Text;
using System.Text.RegularExpressions;

namespace Puniemu.Src.Server.Translation
{
    // Last-resort fallback for names nobody has translated yet: kana-only texts ("ナツネ",
    // "ガシャドクロ") are written in Hepburn romaji ("Natsune", "Gashadokuro") instead of being shown in
    // Japanese. Only short name-like texts are converted, sentences are left to the dictionaries.
    public static class Romaji
    {
        private static readonly Regex NAME_LIKE = new(@"^[ぁ-ゟァ-ヿｦ-ﾟ・ー～〜 　A-Za-z0-9Ａ-Ｚａ-ｚ０-９\-\(\)（）&＆!！?？]+$", RegexOptions.Compiled);
        private static readonly Regex KANA = new(@"[ぁ-ゟァ-ヿｦ-ﾟ]", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> MAP = Build();

        private static Dictionary<string, string> Build()
        {
            var basic = new (string kana, string roma)[]
            {
                ("あ","a"),("い","i"),("う","u"),("え","e"),("お","o"),
                ("か","ka"),("き","ki"),("く","ku"),("け","ke"),("こ","ko"),
                ("さ","sa"),("し","shi"),("す","su"),("せ","se"),("そ","so"),
                ("た","ta"),("ち","chi"),("つ","tsu"),("て","te"),("と","to"),
                ("な","na"),("に","ni"),("ぬ","nu"),("ね","ne"),("の","no"),
                ("は","ha"),("ひ","hi"),("ふ","fu"),("へ","he"),("ほ","ho"),
                ("ま","ma"),("み","mi"),("む","mu"),("め","me"),("も","mo"),
                ("や","ya"),("ゆ","yu"),("よ","yo"),
                ("ら","ra"),("り","ri"),("る","ru"),("れ","re"),("ろ","ro"),
                ("わ","wa"),("ゐ","i"),("ゑ","e"),("を","o"),("ん","n"),
                ("が","ga"),("ぎ","gi"),("ぐ","gu"),("げ","ge"),("ご","go"),
                ("ざ","za"),("じ","ji"),("ず","zu"),("ぜ","ze"),("ぞ","zo"),
                ("だ","da"),("ぢ","ji"),("づ","zu"),("で","de"),("ど","do"),
                ("ば","ba"),("び","bi"),("ぶ","bu"),("べ","be"),("ぼ","bo"),
                ("ぱ","pa"),("ぴ","pi"),("ぷ","pu"),("ぺ","pe"),("ぽ","po"),
                ("ぁ","a"),("ぃ","i"),("ぅ","u"),("ぇ","e"),("ぉ","o"),("ゃ","ya"),("ゅ","yu"),("ょ","yo"),("ゎ","wa"),("ゔ","vu"),
                ("きゃ","kya"),("きゅ","kyu"),("きょ","kyo"),("しゃ","sha"),("しゅ","shu"),("しょ","sho"),("しぇ","she"),
                ("ちゃ","cha"),("ちゅ","chu"),("ちょ","cho"),("ちぇ","che"),("にゃ","nya"),("にゅ","nyu"),("にょ","nyo"),
                ("ひゃ","hya"),("ひゅ","hyu"),("ひょ","hyo"),("みゃ","mya"),("みゅ","myu"),("みょ","myo"),
                ("りゃ","rya"),("りゅ","ryu"),("りょ","ryo"),("ぎゃ","gya"),("ぎゅ","gyu"),("ぎょ","gyo"),
                ("じゃ","ja"),("じゅ","ju"),("じょ","jo"),("じぇ","je"),("びゃ","bya"),("びゅ","byu"),("びょ","byo"),
                ("ぴゃ","pya"),("ぴゅ","pyu"),("ぴょ","pyo"),
                ("ふぁ","fa"),("ふぃ","fi"),("ふぇ","fe"),("ふぉ","fo"),("てぃ","ti"),("でぃ","di"),("とぅ","tu"),("どぅ","du"),
                ("うぃ","wi"),("うぇ","we"),("うぉ","wo"),("ゔぁ","va"),("ゔぃ","vi"),("ゔぇ","ve"),("ゔぉ","vo"),
                ("つぁ","tsa"),("つぇ","tse"),("つぉ","tso"),("いぇ","ye"),("でゅ","dyu"),("てゅ","tyu"),
            };
            var map = new Dictionary<string, string>();
            foreach (var (kana, roma) in basic)
            {
                map[kana] = roma;
                // the same in katakana (U+30A1.. = U+3041.. + 0x60)
                map[new string(kana.Select(ch => ch >= 'ぁ' && ch <= 'ゖ' ? (char)(ch + 0x60) : ch).ToArray())] = roma;
            }
            map["ヴ"] = "vu"; map["ヷ"] = "va";
            return map;
        }

        private static readonly Regex HIRAGANA = new(@"[ぁ-ゟ]", RegexOptions.Compiled);

        // Short katakana texts: names and loanwords. Hiragana texts are real words ("つよい") and are left
        // to the dictionaries / machine translation.
        public static bool IsNameLike(string text) =>
            text.Length <= 24 && KANA.IsMatch(text) && !HIRAGANA.IsMatch(text) && NAME_LIKE.IsMatch(text);

        public static string Convert(string text)
        {
            text = text.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder();
            bool sokuon = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == 'っ' || ch == 'ッ') { sokuon = true; continue; }
                string? roma = null;
                if (i + 1 < text.Length && MAP.TryGetValue(text.Substring(i, 2), out var two)) { roma = two; i++; }
                else if (MAP.TryGetValue(ch.ToString(), out var one)) roma = one;
                if (roma != null)
                {
                    if (sokuon) { roma = (roma.StartsWith("ch") ? "t" : roma[0].ToString()) + roma; sokuon = false; }
                    sb.Append(roma);
                    continue;
                }
                sokuon = false;
                if (ch == 'ー')
                {
                    // long vowel mark: dropped, like most official romanisations ("ジバニャーン" -> "Jibanyan")
                    continue;
                }
                sb.Append(ch switch { '・' => ' ', '～' or '〜' => '~', '　' => ' ', _ => ch });
            }
            // capitalise every word
            var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length > 0 && char.IsLower(w[0]) ? char.ToUpperInvariant(w[0]) + w[1..] : w);
            return string.Join(" ", words);
        }
    }
}
