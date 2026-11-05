# Puniemu (edited)

A server emulator for **Yo-kai Watch Puni Puni** and **Yo-kai Watch Wibble Wobble**, written in C# / .NET 8.

<p align="center">
<img src=https://i.imgur.com/zO49hMu.png alt=logo width=320>
</p>

This is a fork of [SuperTavor/puniemu](https://github.com/SuperTavor/puniemu) that collects the
improvements made in the community forks, fixes a long list of bugs and adds new features:

* **Event switcher** - choose which events are running, server-wide from the admin panel or per
  player from inside the game.
* **English translation layer** for the Japanese Puni Puni data.
* **Web admin panel** - players, gifts, bans, events, rates, maintenance mode, logs.
* **Built-in asset hosting**, HSP launching endpoint, Docker / Pterodactyl deployment.

## Disclaimers

This project is completely non-profit. In-app purchases are disabled.
It is in no way affiliated with LEVEL-5 or NHN. Only use game files you are legally allowed to use.
Game data (`Resources`, `dataDownload`) and APKs are **not** part of this repository.

---

## Quick start

### Requirements

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [PostgreSQL](https://www.postgresql.org/download/) (any recent version)
* The game's server data: a `Resources` folder (master tables) and optionally the `dataDownload`
  folder (assets)

### 1. Database

Create an empty database, e.g. `puniemu`. The tables are created automatically on the first start
(you can also run `Database/schema.sql` yourself).

### 2. Configuration

Copy `appsettings.example.json` to `appsettings.json` and fill in at least:

```json
{
  "PostgresConnectionString": "Host=localhost;Database=puniemu;Username=postgres;Password=CHANGEME",
  "IsWibWob": false,
  "GameVersion": "4.0.6",
  "PublicServerURL": "http://192.168.1.100:8080",
  "AdminToken": "a-long-random-secret"
}
```

Every setting is explained in [Configuration](#configuration). Any setting can also be passed as an
environment variable (handy for Docker).

### 3. Game data

```
puniemu-edited/
├── Resources/                ← ywp_mst_*.txt, gacha_pool.txt, stage_data.txt, ...
└── wwwroot/dataDownload/     ← assets (optional, see "Data download")
```

`Resources` is read from disk at startup, so you don't have to rebuild the server after changing it
(it is also still embedded at build time like before).

### 4. Run

```
dotnet run -c Release
```

The console shows how many game tables were loaded, the translation and event status and the admin
panel URL. Check `http://localhost:8080/status` in a browser.

### Docker

```
cp .env.example .env        # set POSTGRES_PASSWORD, ADMIN_TOKEN, ...
docker compose up -d --build
```

`docker-compose.yml` starts PostgreSQL and Puniemu and mounts `./Resources`, `./wwwroot`,
`./Events` and `./Translations`. A Pterodactyl/Pelican egg is in `deploy/egg-puniemu.json` and a
Render.com blueprint in `deploy/render.yaml`. Hosting platforms that pass the port in `$PORT`
(Render, Railway, Fly.io, Heroku) are supported automatically.

---

## Connecting the game

The client has to be patched to talk to your server instead of NHN's.

### Wibble Wobble (summary of the community guide)

1. **Server URL in `libSGF.so`.** Open the APK (e.g. with APK Editor Studio), open
   `lib/armeabi-v7a/libSGF.so` in a hex editor and search for `https://`. Overwrite the game server
   host (`s-ywp-us-gameserver.hangame.co.jp`, or `wibwob.ddns.net` in Reloaded builds) with your
   own host **without changing the length** - the slot is 33 characters, pad with `/` if your host is
   shorter. Repeat for `lib/x86`.
2. **`res/xml/hsp_launching_zone.xml`** - set every `address="https://YOUR_HOST/hsp"`.
   Puniemu answers the launching request itself (`/hsp/...`), so the game gets past the
   maintenance screen.
3. Rebuild and sign the APK (e.g. `java -jar uber-apk-signer.jar -a game.apk --allowResign`).

[TheC0mmand/WibWobPadaboom](https://github.com/TheC0mmand/WibWobPadaboom) ships an APK builder and an
installer that automate this for Wibble Wobble, and
[WibWobPS/wwpatcher](https://github.com/WibWobPS/wwpatcher) is a web tool that rewrites the launching
zone of an `.apk` or `.ipa` and resigns it. Puniemu answers the launching request those patched
builds make (`/hsp/...getLaunchingInfos`).

For a public server put a reverse proxy with TLS in front of Puniemu (and set
`"TrustProxyHeaders": true`). Tunnels such as ngrok work too if the host fits into the 33 characters.

### Puni Puni

Same idea (server URL + launching zone), plus the protections of the Japanese client. Not covered
here: in the 4.177.0 client the game server's host name is not stored as plain text anywhere (it
is built at run time inside `libSGF.so`), so it cannot simply be replaced.

[docs/EMULATOR.md](docs/EMULATOR.md) shows how to run the unmodified 4.177.0 client against Puniemu on
an Android emulator (hosts redirect + your own CA, `Tools/make_certs.sh`).

[docs/ARCHIVES.md](docs/ARCHIVES.md) lists where old game data survives (archive.org uploads of
event caches, Wibble Wobble server data...) and how to get it.

[docs/PROTOCOL.md](docs/PROTOCOL.md) has the request fields and response parts of 107 server calls,
read out of the unpacked Wibble Wobble 2.2.6 client with `Tools/extract_protocols.py` (the 4.177.0
client's code is encrypted).

[docs/CLIENT_ENDPOINTS.md](docs/CLIENT_ENDPOINTS.md) lists all 164 endpoints the 4.177.0 client
knows and which ones Puniemu answers. Requests to the others are recorded with their decrypted body
(admin panel -> Logs -> Unimplemented requests), so they can be implemented from real traffic.

---

## Features

### Event switcher

Puni Puni ran hundreds of events, but the official server only ever sent the events running *at that
moment* - so no data dump contains all of them. Puniemu builds an event catalog from everything that
**is** in your data:

* the rows of `ywp_mst_event`,
* the event maps in `ywp_mst_map`,
* the leftovers of past events in the other event tables (`ywp_mst_event_quest`,
  `ywp_mst_event_stage_assist`, `ywp_mst_event_youkai_assist`, `ywp_mst_event_point_trade`, ...),
* **event packs** you drop into `Events/` (JSON) to re-create or complete events.

When an event selection is active, the server rewrites the event tables it sends: selected events
get their `ywp_mst_event` row and their maps / quests / bonuses are re-opened around today, the maps
of the other events are closed. Normal story maps are never touched and `Resources` is never changed.

How to choose:

* **Admin panel → Events**: "Original data" (as shipped), "Custom" with a tick list, or
  **"Rotation"** - the server cycles through the events by itself like the live game did
  (e.g. 2 events at a time, changing every 7 days; uses the ticked events, or every playable one).
* **In game**: open **Help** in the menu and tap the **Events & Server** button. Every player can
  pick their own events (can be disabled in the admin panel). Return to the title screen afterwards.

An **event history** of the live game (151 events in 26 series) is included: players can browse it in
game, and linking an entry to event ids in the admin panel gives those events their real English
names. The lists also show each event's original dates and, when the data has them, the characters that appear
in the event's story, to help recognise unnamed events. See [Events/README.md](Events/README.md) for
event packs and friendly names (`Events/names.json`).

**More past events from old dumps:** extract all the dumps you have (for example the community
archive of past Puni Puni dumps) into one folder and run

```
python Tools/import_dumps.py --current Resources --dumps path/to/extracted/dumps --out Events/imported
```

It writes one event pack per past event that your current data doesn't have (event row, maps,
quests, bonuses, story) plus a data-only pack with the Yo-kai, stages, enemies and items those events
need. Restart the server or press **Rescan data**: the events show up in both event lists. The dump
files are only parsed as data, never executed.

### English translation

Set `"Language": "en"`. Every JSON dictionary in `Translations/en/` is applied to the game data
before it is sent (exact text matches, optional phrase replacements). The tool
`Tools/build_translation.py` builds a complete dictionary by lining up the Japanese Puni Puni tables
with the English Wibble Wobble tables (same ids). Everything that is left can be exported from the
admin panel and machine-translated offline with `Tools/machine_translate.py` (a local
Japanese -> English model, it keeps Yo-kai names, line breaks and placeholders), so the whole game
is English; untranslated katakana names fall back to romaji. Collab characters get their official
English names from AniList (`Tools/fetch_anime_names.py`: Lelouch Lamperouge, Shinji Ikari, Rem...). See [Translations/README.md](Translations/README.md).

**Translated images and sounds (works on Android too):** the
[YWP-Translation](https://github.com/Lit8tech/YWP-Translation) project made English versions of
about 9,600 of the game's image and sound files, but only ships them as an iOS mod. Puniemu can send
them to every client through the data download: copy (or `git clone`) that project into an
`AssetOverlay/` folder next to the server. On startup every file that replaces a data download file
gets a new version, so clients download it again; requests for it are answered with the overlay
copy. Files identical to the server's own copy are skipped, so players only re-download what really
changed (on a Wibble Wobble server most of that project's files are already the English originals).
The replacement files must be in the game's own format (plain PNG, the game's encrypted `.m4`/`.ez`...),
as the YWP-Translation files are; [pqr29/wibwob_assets_encryptor](https://github.com/pqr29/wibwob_assets_encryptor)
and [simontime/fictional-couscous](https://github.com/simontime/fictional-couscous) convert to and from
that format. Any other replacement files work the same way (files are matched by name). Requires the
client to download its assets from this server (`ServeDataDownload`).

All server messages, logs and code comments are in English.

### Admin panel

Set `AdminToken` and open `http://your-server:8080/admin`, enter the token.

| Tab | What you can do |
| --- | --- |
| Overview | online players, accounts, uptime, memory, requests and errors per endpoint |
| Players | search by name / friend code / user id / gdkey, look up Yo-kai / item ids by name, give or take Y-Money, spirits, items, Yo-kai, rename, export / import a save (backup or move it to another account), ban / unban |
| Events | the event switcher (see above) |
| Gifts | server-wide gifts - every player receives them once on their next login |
| Settings | Y-Money and EXP multipliers, infinite spirits, news text, maintenance mode, save all accounts now |
| Logs | the last 300 server log lines |

Runtime settings are stored in `server_state.json`. The API behind the panel is under
`/admin/api/*` (token in the `X-Admin-Token` header).

### In-game wiki

In game: **Help → Wiki**. Everything is built from the server's own game data (so it always matches
the server, and is in English when the data is English or translated) plus hand-written guides:

* **Yo-kai** - search and filter by rank and tribe; each page shows the picture, rank, tribe,
  favourite food, HP / attack, Soultimate, bonus effect, evolution and **where to befriend it**
* **Maps & stages** - every stage with its 3 star conditions, enemies, boss / hidden markers,
  recommended level and hidden-stage unlock conditions (when the data has them)
* **Items**, **missions** (with rewards) and **Crank-a-kai** machines with price, odds per rank and
  every Yo-kai inside
* **15 guides**: getting started, battles & combos, befriending, ranks & tribes, levels & evolution,
  stars & hidden stages, items, crank-a-kai, missions, Score Attack, watches, events, friends,
  keeping your save safe, and a **Japanese → English glossary** for the Japanese app
* **This server** (current rates and rules) and links to community sites (Mt. Wildwood, Game8,
  Yo-kai Watch España, the Yo-kai Watch Fandom wiki...)

Pictures come from the data download (`bl_<id>.png` and similar), so they appear when
`ServeDataDownload` has the files.

### Balance mode (old Yo-kai catch up)

In the master data every Yo-kai of a rank has exactly the same total stats (all Z: 1933 max HP + ATK,
all UZ: 2900, all UZ+: 3330); the power creep comes from the new ranks (ZZ 2020, ZZZ 2021, UZ 2023,
UZ+ 2025). `"BalanceStrength"` moves every rank toward the top rank: `0` = original, `0.5` closes
half of the gap (rank D x4.3, S x2.2, Z x1.5, UZ x1.16), `1` gives every Yo-kai top-rank stats. HP and
attack keep their ratio, enemies are not changed, Yo-kai you already own get the new stats at their
next level-up. Changed tables get a new master version, so clients download them again.

### Crank-a-kai: every machine, coins and mixes

Every crank-a-kai of the master data works (338 on the live 4.177.0 data). Real pools: Japan requires gacha
games to publish their odds, and `Tools/fetch_gacha_rates.py` reads Puni Puni's official odds pages
(one per coin, linked from `ywp_mst_coin_purchase_master`) into `gacha_pool.txt` - every Yo-kai with
its exact chance (41 pages / 42 machines on 4.177.0; the "pick a Yo-kai" coins have no odds page):

```
python Tools/fetch_gacha_rates.py --resources Resources --out Resources/gacha_pool.txt --merge
```
 The official servers keep
the drop pools to themselves, so a machine without a captured pool (`gacha_pool.txt`) gets a lineup
rebuilt from the master data: its "pick a Yo-kai" list (`ywp_mst_gacha_youkai_choice`) where it has
one, otherwise the Yo-kai released in the three weeks after the machine came out (the Monster Strike
coin gets Lucifer, Pandora, Moses...). They are added with a rate-up on top of the regular Yo-kai
of a machine with the same price.

In-game **Wiki -> Crank-a-kai** lists every machine with its lineup and odds, and:

* **Get a coin** - coin machines need their coin; players get some free each day (or buy them for
  Y-Money, or not at all - admin panel -> Settings -> Crank-a-kai),
* **My mix** - add any machines to your mix and your normal Y-Money crank-a-kai draws from all of
  them together (each machine counts the same). The server owner can set a mix for everyone.

The page identifies players by the user id the game puts in the Help page link. Anybody who knows a
player's id could use that player's free coins or mix, which is harmless; with "for Y-Money" they
could also spend that player's Y-Money, so only use that mode with people you trust.

### In-game Event Guide

In game: **Help → Event Guide** (also linked from the notice page and the Events & Server page).
A mobile guide to every event of the live game, grouped by series, with search (events, series,
characters) and a "playable here" filter. Each event page shows:

* its featured Yo-kai and characters (pictures from the community Drive archive, linked not copied;
  tap to enlarge) and a link to all its pictures,
* the fan translation of its story, when there is one,
* for events this server has data for: the original dates, the **bonus Yo-kai** and their effects,
  the **event missions** and maps (taken from the master data), and a **Play this event** button
  that switches it on for that player,
* tips, plus a "How events work" help page.

Link history entries to event ids (admin panel → Events → Event history) to unlock the data parts.
You can also add your own `"notes"` to any event in `Events/history.json`.

### In-game pages

* `/help/inquiry/top.nhn` - the game's help page (`help.html` from the data download) with an added
  **Events & Server** button.
* `/web/portal.nhn` - player portal: news and personal event selection.
* `/web/guide.nhn` - the Event Guide.
* `/web/wiki.nhn` - the wiki.
* `/web/noticeBannerPage.nhn`, `/web/viewpage.nhn?webviewId=...` - notice pages from `Web/Page/`
  (see [Web/README.md](Web/README.md)).

### Data download

If `wwwroot/` or the folder in `DataDownloadPath` exists, Puniemu serves the game's asset files
itself (with the right content types for `.ez`, `.m4`, `.ayd`, ...). Requests are matched to the exact
file first (`/dataDownload/...` and `/eal/...`); if that fails, by file name, using the newest version
folder when a file exists in several versions. If `DataDownloadURL` is
empty, it defaults to `PublicServerURL + /dataDownload/`.

### Gameplay

* **Oni Fever** gauge for the Wibble Wobble gacha (WibWobYT).
* Food is refused instead of wasted when the Yo-kai's amiability is already maxed (WibWobYT).
* Y-Money / EXP multipliers and infinite spirits (admin panel).
* Server-wide gifts.

### Security and stability

* Whole request bodies are buffered before the handlers read them. The handlers read the body with a
  single `BodyReader.ReadAsync()`, which only returned the first chunk of a large request.
* Requests for the same save are processed one at a time, so firing requests in parallel can no
  longer duplicate currency.
* A crashing request returns an error dialog in game instead of dropping the connection, and is
  logged with the full exception.
* Bans and maintenance mode are enforced on every request.
* Optional check that the device sending a request owns the save (`AccountOwnershipMode`).
* Per-IP rate limit (`RateLimitPerMinute`).
* Account link/restore codes: cryptographic RNG, 15 minute expiry, lockout after 5 wrong codes, and
  an answer when a code is wrong (before, the game got no response).
* `max_gdkeys` per device is actually enforced; missing device rows are created instead of leaving
  orphaned saves.
* `|` and `*` are stripped from player names (they are the save-table separators and corrupted the
  save).
* The account cache no longer loads the same account twice under concurrency and no longer evicts
  accounts that are still in use (both could silently lose progress). The last login time is saved.
* Path traversal in the web view page id is rejected.

### Fixed game logic bugs

* The befriend check looked up the soultimate level by *enemy* id, so maxed Yo-kai still got a
  befriend roll (WibWobYT).
* Login stamps compared against 8400 seconds instead of a day (86400).
* Score attack overwrote the stored best score with a hardcoded value before comparing, resetting
  the record every run.
* The Goku and Crystal menus sent placeholder player data ("superog", "random") and a hardcoded
  master version, and always used the device's first save.
* The collect menu filled the effect table using the reward table's indexes.
* ExecuteGacha crashed on an unknown gacha or when the player had none of the price item.

---

## Configuration

All keys are optional except `PostgresConnectionString` and `IsWibWob`.

| Key | Default | Meaning |
| --- | --- | --- |
| `PostgresConnectionString` | - | Npgsql connection string |
| `IsWibWob` | - | `true` for Wibble Wobble, `false` for Puni Puni |
| `GameVersion` | - | client version that may connect |
| `StrictVersionCheck` | `true` | `false` accepts any client version |
| `AllowedGameVersions` | `""` | extra accepted versions, comma separated |
| `ServerName` | `Puniemu` | shown in dialogs and pages |
| `PublicServerURL` | `""` | public base URL (launching info, default data download URL) |
| `DataDownloadURL` | `""` | where the client downloads assets from |
| `ServeDataDownload` | `true` | serve `wwwroot/` / `DataDownloadPath` from this server |
| `DataDownloadPath` | `wwwroot/dataDownload` | asset folder served under `/dataDownload` |
| `AdminToken` | `""` | enables the admin panel |
| `Language` | `""` | `en` = translate the game data with `Translations/en` |
| `RomajiFallback` | `true` | write untranslated katakana names in romaji |
| `BalanceStrength` | `0` | 0-1: raise lower ranks' stats toward the top rank |
| `TranslationsPath` / `EventsPath` / `WebPath` / `AssetOverlayPath` | `Translations` / `Events` / `Web` / `AssetOverlay` | folders |
| `ServerStatePath` | `server_state.json` | runtime settings file |
| `AccountOwnershipMode` | `log` | `off`, `log` or `enforce` |
| `RateLimitPerMinute` | `900` | per IP, `0` = off |
| `TrustProxyHeaders` | `false` | trust `X-Forwarded-*` (only behind your own proxy) |
| `MaxGdkeysPerDevice` | `3` | saves per device |
| `SkipOpeningTutorial` | `false` | new saves skip the opening tutorial |
| `MaxConnections` / `MaxCachedAccounts` | `1500` / `2000` | capacity limits |
| `LogRequests` | `false` | print every decrypted request (debugging) |
| `EmailForAuthMessages` / `AppPasswordForAuthMessages` | `""` | Gmail account used to send account link codes |

Useful URLs: `/healthz` (liveness), `/status` (JSON status), `/admin` (admin panel).

---

## Credits

Original Puniemu:

- Zura - Main dev
- DarkCraft - Main dev
- wibwob_yt - Dev
- onepiecefreak3 - Reverse engineering help
- kuronosuFear - Reverse engineering help
- picky_x_keizen - Logo

Community forks this edition takes features and fixes from:

- **WibWobYT** - Oni Fever, befriend fix for maxed Yo-kai, max amiability food check
- **BobFlar / Nohan** - in-game web view pages
- **marlonnblox-netizen** - HSP launching info endpoint, forwarded headers
- **WibWobPS (hxgohxrr)** - the wwps port: launching template, security hardening, admin tooling and
  dashboard ideas, and the list of server bugs it documented
- **AndroidAbuse**, **Julian52575** - Docker setups
- **Washaaaan** - data download serving, Render deployment, skipping the opening tutorial
- **eliasderzocker** - Pterodactyl egg
- **Rorosak** - FoodType enum
- **Amumis** - Wibble Wobble server guide
- **Lit8tech and the YWP-Translation team** - the English images/sounds the asset overlay can serve
- **cursedmono** - WWR Explorer (idea for the admin id lookup)
- **TheC0mmand** - WibWobPadaboom (Wibble Wobble tooling and English docs)
