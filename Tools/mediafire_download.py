#!/usr/bin/env python3
"""
Downloads files (or whole folders) from MediaFire links, e.g. the community game dumps.

  python Tools/mediafire_download.py https://www.mediafire.com/file/<key>/<name>/file  --out downloads
  python Tools/mediafire_download.py https://www.mediafire.com/folder/<key>/<name>     --out downloads
  python Tools/mediafire_download.py <link> --direct "https://download1234.mediafire.com/..."   (see below)

* File name, size and SHA-256 come from MediaFire's public API (file/get_info, folder/get_content).
* The download page is read for the real download link (download*.mediafire.com), the file is
  downloaded in chunks, resumes if it was interrupted (rerun the same command) and its SHA-256 is
  checked against the one MediaFire reports.
* MediaFire sometimes shows a "Just a moment..." bot check (Cloudflare), mostly to servers and VPNs,
  rarely to a normal home connection. The tool does not try to get around it: open the link in your
  browser, right-click the blue "Download" button -> copy link address, and pass that with --direct
  (the tool still resumes and verifies the download).

Needs Python 3 + requests.
"""
import argparse
import hashlib
import os
import re
import sys
import time

import requests

API = "https://www.mediafire.com/api/1.5"
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"
CHUNK = 1 << 20


def session():
    s = requests.Session()
    s.headers["User-Agent"] = UA
    return s


def api(s, path, **params):
    r = s.get(f"{API}/{path}", params=dict(params, response_format="json"), timeout=60)
    r.raise_for_status()
    j = r.json().get("response", {})
    if j.get("result") != "Success":
        raise RuntimeError(f"MediaFire API {path}: {j.get('message') or j}")
    return j


def parse_link(link):
    m = re.search(r"mediafire\.com/(file|file_premium|folder|view|download)/([A-Za-z0-9]+)", link) or \
        re.search(r"mediafire\.com/\?([A-Za-z0-9]+)", link)
    if not m:
        raise SystemExit(f"not a MediaFire link: {link}")
    if len(m.groups()) == 1:
        return "file", m.group(1)
    return ("folder" if m.group(1) == "folder" else "file"), m.group(2)


def file_info(s, key):
    return api(s, "file/get_info.php", quick_key=key)["file_info"]


def folder_files(s, key, prefix=""):
    """[(relative path, quickkey)] of a folder and its sub-folders."""
    out = []
    for kind in ("files", "folders"):
        chunk = 1
        while True:
            j = api(s, "folder/get_content.php", folder_key=key, content_type=kind, chunk=chunk, chunk_size=1000)
            content = j.get("folder_content", {})
            for item in content.get(kind, []):
                if kind == "files":
                    out.append((os.path.join(prefix, item["filename"]), item["quickkey"]))
                else:
                    out += folder_files(s, item["folderkey"], os.path.join(prefix, item["name"]))
            if content.get("more_chunks") != "yes":
                break
            chunk += 1
    return out


class Blocked(Exception):
    pass


def download_link(s, info):
    """The real download URL from the file's download page."""
    page = (info.get("links") or {}).get("normal_download") or f"https://www.mediafire.com/file/{info['quickkey']}/file"
    r = s.get(page, timeout=60)
    if r.status_code in (403, 503) or "Just a moment" in r.text[:3000]:
        raise Blocked(page)
    m = re.search(r'href="(https?://download\d*\.mediafire\.com/[^"]+)"', r.text) or \
        re.search(r'(https?://download\d*\.mediafire\.com/[^"\'\s<>]+)', r.text)
    if not m:
        raise RuntimeError(f"no download link found on {page} (the file may have been removed)")
    return m.group(1).replace("&amp;", "&")


def fetch(s, url, dest, size):
    part = dest + ".part"
    have = os.path.getsize(part) if os.path.exists(part) else 0
    if size and have >= size:
        os.replace(part, dest)
        return
    headers = {"Range": f"bytes={have}-"} if have else {}
    with s.get(url, headers=headers, stream=True, timeout=120) as r:
        if r.status_code == 200 and have:
            have = 0  # the server ignored the range: start over
        elif r.status_code not in (200, 206):
            raise RuntimeError(f"download failed: HTTP {r.status_code}")
        if "text/html" in r.headers.get("Content-Type", ""):
            raise Blocked(url)
        start, last = time.time(), 0.0
        with open(part, "ab" if have else "wb") as f:
            for block in r.iter_content(CHUNK):
                f.write(block)
                have += len(block)
                now = time.time()
                if now - last > 2:
                    pct = f"{have / size * 100:5.1f}%" if size else ""
                    print(f"\r  {have / 1e6:9.1f} MB {pct}  {have / 1e6 / max(now - start, 0.001):.1f} MB/s", end="", flush=True)
                    last = now
    print()
    os.replace(part, dest)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(CHUNK), b""):
            h.update(block)
    return h.hexdigest()


def safe_rel(name):
    """File / folder names come from MediaFire: keep them inside the output folder."""
    parts = [p for p in re.split(r"[\\/]+", name) if p not in ("", ".", "..")]
    parts = [re.sub(r'[<>:"|?*\x00-\x1f]', "_", p) for p in parts]
    return os.path.join(*parts) if parts else "download"


def get_file(s, key, dest_dir, rel=None, direct=None):
    info = file_info(s, key)
    name = safe_rel(rel or info["filename"])
    dest = os.path.join(dest_dir, name)
    os.makedirs(os.path.dirname(dest) or ".", exist_ok=True)
    size = int(info.get("size") or 0)
    print(f"{name}: {size / 1e6:.1f} MB")
    if os.path.exists(dest) and (not size or os.path.getsize(dest) == size):
        print("  already downloaded")
    else:
        url = direct
        for attempt in range(5):
            try:
                url = url or download_link(s, info)
                fetch(s, url, dest, size)
                break
            except Blocked:
                print("  MediaFire shows a bot check to this connection. Open the link in your browser,\n"
                      "  copy the address of the 'Download' button and run again with --direct \"<that link>\".")
                return False
            except (requests.RequestException, RuntimeError) as e:
                if attempt == 4:
                    raise
                print(f"  {e} - retrying")
                url = direct  # download links expire: ask for a fresh one
                time.sleep(2 ** attempt)
    want = (info.get("hash") or "").lower()
    if len(want) == 64:
        got = sha256(dest)
        if got != want:
            print(f"  SHA-256 MISMATCH (got {got}, MediaFire says {want}) - delete the file and download again")
            return False
        print("  SHA-256 ok")
    return True


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("link")
    ap.add_argument("--out", default="downloads")
    ap.add_argument("--direct", help="download link copied from the browser (file links only)")
    a = ap.parse_args()
    s = session()
    kind, key = parse_link(a.link)
    if kind == "file":
        ok = get_file(s, key, a.out, direct=a.direct)
    else:
        files = folder_files(s, key)
        print(f"{len(files)} files in the folder")
        ok = all([get_file(s, k, a.out, rel) for rel, k in files])
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
