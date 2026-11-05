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

`|` and `*` are table separators and are removed from translations automatically.
Files shipped in `Translations/en/`:

* `common.json` - small hand-made starter dictionary (main characters, tribes, UI words),
* `names.fandom.json` - about 3,900 Japanese -> English Yo-kai / character / item names from the
  [Yo-kai Watch Fandom wiki](https://yokaiwatch.fandom.com) (CC BY-SA 3.0). Refresh it with
  `python Tools/fetch_fandom_names.py --out Translations/en/names.fandom.json`,
* `patterns.json` - rules for numbered texts: stage names, star conditions, missions...

Files are loaded in alphabetical order and later files win, so a `strings.generated.json` built
from the official Wibble Wobble text overrides the Fandom names where both exist.

Result on the live Puni Puni 4.177.0 data with a dictionary generated from Wibble Wobble:
about 87,000 of 178,000 Japanese texts are shown in English, including every stage / star condition
pattern and about 2,400 of the 4,300 Yo-kai names.
