# Running Puniemu on Pelican (or Pterodactyl)

The egg is `deploy/egg-puniemu.json`. It builds the server from git when the server is installed
(and again on every Reinstall), keeps player saves in a SQLite file inside the server folder (no
database to set up) and is configured from the panel's Startup tab.

Tested here: the install script (clone, build, folder layout), a start with the egg's startup
command and default variables (the panel's "started" line appears, the smoke test passes), saves
surviving a reinstall, and HTTPS with a certificate from `Tools/make_certs.sh`. Not tested on a real
Pelican panel: if a step there looks different, the panel's own wording wins.

## 1. Import the egg (panel admin)

* **Pelican:** Admin area -> **Eggs** -> **Import** -> choose `deploy/egg-puniemu.json` (or paste the
  raw GitHub URL of the file).
* **Pterodactyl:** Admin -> Nests -> (a nest) -> **Import Egg**.

## 2. Create the server

Admin area -> **Servers** -> **Create**:

| Field | Value |
|---|---|
| Egg | Puniemu (Yo-kai Watch Puni Puni / Wibble Wobble) |
| Docker image | `.NET 8 (parkervcp yolks)` |
| Allocation (port) | any free port, e.g. `8080`; **443** if the game should connect directly (see 6) |
| Memory | 1.5-2 GB (measured: ~800 MB with the English translation and 75k asset files) |
| Disk | 2 GB + the size of your `dataDownload` folder (often several GB) |
| CPU | 100-200 % is plenty |

Leave the variables at their defaults for now. The install starts by itself: the console shows the
git clone, the build and the game data download (3-6 minutes, longer with assets) and ends with
`Install complete.`

## 3. Game data

### Easiest: your private data repository

`Elas711/Puniemu-Data` (private) holds everything ready-made: the 4.177.0 master tables with the
real crank-a-kai pools, the complete English translation (0 texts left in Japanese) and ~2,600 old
asset files the official server no longer has. The egg uses it by default:

1. GitHub -> Settings -> Developer settings -> **Personal access tokens -> Fine-grained tokens** ->
   *Generate new token*: Repository access = only `Puniemu-Data`, Permissions -> Contents =
   **Read-only**. Copy the token.
2. Server -> Startup tab: **Git token** = the token (Data repository is already filled in).
3. Settings tab -> **Reinstall server**. The install copies `Resources/`, `Translations/` and the
   archive assets in place and skips the slow automatic steps below.

Optional: **Download assets** = `true` also fetches the ~75,000 current asset files (several GB)
from the official file server.

Tested here: a fresh install from the data repository starts, translates all 173,690 texts (0 left
in Japanese) and passes the smoke test.

### Without a data repository (automatic)

Empty **Data repository** and the install builds the data itself:

1. **Base data** (`Base data repository`, default `Washaaaan/puniemu2`): Puniemu's own files
   (`stage_data`, `gacha_pool`, `mission_cfg`, ...) and a set of master tables, copied into
   `Resources/`. Files you already have there are kept.
2. **Current master tables** (`Download master tables` = `true`): downloaded from the official
   server the way the game does it (needs the current `Game version`, 4.177.0 today). This
   overwrites the `ywp_mst_*` files at every reinstall.
3. **Assets** (`Download assets` = `true`, off by default): all ~75,000 files (several GB) into
   `dataDownload/`. A later reinstall only fetches new files. Give the server enough disk first.

Tested here: an empty server folder ends up with 331 files in `Resources/` and the smoke test
passes. If the official server refuses (old `Game version`), the install continues with the base
data and prints a warning.

You can still upload your own data (Files tab or SFTP): `Resources/` for the master tables,
`dataDownload/` for assets, generated translations (`machine*.json`) into `Translations/en/`. Big
folders: upload a zip and use the file manager's **Unarchive**. To keep edited master tables, set
`Download master tables` to `false`.

4. **Crank-a-kai odds + translations** (`true`): the real crank-a-kai pools from the official odds
   pages; with Language `en` the English dictionary from Wibble Wobble and (Machine translation =
   `true`) an offline machine translation of every text that is left (1.1 GB model once, 10-30
   minutes CPU).

The same download works outside the panel:
`Puniemu --fetch-game-data --version 4.177.0 --resources Resources [--assets dataDownload]`.

## 4. Settings (Startup tab)

| Variable | What to set |
|---|---|
| Admin token | a long random password; the admin panel is at `http://<node address>:<port>/admin` |
| Public server URL | the address players use, e.g. `http://<node address>:<port>` (or `https://gameserver.yw-p.com` for the redirect set-up in 6) |
| Language | `en` for English, empty for Japanese |
| Game | `false` = Puni Puni, `true` = Wibble Wobble |
| Database | `sqlite` (saves in `data/saves.db`) or `postgres` + connection string |
| Balance mode / strength, Anti-cheat | see the README |
| Git branch | `ccr-00a9cf0a-1hujzi` today; switch to `master` once the work is merged |

More settings (everything in the README's configuration table) go into `appsettings.json` in the
server folder; the panel variables take priority over it.

## 5. Start

Press **Start**. The console shows `db service started: SQLite (...)` and
`Application started` - the panel then marks the server as running. Open
`http://<node address>:<port>/admin` and log in with the admin token.

* **Update:** Settings tab -> **Reinstall server**. Only the `app/` folder is rebuilt; `Resources/`,
  `dataDownload/`, `data/` (saves, admin settings), `appsettings.json` and your own files stay.
* **Backups:** the Backups tab includes `data/saves.db`; stop the server first for a clean copy.

## 6. Connecting the game

The 4.177.0 client only talks to `gameserver.yw-p.com` over HTTPS (docs/EMULATOR.md):

1. Give the server the allocation **443** (ask the panel admin for it).
2. On your PC: `sh Tools/make_certs.sh certs`, then upload the `certs` folder into the server
   folder.
3. Startup tab: **HTTPS certificate** = `certs/server.pfx`, password `puniemu`,
   **Public server URL** = `https://gameserver.yw-p.com`. Restart.
4. On the emulator / phone: install `certs/ca.pem` (or the `*.0` file) as in docs/EMULATOR.md and
   point the game's host names at the **node's IP address** instead of `10.0.2.2`.

Wibble Wobble clients: point the launching zone at `http://<node address>:<port>/hsp`
(docs/EMULATOR.md, "Wibble Wobble clients").

## Troubleshooting

* **Install fails at `git clone`:** wrong branch name, or a private repository without a token
  (Git token variable).
* **Server stops right after start:** the console's last lines say why - usually an empty
  `Resources/` folder or a wrong `IsWibWob` value.
* **Panel never shows "running":** the server didn't log `Application started`; check the port
  (allocation) and the console.
* **The tools (`Tools/*.py`)** need Python, which the server image does not have: run them on your
  PC (the smoke test works against the server's address:
  `python Tools/test_server.py --url http://<node address>:<port>`).
