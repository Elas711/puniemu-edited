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
| `4.117.1` | Android data folder of 4.117.1 (2023): 51,500 asset files incl. events 203-205, crank-a-kai 222-227 and 40 maps the official server no longer has; `ywp_cud` master cache (encrypted) |
| `ykwpp-dump-4.123.0-ver`, `-4.124.0-ver-galaxy-watch-part-3-event`, `-4.129.0-ver` | listed, but their files have been removed |
| MediaFire `YKWPP1.0.3-4.96.0 files.zip` (2.1 GB RAR, by Math_kk) | mirror of the official file server from 1.0.3 to 4.96.0 (version/date folders, 45,704 files) |
| MediaFire `Yo-kai Watch Puni Puni 4.146.1 Data.rar` (3.0 GB, by Math_kk) | Android data folder of 4.146.1 (2025), 61,798 files + `ywp_cud` cache + shared_prefs (ad SDKs only) |
| MediaFire `files.rar` (4.0 GB, by Pablxx Puni, 2026-10) | Android data folder (2026), 73,967 files + `ywp_cud` cache |

| MediaFire `YWPPNews_Hololive8thColabEvent_UNFINISHED.rar` (189 MB, 2026-10) | saved official news pages (152) + images + promo videos of the Hololive 8 event; 59 crank-a-kai odds pages (`fetch_gacha_rates.py --html`) |
| Google Drive `KirasPuniModelDump.zip` (1.7 GB) | 7,117 Yo-kai model files (`.ez`/`.ayd`), all still on the official server |
| GitHub [Pabl06/YWPuniPuniAssets](https://github.com/Pabl06/YWPuniPuniAssets) | decoded art of collabs / maps / titles 2022-2025 (1,108 files the official server no longer has) and READMEs with each event's start date (used in `Events/history.json`) |
| Discord `ezPuniImporter_V1_2.zip` | Blender add-on importing `.ez` / `.ymd` models (based on Tiniifan's Level-5 add-on); harmless code, not needed by the server |

Every asset file of the three MediaFire packs is still on the official file server, so a full
download with the data downloader already has them. MediaFire's page shows a Cloudflare bot check
to servers; `Tools/mediafire_download.py --direct <link copied from the Download button>` fetches the
file from the download server (no check there), resumes and verifies the SHA-256.

The `.cud` master-data caches in all dumps (4.117.1 - 4.177) stay unreadable: not the request cipher
(`JsonCryptor` key `a865d7e5...` / salt `0bk2kvtFE2`, as hex or text, ECB or CBC) nor the `.ez` /
sound keys. In the 2.2.6 client `CustomUserDefault::saveData` only writes the bytes; the 4.x key is
in the encrypted code.
| `yo-kai-watch-wibble-wobble-reloaded-server-data-apk` | Wibble Wobble Reloaded: APK + server data (`Resources`, `dataDownload`) |
| `NAYokaiWatchWibbleWobble.7z`, `ywpeu2.0.3` | Wibble Wobble NA / EU data download (+ APK) |
| `TWPuniPuni.7z` | Taiwan Puni Puni data download |
| `wiki-yokai_watch_puni_punifandomcom`, `miraheze-wiki-punipuniwiki-*` | wiki dumps |

Files of these dumps that the official server no longer has can be added to the data download:
put them in `dataDownload/<version>/archive/` (the server finds files by name, newest version first).
On the 4.177.0 data that adds about 1,500 files (events 203-205 and 266-270, crank-a-kai art,
maps, about 360 music tracks).

The data in these uploads belongs to LEVEL-5; use it only for your own private server.

## Crank-a-kai odds

The real pools of the coin machines come from the official odds pages, see
`Tools/fetch_gacha_rates.py` (live server) - saved pages from a client cache work too (`--pages`,
`--offline`).

## Finding more sites: `Tools/discover_sites.py`

```
python -I Tools/discover_sites.py --out sites_found               # search APIs + crawler
python -I Tools/discover_sites.py --out sites_found --no-crawl    # search APIs only (~2 min)
python -I Tools/discover_sites.py --out sites_found --seed https://example.com/page
```

Searches the keyless public APIs (archive.org, GitHub / GitLab / Codeberg, Wikipedia in 7 languages
and their external links, Wikimedia Commons, Fandom wikis, Hacker News, StackExchange), then crawls
from the results (robots.txt respected, 1.5 s between requests per host, HTML only) and keeps pages
whose text is about Puni Puni / Wibble Wobble. Runs merge into `sites_found/sites.json` and
`sites_found/SITES.md` (by category). `GITHUB_TOKEN` raises the GitHub limit.
Not used: result pages of search engines and sites behind bot checks or logins (Reddit, X, Discord);
file hosts are never followed or listed.
