#!/usr/bin/env python3
"""
Builds and maintains the translation dictionaries used by Puniemu's TranslationManager.

Puni Puni (Japan) and Wibble Wobble (international) share most of their master data: the same
tables, the same ids, only the text differs. This tool lines the two up row by row and writes a
Japanese -> English dictionary that the server applies to the game data it sends.

  # 1. build a dictionary from the JP and EN Resources folders
  python Tools/build_translation.py build --jp path/to/Resources_JP --en path/to/Resources_EN \
         --out Translations/en/strings.generated.json

  # 2. list every Japanese text that is still untranslated (fill in the values and keep the file
  #    in Translations/en/ - empty values are ignored by the server)
  python Tools/build_translation.py extract --jp Resources --translations Translations/en \
         --out Translations/en/untranslated.json

  # 3. show how much of the data a dictionary covers
  python Tools/build_translation.py stats --jp Resources --translations Translations/en

Only Python 3 is needed, no extra packages.
"""
import argparse
import collections
import json
import os
import re
import sys

JAPANESE = re.compile(r"[぀-ヿ㐀-鿿ｦ-ﾟ]")


def has_jp(s):
    return isinstance(s, str) and bool(JAPANESE.search(s))


def load_table(path):
    """Returns ("json", rows) for JSON tables or ("pipe", rows) for pipe/asterisk tables."""
    with open(path, encoding="utf-8-sig") as f:
        raw = f.read()
    t = raw.lstrip()
    if t.startswith("{") or t.startswith("["):
        try:
            tok = json.loads(raw)
        except json.JSONDecodeError:
            return "pipe", [r.split("|") for r in raw.split("*")]
        if isinstance(tok, dict) and isinstance(tok.get("tableData"), str):
            return "pipe", [r.split("|") for r in tok["tableData"].split("*")]
        if isinstance(tok, dict) and isinstance(tok.get("data"), list):
            return "json", [r for r in tok["data"] if isinstance(r, dict)]
        if isinstance(tok, list):
            return "json", [r for r in tok if isinstance(r, dict)]
        return "other", []
    return "pipe", [r.split("|") for r in raw.split("*")]


def json_key(row):
    keys = []
    for k in sorted(row):
        v = row[k]
        if isinstance(v, (int, float)) and not isinstance(v, bool) and (k.lower().endswith("id") or k.lower().endswith("no") or k.lower().endswith("seq")):
            keys.append((k, v))
    return tuple(keys)


def pipe_key(row):
    # The leading numeric columns identify a row in practically every table.
    key = []
    for c in row[:4]:
        if c.isdigit():
            key.append(c)
        else:
            break
    return tuple(key)


def index_rows(kind, rows):
    idx = collections.defaultdict(list)
    for r in rows:
        k = json_key(r) if kind == "json" else pipe_key(r)
        if k:
            idx[k].append(r)
    return idx


def same_layout(a, b):
    n = min(len(a), len(b))
    nums = [(x, y) for x, y in zip(a[:n], b[:n]) if x.isdigit() or y.isdigit()]
    if len(nums) < 3:
        return False
    return sum(1 for x, y in nums if x == y) / len(nums) >= 0.7


def table_files(folder):
    out = {}
    for name in os.listdir(folder):
        if name.endswith((".txt", ".json")):
            out.setdefault(os.path.splitext(name)[0], os.path.join(folder, name))
    return out


