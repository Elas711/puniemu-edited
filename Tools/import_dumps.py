#!/usr/bin/env python3
"""
Turns old Puni Puni / Wibble Wobble data dumps into Puniemu event packs.

Every dump of the official servers only contains the events that were running when it was made.
Put all the dumps you have side by side and this tool collects every event they contain that your
current Resources folder does not have, and writes:

  Events/imported/event_<id>.json   one pack per event: its ywp_mst_event row, maps, quests,
                                    bonuses, story texts...
  Events/imported/_missing_rows.json  a data-only pack (eventId 0) with the rows of every other
                                    master table (Yo-kai, stages, enemies, items, gachas...) that
                                    exist in the dumps but not in your Resources. Event maps need
                                    them (their stages, enemies and new Yo-kai).
  Events/imported/report.txt        what was found.

Usage:
  python Tools/import_dumps.py --current Resources --dumps path/to/extracted/files --out Events/imported

--dumps is searched recursively: every folder that contains ywp_mst_*.txt / .json files is one dump
(folders are processed in name order, later dumps win when the same row differs). getMaster
responses saved as JSON (one file with many "ywp_mst_..." keys) are understood too.

Afterwards restart Puniemu (or press "Rescan data" in the admin panel): the events appear in the
event lists and can be switched on.

Only Python 3 is needed. The dump files are only parsed as JSON / text, never executed.
"""
import argparse
import collections
import json
import os
import re
import sys

# Event tables whose rows belong to one event: table -> column holding the event id (pipe tables).
PIPE_EVENT_COL = {
    "ywp_mst_event_quest": 0,
    "ywp_mst_event_quest_sub": 0,
    "ywp_mst_event_youkai_assist_disp": 1,
    "ywp_mst_event_tutorial": 0,
    "ywp_mst_event_tutorial_message": 0,
    "ywp_mst_event_point_buff": 0,
    "ywp_mst_event_point_buff_lv": 0,
    "ywp_mst_event_point_trade_release": 0,
    "ywp_mst_event_match": 0,
}
# Never copied: versioning / per-dump bookkeeping, and the event table itself (handled per event).
SKIP_TABLES = {"ywp_mst_event", "ywp_mst_version_master", "ywp_mst_version_master_unchecked",
               "ywp_mst_version_resource", "ywp_mst_version_resource_unchecked"}
TABLE_NAME = re.compile(r"^ywp_mst_[a-z0-9_]+$")


class Table:
    """kind: 'json' (rows are dicts) or 'pipe' (rows are lists of strings)."""

    def __init__(self, kind, rows):
        self.kind = kind
        self.rows = rows


def parse_table(raw):
    t = raw.lstrip("﻿").lstrip()
    if t.startswith("{") or t.startswith("["):
        try:
            tok = json.loads(t)
        except json.JSONDecodeError:
            return None
        if isinstance(tok, dict) and isinstance(tok.get("tableData"), str):
            return Table("pipe", [r.split("|") for r in tok["tableData"].split("*") if r])
        if isinstance(tok, dict) and isinstance(tok.get("data"), list):
            return Table("json", [r for r in tok["data"] if isinstance(r, dict)])
        if isinstance(tok, list):
            return Table("json", [r for r in tok if isinstance(r, dict)])
        return None
    if "|" in t:
        return Table("pipe", [r.split("|") for r in t.split("*") if r])
    return None


def load_folder(folder):
    """Returns {table name: Table} for one dump folder."""
    tables = {}
    for name in sorted(os.listdir(folder)):
        path = os.path.join(folder, name)
        if not os.path.isfile(path) or not name.endswith((".txt", ".json")):
            continue
        base = os.path.splitext(name)[0]
        try:
            with open(path, encoding="utf-8-sig", errors="replace") as f:
                raw = f.read()
        except OSError:
            continue
        if TABLE_NAME.match(base):
            if base in tables and name.endswith(".json"):
                continue  # prefer the .txt twin
            tab = parse_table(raw)
            if tab:
                tables[base] = tab
            continue
        # A saved getMaster response: {"ywp_mst_x": ..., "ywp_mst_y": ...}
        try:
            tok = json.loads(raw)
        except json.JSONDecodeError:
            continue
        if isinstance(tok, dict):
            for k, v in tok.items():
                if TABLE_NAME.match(k) and k not in tables:
                    tab = parse_table(v if isinstance(v, str) else json.dumps(v))
                    if tab:
                        tables[k] = tab
    return tables


def find_dumps(root):
    out = []
    for d, _, files in os.walk(root):
        if any(TABLE_NAME.match(os.path.splitext(f)[0]) for f in files) or \
           any(f.endswith(".json") and "getMaster" in f for f in files):
            out.append(d)
    return sorted(out)


def row_text(kind, row):
    return json.dumps(row, sort_keys=True, ensure_ascii=False) if kind == "json" else "|".join(row)


def json_key_fields(rows):
    """Id-like fields that identify a row of a JSON table, if they are unique."""
    if not rows:
        return None
    cands = [k for k, v in rows[0].items() if isinstance(v, int) and not isinstance(v, bool)
             and (k.lower().endswith("id") or k.lower().endswith("no") or k.lower().endswith("idx") or k.lower().endswith("index"))]
    cands.sort()
    for n in range(1, len(cands) + 1):
        keys = cands[:n]
        seen = set()
        ok = True
        for r in rows:
            k = tuple(r.get(x) for x in keys)
            if k in seen:
                ok = False
                break
            seen.add(k)
        if ok:
            return keys
    return None


