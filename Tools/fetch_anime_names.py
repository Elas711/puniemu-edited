#!/usr/bin/env python3
"""
Official English names for the collaboration characters (Lelouch, Miku Nakano, Rem, Frieren...).

The Puni Puni collab events turn anime / manga characters into Yo-kai and give them their Japanese
names. This tool looks the series up on AniList (https://anilist.co, a public anime database),
reads every character's Japanese and English name, and keeps the names that really appear as a
Yo-kai in your game data. Puni Puni often uses the given name only ("三玖", "レム"); those are matched
too, but only inside the series the tool is reading, so two shows cannot mix up their characters.

  python Tools/fetch_anime_names.py --resources Resources --out Translations/en/names_anime.json

Variants in brackets ("三玖(水着)") are translated by the patterns, using these names.
Edit SERIES below to add a collab. Only Python 3 is needed (urllib). AniList allows 90 requests a
minute; the tool waits a second between requests.
"""
import argparse
import json
import os
import re
import time
import unicodedata
import urllib.request

API = "https://graphql.anilist.co"
UA = "Puniemu-translation-builder/1.0"

# collab -> AniList search (the series the characters come from)
SERIES = {
    "Code Geass": ["Code Geass: Hangyaku no Lelouch"],
    "Evangelion": ["Shin Seiki Evangelion"],
    "Attack on Titan": ["Shingeki no Kyojin"],
    "The Seven Deadly Sins": ["Nanatsu no Taizai"],
    "The Eminence in Shadow": ["Kage no Jitsuryokusha ni Naritakute!"],
    "Frieren": ["Sousou no Frieren"],
    "Konosuba": ["Kono Subarashii Sekai ni Shukufuku wo!"],
    "Mushoku Tensei": ["Mushoku Tensei: Isekai Ittara Honki Dasu"],
    "Quintessential Quintuplets": ["5-toubun no Hanayome"],
    "Re:Zero": ["Re:Zero kara Hajimeru Isekai Seikatsu"],
    "Shaman King": ["Shaman King (2021)"],
    "Tensura": ["Tensei shitara Slime Datta Ken"],
    "Tokyo Revengers": ["Tokyo Revengers"],
    "Sword Art Online": ["Sword Art Online", "Sword Art Online II", "Sword Art Online: Alicization"],
    "Rent-A-Girlfriend": ["Kanojo, Okarishimasu"],
    "Fire Force": ["Enen no Shouboutai"],
    "Kingdom Hearts / Final Fantasy": ["Final Fantasy VII: Advent Children"],
    "Inazuma Eleven": ["Inazuma Eleven", "Inazuma Eleven GO"],
    "Danball Senki (LBX)": ["Danball Senki"],
    "Snack World": ["Snack World"],
    "Professor Layton": ["Layton Mystery Tanteisha: Katri no Nazotoki File"],
    "Ni no Kuni": ["Ni no Kuni"],
    "Megaton Musashi": ["Megaton-kyuu Musashi"],
    "Shonen Sunday": ["Inuyasha", "Detective Conan", "Ranma 1/2", "Urusei Yatsura (2022)", "Kekkaishi", "Magi: The Labyrinth of Magic"],
    "Medalist": ["Medalist"],
    "Spy x Family": ["SPY x FAMILY"],
    "Oshi no Ko": ["Oshi no Ko"],
    "Dr. Stone": ["Dr. Stone"],
    "Gintama": ["Gintama"],
}

QUERY = """query ($search: String, $page: Int) {
  Media(search: $search, type: ANIME) {
    id title { romaji english }
    characters(page: $page, perPage: 25, sort: [ROLE, RELEVANCE]) {
      pageInfo { hasNextPage }
      nodes { name { full native alternative } }
    }
  }
}"""


