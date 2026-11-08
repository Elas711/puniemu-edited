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
git clone and the build (2-5 minutes) and ends with `Install complete.`

## 3. Upload the game data

Files tab (or SFTP: the panel shows the address, user and your panel password):

* `Resources/` - the master tables (`ywp_mst_*.txt`, `ywp_user_*_def.txt`, `stage_data`...), e.g. from
  `Tools/dataDownloadDownloader.py` run on your PC.
* `dataDownload/` - the asset files (folders `<version>/<date>/...`), if this server should serve
  them.
* Optional: generated translations (`machine*.json`, `strings.generated.json`) into
  `Translations/en/`.

Big folders: zip them on your PC, upload the zip and use the file manager's **Unarchive**.

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
