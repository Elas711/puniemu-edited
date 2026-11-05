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
`common.json` is a small hand-made starter dictionary (main characters and tribes).
