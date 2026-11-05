#!/usr/bin/env python3
"""
Smoke test for a running Puniemu: creates an account, logs in, plays two stages and opens the main
screens, like the game does. Prints one line per call; exits with 1 if a call fails.

  python Tools/test_server.py --url http://127.0.0.1:5000 --version 4.177.0 [--admin-token secret] [--jp]

--jp       also reports Japanese text left in the responses (for servers with Language=en)
--account  reuse an account file written by a previous run (default: a new account each time)

Needs Python 3 + requests + pycryptodome (pip install requests pycryptodome).
"""
import argparse
import gzip
import hashlib
import json
import re
import sys
import uuid
from base64 import b64decode, urlsafe_b64encode

import requests
try:
    from Cryptodome.Cipher import AES
    from Cryptodome.Util.Padding import pad, unpad
except ImportError:  # pycryptodome installed as "Crypto"
    from Crypto.Cipher import AES
    from Crypto.Util.Padding import pad, unpad

KEY = bytes.fromhex("a865d7e5e2458f8ce1b5ecd087e54594")
SALT = b"0bk2kvtFE2"
JP = re.compile(r"[぀-ヿ一-鿿]")


def enc(obj):
    body = json.dumps(obj).encode()
    digest = hashlib.sha1(SALT + hashlib.sha1(SALT + b" " + body).digest()).digest()
    return urlsafe_b64encode(AES.new(KEY, AES.MODE_ECB).encrypt(pad(digest + body, 16)))


def dec(text):
    t = text.strip().replace("-", "+").replace("_", "/")
    t += "=" * (-len(t) % 4)
    return json.loads(gzip.decompress(unpad(AES.new(KEY, AES.MODE_ECB).decrypt(b64decode(t)), 16)[20:]).decode())


