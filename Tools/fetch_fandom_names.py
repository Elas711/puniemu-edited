#!/usr/bin/env python3
"""
Builds a Japanese -> English name dictionary from the Yo-kai Watch Fandom wiki
(https://yokaiwatch.fandom.com, content licensed CC BY-SA 3.0).

Every page in the given categories that has a "japanese name" field in its infobox becomes one entry:
the page title (the English name, or the romaji name for Yo-kai that never got an English name) for
the Japanese name. The result is a normal Puniemu translation dictionary.

  python Tools/fetch_fandom_names.py --out Translations/en/names.fandom.json

Needs only Python 3 (urllib). Requests are made in batches of 50 pages with a pause in between.
"""
import argparse
import json
import re
import time
import urllib.parse
import urllib.request

API = "https://yokaiwatch.fandom.com/api.php"
UA = "Puniemu-translation-builder/1.0 (private server translation tool)"
FIELD = re.compile(r"^\s*\|\s*japanese\s*name\s*=\s*(.+?)\s*$", re.I | re.M)
JP = re.compile(r"[぀-ヿ㐀-鿿]")


def api(params):
    params = dict(params, format="json")
    req = urllib.request.Request(API + "?" + urllib.parse.urlencode(params), headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def members(category):
    out, cont = [], {}
    while True:
        j = api({"action": "query", "list": "categorymembers", "cmtitle": category, "cmlimit": 500, "cmnamespace": 0, **cont})
        out += [m["title"] for m in j["query"]["categorymembers"]]
        if "continue" not in j:
            return out
        cont = {"cmcontinue": j["continue"]["cmcontinue"]}
        time.sleep(0.5)


def embedded(template):
    out, cont = [], {}
    while True:
        j = api({"action": "query", "list": "embeddedin", "eititle": template, "eilimit": 500, "einamespace": 0, **cont})
        out += [m["title"] for m in j["query"]["embeddedin"]]
        if "continue" not in j:
            return out
        cont = {"eicontinue": j["continue"]["eicontinue"]}
        time.sleep(0.5)


def clean_title(t):
    return re.sub(r"\s*\((Yo-kai|Puni|character|Yo-kai Watch[^)]*)\)\s*$", "", t).strip()


def clean_jp(v):
    v = re.sub(r"<[^>]+>|\{\{[^}]*\}\}|\[\[|\]\]|'''?", "", v)
    v = re.split(r"[(（/]", v)[0]
    return v.strip()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default="Translations/en/names.fandom.json")
    ap.add_argument("--category", action="append", default=None,
                    help="wiki category to read (default: Category:Characters, Category:Items)")
    ap.add_argument("--template", action="append", default=None,
                    help="infobox template whose pages are read (default: Template:Yo-kai)")
    args = ap.parse_args()
    cats = args.category or ["Category:Characters", "Category:Items"]
    titles = []
    for t in args.template or ["Template:Yo-kai"]:
        try:
            pages = embedded(t)
            print(f"{t}: {len(pages)} pages")
            titles += pages
        except Exception as e:  # noqa: BLE001
            print(f"{t}: {e}")
    for c in cats:
        try:
            t = members(c)
            print(f"{c}: {len(t)} pages")
            titles += t
        except Exception as e:  # noqa: BLE001
            print(f"{c}: {e}")
    titles = list(dict.fromkeys(titles))
    names = {}
    for i in range(0, len(titles), 50):
        batch = titles[i:i + 50]
        try:
            j = api({"action": "query", "prop": "revisions", "rvprop": "content", "rvslots": "main", "titles": "|".join(batch)})
        except Exception as e:  # noqa: BLE001
            print("batch failed:", e)
            continue
        for page in j.get("query", {}).get("pages", {}).values():
            revs = page.get("revisions")
            if not revs:
                continue
            text = revs[0].get("slots", {}).get("main", {}).get("*", "")
            m = FIELD.search(text)
            if not m:
                continue
            jp = clean_jp(m.group(1))
            en = clean_title(page["title"])
            # page titles carry disambiguations ("Jaou Gun Ogama (Light)") the Japanese name does not have
            if not re.search(r"[(（]", jp):
                en = re.sub(r"\s*\([^)]*\)\s*$", "", en).strip()
            if jp and JP.search(jp) and en and not JP.search(en):
                names.setdefault(jp, en)
        time.sleep(1)
        print(f"{min(i + 50, len(titles))}/{len(titles)} pages, {len(names)} names", flush=True)
    out = {"_info": "Japanese -> English names from the Yo-kai Watch Fandom wiki (https://yokaiwatch.fandom.com), CC BY-SA 3.0."}
    out.update(dict(sorted(names.items())))
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(f"{len(names)} names written to {args.out}")


if __name__ == "__main__":
    main()