def post(variables):
    body = json.dumps({"query": QUERY, "variables": variables}).encode()
    req = urllib.request.Request(API, data=body, headers={"Content-Type": "application/json", "Accept": "application/json", "User-Agent": UA})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def characters(search, pages):
    out = []
    for page in range(1, pages + 1):
        j = post({"search": search, "page": page})
        media = (j.get("data") or {}).get("Media")
        if not media:
            break
        if page == 1:
            print(f"  {search} -> {media['title'].get('english') or media['title'].get('romaji')}", flush=True)
        ch = media["characters"]
        out += [n["name"] for n in ch["nodes"]]
        time.sleep(1)
        if not ch["pageInfo"]["hasNextPage"]:
            break
    return out


def norm(s):
    return re.sub(r"[\s・･=＝]", "", unicodedata.normalize("NFKC", s or ""))


def keys(name):
    """Japanese spellings a character can have in Puni Puni -> English name."""
    native, full = name.get("native") or "", (name.get("full") or "").strip()
    if not native or not full:
        return {}
    out = {norm(native): (full, True)}
    parts_en = full.split()
    if re.search(r"[・･]", native):  # katakana names are given name first: ルルーシュ・ランペルージ
        given = re.split(r"[・･]", native)[0]
        if len(parts_en) > 1:
            out.setdefault(norm(given), (parts_en[0], False))
    elif " " in native.strip():  # kanji names are family name first: 中野 三玖 -> 三玖 = Miku Nakano
        given = native.strip().split()[-1]
        out.setdefault(norm(given), (full, False))
    return out


def game_names(resources):
    """Base names (without the variant in brackets) of every Yo-kai in ywp_mst_youkai."""
    path = next((os.path.join(resources, f) for f in ("ywp_mst_youkai.txt", "ywp_mst_youkai.json") if os.path.exists(os.path.join(resources, f))), None)
    if not path:
        raise SystemExit(f"no ywp_mst_youkai in {resources}")
    raw = open(path, encoding="utf-8-sig").read()
    if raw.lstrip().startswith("{"):
        raw = json.loads(raw).get("tableData", "")
    names = set()
    for row in raw.split("*"):
        cells = row.split("|")
        if len(cells) > 1 and cells[1]:
            # characters (collabs and Puni originals) have ids from 5000000 up; the classic Yo-kai
            # (カルラ = Tengulightened) are never touched
            if not cells[0].isdigit() or int(cells[0]) < 5000000:
                continue
            base = re.sub(r"[(（].*?[)）]", "", unicodedata.normalize("NFKC", cells[1])).strip()
            names.add(base)
    return names


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--resources", required=True, help="folder with ywp_mst_youkai")
    ap.add_argument("--out", default="Translations/en/names_anime.json")
    ap.add_argument("--translations", default="Translations/en",
                    help="existing dictionaries: official names are kept, a given-name-only match only fills gaps")
    ap.add_argument("--pages", type=int, default=6, help="character pages (25 each) per series")
    args = ap.parse_args()
    in_game = game_names(args.resources)
    official, known = set(), set()
    if os.path.isdir(args.translations):
        for f in os.listdir(args.translations):
            if not f.endswith(".json") or f.startswith(("machine", "patterns", "phrases", "names_anime")):
                continue
            try:
                d = json.load(open(os.path.join(args.translations, f), encoding="utf-8-sig"))
            except ValueError:
                continue
            ks = {unicodedata.normalize("NFKC", k) for k in d if not k.startswith("_")}
            known |= ks
            if f.startswith("strings"):
                official |= ks
    by_norm = {}
    for n in in_game:
        by_norm.setdefault(norm(n), n)
    found = {}
    for collab, searches in SERIES.items():
        print(collab, flush=True)
        for s in searches:
            try:
                chars = characters(s, args.pages)
            except Exception as e:  # noqa: BLE001
                print(f"  {s}: {e}")
                continue
            for c in chars:
                for k, (en, full_name) in keys(c).items():
                    jp = by_norm.get(k)
                    if not jp or jp in found or jp in official:
                        continue
                    if not full_name and jp in known:
                        continue  # a short given name: only where nothing else translates it
                    found[jp] = en
    out = {"_info": "Official English names of collaboration characters, from AniList (https://anilist.co). Tools/fetch_anime_names.py"}
    out.update(dict(sorted(found.items())))
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(f"{len(found)} names written to {args.out}")


if __name__ == "__main__":
    main()