class Client:
    def __init__(self, url, version):
        self.url, self.version, self.s = url.rstrip("/") + "/", version, requests.Session()
        self.s.trust_env = False
        self.failed = 0
        self.jp = {}

    def call(self, ep, body, expect_dialog=False):
        r = self.s.post(self.url + ep, data=enc(body), timeout=120)
        try:
            j = dec(r.text)
        except Exception:  # noqa: BLE001
            print(f"  FAIL {ep:24} HTTP {r.status_code}: {r.text[:120]!r}")
            self.failed += 1
            return {}
        msg = j.get("dialogMsg") or ""
        bad = msg and not expect_dialog and ("error" in msg.lower() or "Unimplemented" in msg)
        print(f"  {'FAIL' if bad else 'ok  '} {ep:24} {len(j):3} keys" + (f"  dialog: {msg[:70]!r}" if msg else ""))
        if bad:
            self.failed += 1
        self.scan(ep, j)
        return j

    def scan(self, ep, o, path=""):
        if isinstance(o, dict):
            for k, v in o.items():
                self.scan(ep, v, path + "." + k)
        elif isinstance(o, list):
            for v in o:
                self.scan(ep, v, path)
        elif isinstance(o, str) and JP.search(o):
            self.jp.setdefault(ep + path, o[:60])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--url", default="http://127.0.0.1:5000")
    ap.add_argument("--version", default="4.177.0")
    ap.add_argument("--admin-token", help="also checks the admin API")
    ap.add_argument("--jp", action="store_true")
    ap.add_argument("--account")
    a = ap.parse_args()
    c = Client(a.url, a.version)

    if a.account:
        acc = json.load(open(a.account))
        udkey, gdkey = acc["udkey"], acc["gdkey"]
        print("account", gdkey)
    else:
        udkey = "d-" + uuid.uuid4().hex
        c.s.get(c.url + "api/v1/active.nhn", params={"TICKET": udkey}, timeout=60)
        gdkey = c.s.get(c.url + "api/v1/create_gdkey.nhn", params={"udkey": udkey}, timeout=60).json()["gdkey"]["value"]
        print("new account", gdkey)
    base = {"appVer": a.version, "deviceID": udkey, "level5UserID": gdkey, "osType": 2, "userID": "0", "ywpToken": "0", "mstVersionVer": 0}

    c.call("init.nhn", base)
    if not a.account:
        c.call("createUser.nhn", {**base, "iconID": 1, "playerName": "Tester", "playerSexType": 1})
    login = c.call("login.nhn", {**base, "gdkeyValue": gdkey})
    user_id = (login.get("ywp_user_data") or {}).get("userId")

    for stage in (1001001, 1001002):
        gs = c.call("gameStart.nhn", {**base, "stageId": stage, "battleType": 0}, expect_dialog=True)
        enemies = gs.get("enemyYoukaiList") or []
        if gs.get("requestId"):
            c.call("gameEnd.nhn", {**base, "requestId": gs["requestId"], "stageId": stage, "battleType": 0, "score": 25000,
                                   "clearTimeSec": 60, "clearTimeLongSec": 60000, "comboMax": 5, "damageTotal": 5000,
                                   "eraseNumTotal": 100, "eraseSizeAve": 3, "eraseSizeMax": 6, "feverTimeNum": 1, "linkSizeMax": 6,
                                   "enemyYoukaiResultList": [{"enemyId": e.get("enemyId"), "deadEndOrder": i + 1, "deadEndType": 1,
                                                              "dropYoukaiFlg": 0, "dropItemFlg": 0} for i, e in enumerate(enemies)],
                                   "userYoukaiResultList": [], "themeResultList": [], "cheatFlg": 0})

    for ep in ("map.nhn", "getMission.nhn", "loginStamp.nhn", "initGacha.nhn", "getPresentBox.nhn", "initCollectMenu.nhn",
               "initGoku.nhn", "getRanking.nhn", "initScoreAttack.nhn", "userInfoRefresh.nhn", "initWatch.nhn", "friend.nhn"):
        c.call(ep, {**base, "gdkeyValue": gdkey, "collectId": 1})
    c.call("receivePresentBox.nhn", {**base, "seqs": []})
    c.call("writeAppLog.nhn", {**base, "log": "test"})

    # a crank-a-kai pull on the Y-Money machine (price type 1 in ywp_mst_gacha)
    gacha = c.call("initGacha.nhn", base).get("ywp_mst_gacha") or ""
    ymoney_machine = next((r.split("|")[0] for r in str(gacha).split("*") if r.count("|") > 4 and r.split("|")[3] == "1"), None)
    if ymoney_machine:
        c.call("executeGacha.nhn", {**base, "gachaId": int(ymoney_machine), "gachaCnt": 1, "requestYoukaiId": 0, "activeDeckId": 1},
               expect_dialog=True)

    web = [f"web/api/gacha/machines?userId={user_id}", "web/api/wiki/index", "web/api/guide"]
    for w in web:
        r = c.s.get(c.url + w, timeout=60)
        ok = r.status_code == 200
        print(f"  {'ok  ' if ok else 'FAIL'} GET /{w.split('?')[0]:24} HTTP {r.status_code}")
        c.failed += 0 if ok else 1
    if a.admin_token:
        r = c.s.get(c.url + "admin/api/overview", headers={"X-Admin-Token": a.admin_token}, timeout=60)
        print(f"  {'ok  ' if r.status_code == 200 else 'FAIL'} admin overview HTTP {r.status_code}")
        c.failed += 0 if r.status_code == 200 else 1
        if r.status_code == 200:
            u = c.s.get(c.url + "admin/api/unimplemented", headers={"X-Admin-Token": a.admin_token}, timeout=60).json()
            print("  unimplemented requests seen by the server:", ", ".join(f"{x['endpoint']}({x['count']})" for x in u) or "none")

    if a.jp:
        print(f"Japanese text in responses: {len(c.jp)} places")
        for k, v in list(c.jp.items())[:15]:
            print(f"  {k}: {v!r}")
    print("FAILED" if c.failed else "all calls answered", f"({c.failed} failures)")
    json.dump({"udkey": udkey, "gdkey": gdkey, "userId": user_id}, open("test_account.json", "w"))
    sys.exit(1 if c.failed else 0)


if __name__ == "__main__":
    main()