def row_keys(table):
    """Returns a function row -> identity key for this table (full text when no unique id)."""
    if table.kind == "json":
        fields = json_key_fields(table.rows)
        if fields:
            return lambda r: ("id",) + tuple(r.get(x) for x in fields)
        return lambda r: ("row", row_text("json", r))
    col0 = [r[0] for r in table.rows if r]
    if len(col0) == len(set(col0)):
        return lambda r: ("id", r[0] if r else "")
    return lambda r: ("row", row_text("pipe", r))


def event_id_of(name, kind, row):
    if kind == "json":
        v = row.get("eventId")
        return v if isinstance(v, int) and v else None
    col = PIPE_EVENT_COL.get(name)
    if col is not None and len(row) > col and row[col].isdigit():
        return int(row[col]) or None
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--current", required=True, help="the Resources folder Puniemu uses")
    ap.add_argument("--dumps", required=True, nargs="+", help="folder(s) that contain the extracted dumps")
    ap.add_argument("--out", default="Events/imported")
    ap.add_argument("--no-missing-rows", action="store_true", help="only write the event packs")
    args = ap.parse_args()

    current = load_folder(args.current)
    if not current:
        sys.exit(f"No game tables found in {args.current}")
    cur_keys = {}
    cur_events = set()
    for name, tab in current.items():
        keyf = row_keys(tab)
        cur_keys[name] = (keyf, {keyf(r) for r in tab.rows})
    if "ywp_mst_event" in current:
        cur_events = {r.get("eventId") for r in current["ywp_mst_event"].rows}
    cur_map_ids = {r.get("mapId") for r in current["ywp_mst_map"].rows} if "ywp_mst_map" in current else set()

    dumps = [d for root in args.dumps for d in find_dumps(root) if os.path.abspath(d) != os.path.abspath(args.current)]
    if not dumps:
        sys.exit("No dumps found (no folder with ywp_mst_* files)")
    print(f"{len(dumps)} dump folder(s) found")

    events = collections.defaultdict(lambda: {"mst": None, "tables": collections.defaultdict(dict), "dumps": set()})
    missing = collections.defaultdict(dict)  # table -> key -> row
    kinds = {}
    for d in dumps:
        tables = load_folder(d)
        label = os.path.relpath(d, args.dumps[0]) if len(args.dumps) == 1 else d
        print(f"  {label}: {len(tables)} tables")
        if "ywp_mst_event" in tables:
            for r in tables["ywp_mst_event"].rows:
                eid = r.get("eventId")
                if isinstance(eid, int) and eid:
                    events[eid]["mst"] = r
                    events[eid]["dumps"].add(label)
        for name, tab in tables.items():
            if name in SKIP_TABLES:
                continue
            kinds[name] = tab.kind
            if name not in current:
                continue  # Puniemu can only serve tables that exist in Resources
            keyf, have = cur_keys[name]
            for r in tab.rows:
                k = keyf(r)
                eid = event_id_of(name, tab.kind, r)
                is_event_table = name.startswith("ywp_mst_event_") or name == "ywp_mst_map"
                if eid and is_event_table:
                    if k not in have:
                        events[eid]["tables"][name][row_text(tab.kind, r)] = r
                        events[eid]["dumps"].add(label)
                elif k not in have:
                    missing[name][k] = r

    os.makedirs(args.out, exist_ok=True)
    report = []
    written = 0
    for eid in sorted(events):
        ev = events[eid]
        has_content = any(ev["tables"].values())
        if eid in cur_events and not has_content:
            continue
        if not has_content and ev["mst"] is None:
            continue
        maps = list(ev["tables"].get("ywp_mst_map", {}).values())
        name = (ev["mst"] or {}).get("description") or (maps[0].get("mapName") if maps else "") or f"Event #{eid}"
        period = ""
        dated = [m for m in maps if m.get("openDt")]
        if dated:
            period = f"{min(m['openDt'] for m in dated)[:10]} ~ {max((m.get('closeDt') or '') for m in dated)[:10]}"
        pack = {
            "eventId": eid,
            "name": name,
            "description": f"Imported from {len(ev['dumps'])} dump(s)",
            "period": period,
            "tables": {},
        }
        if ev["mst"] is not None:
            pack["mstEvent"] = ev["mst"]
        for tname, rows in sorted(ev["tables"].items()):
            if kinds.get(tname) == "pipe":
                pack["tables"][tname] = "*".join("|".join(r) for r in rows.values())
            else:
                pack["tables"][tname] = list(rows.values())
        with open(os.path.join(args.out, f"event_{eid}.json"), "w", encoding="utf-8") as f:
            json.dump(pack, f, ensure_ascii=False, indent=1)
        written += 1
        counts = ", ".join(f"{t.replace('ywp_mst_', '')}={len(r)}" for t, r in sorted(ev["tables"].items()))
        report.append(f"event {eid}: {name} [{period}] {counts}")

    if not args.no_missing_rows and missing:
        data = {"eventId": 0, "name": "Rows missing from Resources", "tables": {}}
        for tname, rows in sorted(missing.items()):
            if kinds.get(tname) == "pipe":
                data["tables"][tname] = "*".join("|".join(r) for r in rows.values())
            else:
                data["tables"][tname] = list(rows.values())
            report.append(f"missing rows: {tname} = {len(rows)}")
        with open(os.path.join(args.out, "_missing_rows.json"), "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False)

    with open(os.path.join(args.out, "report.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(report) + "\n")
    print(f"\n{written} event pack(s) written to {args.out}")
    print(f"{sum(len(v) for v in missing.values())} missing row(s) in {len(missing)} table(s)")
    print(f"details: {os.path.join(args.out, 'report.txt')}")
    if cur_map_ids:
        new_maps = sum(len(ev['tables'].get('ywp_mst_map', {})) for ev in events.values())
        print(f"{new_maps} event map(s) not in your current data")


if __name__ == "__main__":
    main()
