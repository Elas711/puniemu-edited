# Translations

Yo-kai Watch Puni Puni's master data (Yo-kai names, items, maps, missions, dialogue...) is Japanese.
Puniemu can translate it before sending it to the game.

1. Set `"Language": "en"` in `appsettings.json`.
2. Put dictionaries in `Translations/en/`. Every `*.json` file there is loaded:
   * `{"ジバニャン": "Jibanyan"}` - replaces text cells that match exactly,
   * files whose name starts with `phrases` are applied as substring replacements afterwards
     (longest first). Use them carefully, they can produce mixed sentences.
   * files whose name starts with `patterns` hold regular expressions for numbered texts
     (`"^ステージ([0-9]+)$": "Stage $1"`); captured parts are translated with the other dictionaries.
3. Restart the server. The console and the admin panel show how many texts were translated.

## Building a full dictionary from Wibble Wobble

Wibble Wobble (the international version) uses the same tables and ids as Puni Puni, so its English
data can be lined up with the Japanese data automatically:

```
python Tools/build_translation.py build --jp path/to/Resources_PuniPuni --en path/to/Resources_WibWob \
       --out Translations/en/strings.generated.json
```

Content that only exists in Puni Puni (it ran for years longer) has no English version. Export it,
translate it (by hand or with any translation tool) and keep the file in this folder:

```
python Tools/build_translation.py extract --jp Resources --translations Translations/en \
       --out Translations/en/untranslated.json
python Tools/build_translation.py stats --jp Resources --translations Translations/en
```

## Translating everything that is left (machine translation)

Whatever no dictionary covers can be machine-translated offline, so every text in the game is English:

1. Admin panel -> **Settings** -> **Download untranslated texts** (or
   `GET /admin/api/translation/untranslated` with the `X-Admin-Token` header). The file lists every text
   that is still Japanese, most frequent first, as a dictionary with empty values.
2. Download a CTranslate2 Japanese -> English model, for example
   * [Sugoi v4](https://huggingface.co/entai2965/sugoi-v4-ja-en-ctranslate2) - trained on game / anime
     dialogue, clearly the best for this game. NTT licence: non-commercial / research use only.
   * [FuguMT ja-en](https://huggingface.co/shift-ykurami/fugumt-ja-en-ct2-f32) - CC BY-SA 4.0.
   * [Argos Translate ja_en](https://argos-net.com/v1/translate-ja_en-1_1.argosmodel) - MIT (unzip it), weakest.
3. Run the tool (`pip install ctranslate2 sentencepiece`, CPU only, no GPU needed):

   ```
   python Tools/machine_translate.py --input untranslated.json --model sugoi-v4-ja-en-ctranslate2 \
          --translations Translations/en --out Translations/en/machine.generated.json
   ```

   Before translating it puts in the English names it already knows (every dictionary in
   `Translations/en` plus the game terms in `Tools/translation_glossary.json`, e.g. いりょく -> Power,
   わざゲージ -> Soultimate gauge), so Yo-kai and game words come out right. Dialogue keeps its pages
   (▽), line breaks are redone to fit the text boxes, `<USERNAME>` placeholders and `<R>` highlights
   are kept, and texts that only differ in their numbers are translated once. It can be stopped and
   continues where it stopped. About 26,000 texts take 10-20 minutes on a normal CPU.
4. Restart the server. `machine*.json` files are loaded first, so every other dictionary overrides
   them (and the patterns are tried before them): to fix a bad machine translation, put the corrected text in any other file.

Two automatic fallbacks need no tool at all:

* katakana names nobody translated are written in romaji (`ナツネ` -> `Natsune`); turn it off with
  `"RomajiFallback": false`,
* kana reading columns (`じばにゃん`, used for sorting) are recognised and left alone.

`|` and `*` are table separators and are removed from translations automatically.
Files shipped in `Translations/en/`:

* `common.json` - small hand-made starter dictionary (main characters, tribes, UI words),
* `names.fandom.json` - about 3,900 Japanese -> English Yo-kai / character / item names from the
  [Yo-kai Watch Fandom wiki](https://yokaiwatch.fandom.com) (CC BY-SA 3.0). Refresh it with
  `python Tools/fetch_fandom_names.py --out Translations/en/names.fandom.json`,
* `names_anime.json` - official English names of collab characters (Levi, Lelouch Lamperouge,
  Frieren, Conan Edogawa...) from [AniList](https://anilist.co): the tool reads the characters of every
  collab series and keeps the names that are Yo-kai in the game data (character Yo-kai only, official
  Wibble Wobble names are never replaced, a given name alone only fills a gap). Refresh it with
  `python Tools/fetch_anime_names.py --resources Resources --out Translations/en/names_anime.json`;
  add a collab to `SERIES` in the tool. Rerun the machine translation afterwards so dialogue uses them,
* `patterns.json` - rules for numbered texts: stage names, star conditions, missions, crank-a-kai
  stamp bonuses...

Files are loaded in alphabetical order and later files win, so a `strings.generated.json` built
from the official Wibble Wobble text overrides the Fandom names where both exist.

Result on the live Puni Puni 4.177.0 data with a dictionary generated from Wibble Wobble:
about 87,000 of 178,000 Japanese texts are shown in English, including every stage / star condition
pattern and about 2,400 of the 4,300 Yo-kai names.
