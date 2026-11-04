# Events

Puniemu can switch the game's events on and off. Open the admin panel (`/admin` → **Events**) or let
players choose their own events in game (**Help → Events & Server**).

## Where the event list comes from

The official servers only ever sent the events that were running at that moment, so no data dump
contains every event that ever existed. Puniemu builds its event list from:

1. every row of `ywp_mst_event` in your `Resources` folder,
2. every map in `ywp_mst_map` that belongs to an event (`eventId`),
3. every other event table (`ywp_mst_event_quest`, `ywp_mst_event_stage_assist`,
   `ywp_mst_event_youkai_assist`, `ywp_mst_event_point_trade`, ...) - the leftovers of past events,
4. **event packs**: any `*.json` file in this folder.

An event marked **no maps** in the list has some data (bonuses, rewards...) but no map, so turning it
on may not give you anything to play until you add its maps with a pack.

## What switching does

With an event selection active, the server rewrites what it sends to the client:

* selected events get a `ywp_mst_event` row (taken from the data, from a pack, or generated),
* their maps and quests are re-opened from yesterday until *now + event length* (default 30 days),
* event maps of events that are **not** selected are closed (optional),
* normal story maps are never touched.

The data in `Resources` is never modified. Players have to go back to the title screen to receive the
new event data.

## Friendly names

`names.json` lets you give events readable names:

```json
{
  "1097": "Code Geass collaboration",
  "1112": "Yo-kai World event"
}
```

## Event packs

A pack defines (or completes) one event. Save it as `Events/<anything>.json`. A file can also contain
an array of packs.

```json
{
  "eventId": 900001,
  "name": "My custom event",
  "description": "Shown in the event lists",
  "period": "originally 2020-08-01 ~ 2020-08-15",

  "mstEvent": { "eventId": 900001, "eventType": 1, "eventStatus": 1, "description": "My custom event" },

  "gachaIds": [ 500055 ],

  "tables": {
    "ywp_mst_map": [
      { "mapId": 90001, "eventId": 900001, "mapType": 3, "mapSubType": 1, "mapName": "My event map" }
    ],
    "ywp_mst_event_quest": "900001|0|0|Quest text|1|26051|1|2020-08-15 23:59:59"
  }
}
```

* `mstEvent` - the `ywp_mst_event` row. Copy a real row from your data and change the ids; if it is
  left out a row is generated from an existing one.
* `gachaIds` - gachas whose start/end dates are opened while the event is on.
* `tables` - extra rows served only while the event is on. JSON tables take an array of rows,
  pipe tables (`tableData`) take the rows as a `|` / `*` string. The table must exist in `Resources`.

After adding or editing packs press **Rescan data** in the admin panel (or restart the server).
