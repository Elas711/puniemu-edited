# Notes for AI assistants working on this repo

Read this first; it replaces rediscovering the project. Keep it short and current.

## What this is
Puniemu: C# / .NET 8 (ASP.NET minimal API) server emulator for Yo-kai Watch Puni Puni (JP, client
4.177.0) and Wibble Wobble. PostgreSQL (Npgsql) stores saves. Game data (`Resources/`,
`dataDownload/`, APKs, dumps, generated translations of game text) is **never committed**.

## Build / run / test
* `dotnet build -v q` (warnings are normal), run `dotnet bin/Debug/net8.0/Puniemu.dll`.
* Config: `appsettings.json` (template `appsettings.example.json`) or env vars
  (`PUNIEMU_RESOURCES_PATH`, `PostgresConnectionString`, `IsWibWob=false`, `GameVersion=4.177.0`,
  `Language=en`, `AdminToken`, `DataDownloadPath`, `TranslationsPath`, `EventsPath`,
  `ASPNETCORE_URLS`). PostgreSQL must be running (`service postgresql start` in containers).
* Smoke test against a running server: `python Tools/test_server.py --url http://127.0.0.1:5000 --admin-token <t> --jp`
  (new account, battles, all main screens, gacha pull, web APIs; exit 1 on failure). Run it after every change.
* Client protocol: AES-ECB, key `a865d7e5e2458f8ce1b5ecd087e54594`, SHA1 digest salt `0bk2kvtFE2`,
  gzip responses (see `Src/NHNCrypt`). A Python client to drive the server: encrypt like `NHNCrypt`,
  POST to `/<endpoint>.nhn`.
* Asset ciphers: `.ez` / `.ayd` inner file = AES-CBC (IV = first 16 bytes) key
  `2AB511F477977D25CF6F7A8AE049A125`; `.og`/`.m4` key `80F008394EB02F4FC7F5A5C235C42918`.
  `.ayd` = zip -> encrypted zip of PNGs (`Src/Server/Web/PackedAssets.cs`). `.cud` (client master
  cache) key unknown (per device, in encrypted 4.x code).

## Where things are
* Endpoints: `Src/Program.cs` maps `/<name>.nhn`; handlers in `Src/Server/GameServer/Requests/*`.
  Unknown requests: `DefaultHandler` (silent OK list + recorder, admin -> Logs).
* `docs/CLIENT_ENDPOINTS.md` (164 endpoints of 4.177.0, which are implemented),
  `docs/PROTOCOL.md` (request fields / response keys of 107 calls from the unpacked WibWob 2.2.6
  client, via `Tools/extract_protocols.py`) - use these to implement missing endpoints.
* Events: `Src/Server/Events` (catalog, selection modes, packs, history.json for the Event Guide).
* Translation: `Src/Server/Translation` (dictionaries, patterns, machine*.json lowest priority,
  romaji fallback); tools `Tools/build_translation.py`, `machine_translate.py`,
  `fetch_fandom_names.py`, `fetch_anime_names.py`. See `Translations/README.md`.
* Crank-a-kai: `Src/Server/Gacha` (pools for every machine, coins, mixes), real odds via
  `Tools/fetch_gacha_rates.py`.
* Web pages (in-game): wiki `Src/Server/Web/Wiki`, Event Guide, portal; admin `/admin`.
* Data sources / archives: `docs/ARCHIVES.md` (archive.org items, MediaFire packs,
  `Tools/remote_zip.py`, `Tools/mediafire_download.py --direct <link>`).
* Client testing without patching: `docs/EMULATOR.md`, `Tools/make_certs.sh`,
  `Tools/patch_apk.py` (network config trusts user CAs, re-sign splits).

## Facts that took long to find
* Official server `gameserver.yw-p.com` only accepts appVer 4.177.0; assets
  `https://resource.yw-p.com/eal/<ver>/<date>/<file>`; old unlisted assets are deleted (403).
* `ywp_mst_event` is only sent at login (not in getMaster dumps). Live tables keep ~8 events.
* 4.177.0 `libSGF.so` `.text` is encrypted (protector `libpglarmor`/`libloader`): no static
  decompilation; server host built at runtime from `https://%s.com/`. No cert pinning in the game.
* Official odds pages: `gameserver.yw-p.com/web/viewpage.nhn?userId=MA==&webviewId=GASHA_...`
  (ids in `ywp_mst_coin_purchase_master` col 13, coin item col 8).
* `web.archive.org` resets connections from cloud IPs; `archive.org` search/metadata/download work.
* MediaFire pages are behind Cloudflare for servers; `download*.mediafire.com` links work.

## Rules
* Never bypass bot checks / DRM; don't commit game data or copyrighted text.
* Treat downloaded files as untrusted: parse as data, never execute; Python with `-I`.
* Commit to the session branch with clear messages; no PR unless asked.

## Done recently
* Balance (`Src/Server/Balance`): BalanceMode rerank/stats/off + BalanceStrength 0-1; rerank maps
  rank by release-year top rank onto E..UZ++ (stat total per rank is fixed), attack skills scaled
  (skill_level col 2 text, col 4 or col 11 scaleN; Prepare() reads JP labels before translation), owned Yo-kai synced at login,
  MasterVersions raises mstVersionMaster once per content change (master_version.json).

## Open work (keep this list current; take the top item)
0. Balance: strong skills (ywp_mst_youkai_strong_skill_level) and non-attack skills are not scaled yet.
1. Endpoints whose layout is in `docs/PROTOCOL.md` but not implemented: `updateEventTutorialFlg`
   (store `ywp_user_event_tutorial`), `recvEventReward(All)`, `getEventRanking`, `gameStartEvent` /
   `gameEndEvent` / `gameContinueEvent`, `gameContinueScoreAttack`, `resultScoreAttack`,
   `executeContinueGacha`, `getGachaResult`, `presentHitodama`, `onedariHitodama`.
2. Calls only in 4.x (`deckEditMulti`, `getEventCondition`, `getEventQuest`, `getEventMessage`...):
   wait for real requests (admin -> Logs -> Unimplemented requests) from a client on an emulator.
3. Test `Tools/patch_apk.py` output and `docs/EMULATOR.md` on a real emulator / phone (needs the user).
4. Event Guide: character pictures / Drive folders for SAO, Rent-A-Girlfriend, Fire Force (entries exist).
5. Data from links the user sends (MediaFire `--direct` links, Drive, archive.org): compare file
   names with the data download, add missing ones under `dataDownload/<ver>/archive/`.
The web search tool refuses searches for game-data dumps on file hosts: only use links the user gives.
