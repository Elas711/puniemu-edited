#!/usr/bin/env python3
"""
Lists or extracts files of a big ZIP on a web server without downloading all of it (HTTP range
requests), e.g. the multi-GB uploads on archive.org.

  python Tools/remote_zip.py list    URL [--depth 3]
  python Tools/remote_zip.py extract URL --match "/ywp_cud/" --out dumps/4.175

Only the central directory and the matching files are downloaded. Needs Python 3 + requests.
"""
import argparse
import collections
import io
import os
import zipfile

import requests


class HttpFile(io.RawIOBase):
    def __init__(self, url):
        self.s = requests.Session()
        r = self.s.head(url, allow_redirects=True, timeout=60)
        r.raise_for_status()
        self.url, self.size, self.pos = r.url, int(r.headers["Content-Length"]), 0

    def seekable(self):
        return True

    def readable(self):
        return True

    def tell(self):
        return self.pos

    def seek(self, off, whence=0):
        self.pos = off if whence == 0 else self.pos + off if whence == 1 else self.size + off
        return self.pos

    def readinto(self, b):
        if self.pos >= self.size or len(b) == 0:
            return 0
        end = min(self.size, self.pos + len(b)) - 1
        d = self.s.get(self.url, headers={"Range": f"bytes={self.pos}-{end}"}, timeout=120).content
        b[:len(d)] = d
        self.pos += len(d)
        return len(d)


def open_zip(url):
    return zipfile.ZipFile(io.BufferedReader(HttpFile(url), 1 << 20))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["list", "extract"])
    ap.add_argument("url")
    ap.add_argument("--depth", type=int, default=3, help="list: folder depth to sum up")
    ap.add_argument("--match", action="append", default=[], help="extract: part of the path (repeatable)")
    ap.add_argument("--out", default="extracted")
    a = ap.parse_args()
    z = open_zip(a.url)
    infos = [i for i in z.infolist() if not i.is_dir()]
    if a.cmd == "list":
        size, count = collections.Counter(), collections.Counter()
        for i in infos:
            k = "/".join(i.filename.split("/")[:a.depth])
            size[k] += i.file_size
            count[k] += 1
        for k, v in size.most_common(60):
            print(f"{v / 1e6:9.1f} MB {count[k]:7} {k}")
        print(len(infos), "files")
        return
    n = 0
    for i in infos:
        if a.match and not any(m in i.filename for m in a.match):
            continue
        # never write outside --out
        rel = os.path.normpath(i.filename).lstrip(os.sep).replace("..", "_")
        dest = os.path.join(a.out, rel)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as f:
            f.write(z.read(i))
        n += 1
    print(n, "files extracted to", a.out)


if __name__ == "__main__":
    main()