def cmd_build(args):
    jp_files = table_files(args.jp)
    en_files = table_files(args.en)
    votes = collections.defaultdict(collections.Counter)
    tables_used = 0
    for name in sorted(set(jp_files) & set(en_files)):
        try:
            jk, jrows = load_table(jp_files[name])
            ek, erows = load_table(en_files[name])
        except Exception as e:  # noqa: BLE001
            print(f"skip {name}: {e}", file=sys.stderr)
            continue
        if jk != ek or jk == "other":
            continue
        jidx, eidx = index_rows(jk, jrows), index_rows(ek, erows)
        pairs_here = 0
        for key, jr_list in jidx.items():
            er_list = eidx.get(key)
            if not er_list or len(er_list) != len(jr_list):
                continue
            for jr, er in zip(jr_list, er_list):
                if jk == "json":
                    for field, jv in jr.items():
                        ev = er.get(field)
                        if has_jp(jv) and isinstance(ev, str) and ev.strip() and not has_jp(ev):
                            votes[jv][ev] += 1
                            pairs_here += 1
                else:
                    # Newer versions often add columns at the end: compare the shared columns, but
                    # only when the numeric cells agree (same row layout)
                    if len(jr) != len(er) and not same_layout(jr, er):
                        continue
                    for jv, ev in zip(jr, er):
                        if has_jp(jv) and ev.strip() and not has_jp(ev):
                            votes[jv][ev] += 1
                            pairs_here += 1
        if pairs_here:
            tables_used += 1
            print(f"{name}: {pairs_here} text pairs")
    result = {jp: c.most_common(1)[0][0] for jp, c in votes.items()}
    if args.merge and os.path.exists(args.out):
        with open(args.out, encoding="utf-8") as f:
            existing = json.load(f)
        existing.update({k: v for k, v in result.items() if k not in existing or not existing[k]})
        result = existing
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(dict(sorted(result.items())), f, ensure_ascii=False, indent=1)
    print(f"\n{len(result)} translations from {tables_used} tables written to {args.out}")


def load_translations(folder):
    exact, phrases = {}, {}
    if not folder or not os.path.isdir(folder):
        return exact, phrases
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".json"):
            continue
        with open(os.path.join(folder, name), encoding="utf-8-sig") as f:
            try:
                d = json.load(f)
            except json.JSONDecodeError as e:
                print(f"warning: {name}: {e}", file=sys.stderr)
                continue
        target = phrases if name.lower().startswith("phrases") else exact
        for k, v in d.items():
            if k and not k.startswith("_") and isinstance(v, str) and v.strip():
                target[k] = v
    return exact, phrases


def japanese_cells(folder):
    """Yields (table, text) for every Japanese text cell."""
    for name, path in sorted(table_files(folder).items()):
        try:
            kind, rows = load_table(path)
        except Exception:  # noqa: BLE001
            continue
        for r in rows:
            values = r.values() if kind == "json" else r
            for v in values:
                if has_jp(v):
                    yield name, v


def cmd_extract(args):
    exact, _ = load_translations(args.translations)
    todo = collections.OrderedDict()
    where = collections.defaultdict(set)
    for table, text in japanese_cells(args.jp):
        if text in exact or text.replace("\n", "") in exact:
            continue
        todo.setdefault(text, "")
        where[text].add(table)
    out = {"_info": "Fill in the English text for each entry. Empty entries are ignored by the server."}
    for k in todo:
        out[k] = ""
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    per_table = collections.Counter(t for k in todo for t in where[k])
    print(f"{len(todo)} untranslated texts written to {args.out}")
    for t, n in per_table.most_common(15):
        print(f"  {t}: {n}")


def cmd_stats(args):
    exact, phrases = load_translations(args.translations)
    total = done = 0
    for _, text in japanese_cells(args.jp):
        total += 1
        if text in exact or text.replace("\n", "") in exact:
            done += 1
    pct = 100.0 * done / total if total else 100.0
    print(f"{len(exact)} strings, {len(phrases)} phrases loaded")
    print(f"{done} / {total} Japanese texts covered ({pct:.1f}%)")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build", help="build a JP->EN dictionary from two Resources folders")
    b.add_argument("--jp", required=True)
    b.add_argument("--en", required=True)
    b.add_argument("--out", default="Translations/en/strings.generated.json")
    b.add_argument("--merge", action="store_true", help="keep entries already in --out")
    e = sub.add_parser("extract", help="export untranslated Japanese texts")
    e.add_argument("--jp", required=True)
    e.add_argument("--translations", default="Translations/en")
    e.add_argument("--out", default="Translations/en/untranslated.json")
    s = sub.add_parser("stats", help="show translation coverage")
    s.add_argument("--jp", required=True)
    s.add_argument("--translations", default="Translations/en")
    args = p.parse_args()
    {"build": cmd_build, "extract": cmd_extract, "stats": cmd_stats}[args.cmd](args)


if __name__ == "__main__":
    main()
