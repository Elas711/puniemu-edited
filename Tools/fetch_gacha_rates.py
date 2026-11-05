#!/usr/bin/env python3
"""
Real crank-a-kai pools from the official "what's inside" pages.

Japanese law makes gacha games publish the odds: Puni Puni shows them in a web page per coin
(viewpage.nhn?webviewId=GASHA_..., listed in ywp_mst_coin_purchase_master). Each page lists every
Yo-kai of the machine with its exact chance. This tool reads those pages and writes the pools in
Puniemu's gacha_pool format, so the coin machines drop what they really dropped.

  python Tools/fetch_gacha_rates.py --resources Resources --out Resources/gacha_pool.txt --merge

--merge keeps the pools already in --out (pools read here replace those of the same machine).
Pages are fetched one per second. Saved pages (--pages DIR, e.g. from a client's web cache) can be
used instead of the live server with --offline.
"""
import argparse
import html
import json
import os
import re
import time
import unicodedata
import urllib.request

PAGE = "https://gameserver.yw-p.com/web/viewpage.nhn?userId=MA==&webviewId={}"
UA = "Mozilla/5.0 (Linux; Android 9) AppleWebKit/537.36 (KHTML, like Gecko) Mobile Safari/537.36"
ENTRY = re.compile(r"([^\s%（）()][^%]*?)\s+(\d+\.\d+)%")


def table(resources, name):
    for ext in (".txt", ".json"):
        p = os.path.join(resources, name + ext)
        if os.path.exists(p):
            raw = open(p, encoding="utf-8-sig").read()
            if raw.lstrip().startswith("{"):
                j = json.loads(raw)
                raw = j.get("tableData", raw) if isinstance(j, dict) else raw
            return [r.split("|") for r in raw.split("*") if r and r != "null"]
    raise SystemExit(f"{name} not found in {resources}")


def norm(s):
    return re.sub(r"\s+", "", unicodedata.normalize("NFKC", html.unescape(s)))


def page_text(raw):
    t = re.sub(r"<script.*?</script>|<style.*?</style>", " ", raw, flags=re.S)
    t = re.sub(r"<[^>]+>", " ", t)
    return re.sub(r"\s+", " ", html.unescape(t))


def parse(text):
    """[(name, percent)] of a page: the entries after the rank summary ("1.200% （9種）")."""
    # the first list is the normal odds; later lists ("確定枠": the guaranteed slot of a 10-pull,
    # step-up tables "100連"...) are left out
    body = text.split("の通りです。", 1)[-1]
    body = re.split(r"妖怪ガシャからは|出現確率は小数|\S*確定枠|\S*枠\s*の出現確率|\s\d+連\s", body)[0]
    out = []
    for name, pct in ENTRY.findall(body):
        name = name.strip()
        if re.fullmatch(r"[\d.%（）()種\s]+", name) or "種）" in name:
            continue
        name = re.sub(r"^.*種）\s*", "", name).strip()
        if name:
            out.append((name, float(pct)))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--resources", required=True, help="master data folder (ywp_mst_gacha, _youkai, _item, _coin_purchase_master)")
    ap.add_argument("--out", required=True, help="gacha_pool file to write")
    ap.add_argument("--merge", action="store_true", help="keep the other pools already in --out")
    ap.add_argument("--pages", default=None, help="folder to save the fetched pages in (or to read them from)")
    ap.add_argument("--offline", action="store_true", help="only use the pages already in --pages")
    args = ap.parse_args()

    youkai, rank = {}, {}
    for r in table(args.resources, "ywp_mst_youkai"):
        if len(r) > 3 and r[0].isdigit() and r[3].isdigit() and 0 < int(r[3]) <= 13:
            k = norm(r[1])
            # several Yo-kai can share a name (forms); keep the newest (highest id)
            if k not in youkai or int(r[0]) > youkai[k]:
                youkai[k] = int(r[0])
            rank[int(r[0])] = int(r[3])
    items = {norm(r[2]): int(r[0]) for r in table(args.resources, "ywp_mst_item") if len(r) > 2 and r[0].isdigit()}
    coin_page = {}
    for r in table(args.resources, "ywp_mst_coin_purchase_master"):
        if len(r) > 13 and r[13].startswith("GASHA") and r[8].isdigit():
            coin_page.setdefault(r[13], set()).add(r[8])
    machines = {}
    for r in table(args.resources, "ywp_mst_gacha"):
        if len(r) > 4 and r[0].isdigit():
            machines.setdefault(r[4], []).append(int(r[0]))

    pools = {}
    if args.merge and os.path.exists(args.out):
        pools = json.load(open(args.out, encoding="utf-8-sig"))
    template = next(iter(pools.values()), {}).get("convertItem", {}) if pools else {}
    if args.pages:
        os.makedirs(args.pages, exist_ok=True)

    found, unknown = 0, set()
    for wid, coins in sorted(coin_page.items()):
        cache = os.path.join(args.pages, wid + ".html") if args.pages else None
        if cache and os.path.exists(cache):
            raw = open(cache, encoding="utf-8", errors="ignore").read()
        elif args.offline:
            continue
        else:
            req = urllib.request.Request(PAGE.format(wid), headers={"User-Agent": UA})
            try:
                with urllib.request.urlopen(req, timeout=60) as resp:
                    raw = resp.read().decode("utf-8", errors="ignore")
            except Exception as e:  # noqa: BLE001
                print(f"{wid}: {e}")
                continue
            if cache:
                open(cache, "w", encoding="utf-8").write(raw)
            time.sleep(1)
        entries = parse(page_text(raw))
        pool = {"weights": {}, "youkai": {}, "items": {}, "rateUp": {}, "convertItem": dict(template)}
        for name, pct in entries:
            k = norm(name)
            if k in youkai:
                yid = youkai[k]
                key = str(rank[yid])
                pool["youkai"].setdefault(key, []).append(yid)
                pool["weights"][key] = round(pool["weights"].get(key, 0) + pct, 4)
                pool["rateUp"][str(yid)] = pct
            elif k in items:
                pool["items"].setdefault("i1", []).append(items[k])
                pool["weights"]["i1"] = round(pool["weights"].get("i1", 0) + pct, 4)
            else:
                unknown.add(name)
        # within a rank each Yo-kai is picked by its own chance: rate-up weights relative to the mean
        for key, ids in pool["youkai"].items():
            mean = pool["weights"][key] / len(ids)
            for y in ids:
                pool["rateUp"][str(y)] = round(pool["rateUp"][str(y)] / mean, 4) if mean > 0 else 1.0
        n = sum(len(v) for v in pool["youkai"].values())
        if n == 0:
            print(f"{wid}: no Yo-kai found on the page")
            continue
        targets = [m for c in coins for m in machines.get(c, [])]
        for m in targets:
            p = dict(pool, gachaId=m)
            pools[str(m)] = p
        found += 1
        print(f"{wid}: {n} Yo-kai, {len(entries) - n} other entries -> machines {targets}", flush=True)

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(pools, f, ensure_ascii=False, indent=1)
    print(f"{found} pages read, {len(pools)} pools in {args.out}")
    if unknown:
        print(f"{len(unknown)} names not found in the master data, e.g. {sorted(unknown)[:8]}")


if __name__ == "__main__":
    main()
