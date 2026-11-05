# Old game data in public archives

The official servers only serve the current game data, and old asset files are deleted from the
file server (they answer 403). Copies survive in public archives.

## Internet Archive (archive.org)

`web.archive.org` (the Wayback Machine) drops connections from many cloud / data-centre networks;
from a home connection it usually works. `archive.org` itself (search, metadata, downloads) works
from everywhere:

* search: `https://archive.org/advancedsearch.php?q=punipuni&fl[]=identifier&fl[]=title&rows=50&output=json`
* files of an item: `https://archive.org/metadata/<identifier>`
* download: `https://archive.org/download/<identifier>/<file>` (supports range requests)

`Tools/remote_zip.py` lists or extracts single files of the multi-GB ZIPs without downloading all
of them. Wayback snapshots of a page can be looked up with
`https://archive.org/wayback/available?url=<url>&timestamp=<yyyymmdd>`.

Uploads useful for Puniemu (found November 2026):

| Item | Contents |
|---|---|
| `yo-kai-watch-puni-puni-event-archives` | iOS client caches of 4.172.0 (Leisure Land + Rent-a-Girlfriend) and 4.175.0 (Hololive #8): event images, sounds, `.ayd`/`.ez` files, the in-game web pages (crank-a-kai odds, notices), and the client's master data cache (`Caches/ywp_cud/*.cud`, encrypted with a per-device key, not readable yet) |
| `yo-kai-watch-wibble-wobble-reloaded-server-data-apk` | Wibble Wobble Reloaded: APK + server data (`Resources`, `dataDownload`) |
| `NAYokaiWatchWibbleWobble.7z`, `ywpeu2.0.3` | Wibble Wobble NA / EU data download (+ APK) |
| `TWPuniPuni.7z` | Taiwan Puni Puni data download |
| `wiki-yokai_watch_puni_punifandomcom`, `miraheze-wiki-punipuniwiki-*` | wiki dumps |

The data in these uploads belongs to LEVEL-5; use it only for your own private server.

## Crank-a-kai odds

The real pools of the coin machines come from the official odds pages, see
`Tools/fetch_gacha_rates.py` (live server) - saved pages from a client cache work too (`--pages`,
`--offline`).
