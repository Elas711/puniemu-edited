using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Puniemu.Src.Server.Events
{
    // One event the server knows about.
    public class EventInfo
    {
        [JsonProperty("eventId")]
        public long EventId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        // When the event originally ran (free text, from event packs or map dates).
        [JsonProperty("period")]
        public string Period { get; set; } = "";

        // Series from Events/history.json, when linked.
        [JsonProperty("series")]
        public string Series { get; set; } = "";

        [JsonProperty("eventType")]
        public int EventType { get; set; }

        // Maps that belong to this event.
        [JsonProperty("mapIds")]
        public List<long> MapIds { get; set; } = new();

        // Master tables that contain rows for this event.
        [JsonProperty("tables")]
        public SortedSet<string> Tables { get; set; } = new();

        // master  = row present in ywp_mst_event
        // pack    = defined by an Events/*.json pack
        // found   = only referenced by other event tables
        [JsonProperty("sources")]
        public SortedSet<string> Sources { get; set; } = new();

        // Selectable events are switched on/off by the event selection. Everything else (shop offers,
        // Y-point counters...) is left exactly as shipped.
        [JsonProperty("selectable")]
        public bool Selectable { get; set; }

        // True when the event's map/quest data is available so it can actually be played.
        [JsonProperty("playable")]
        public bool Playable => MapIds.Count > 0 || Tables.Any(t => t.StartsWith("ywp_mst_event_quest") || t == "ywp_mst_event_stage_assist");

        [JsonIgnore]
        public JObject? MstEventRow { get; set; }

        [JsonIgnore]
        public List<string> Hints { get; set; } = new();

        [JsonIgnore]
        public EventPack? Pack { get; set; }
    }

    // A drop-in event definition (Events/<anything>.json). Lets the community re-create past events by
    // supplying the master rows that the final server data no longer contains.
    public class EventPack
    {
        [JsonProperty("eventId")]
        public long EventId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("period")]
        public string? Period { get; set; }

        // A full ywp_mst_event row. When missing, one is generated.
        [JsonProperty("mstEvent")]
        public JObject? MstEvent { get; set; }

        // Gacha ids that should be opened while the event is active.
        [JsonProperty("gachaIds")]
        public List<long> GachaIds { get; set; } = new();

        // Extra master rows served while the event is active: table name -> JSON array of rows
        // (for JSON tables) or a pipe/asterisk string (for tableData tables).
        [JsonProperty("tables")]
        public Dictionary<string, JToken> Tables { get; set; } = new();

        [JsonIgnore]
        public string FileName { get; set; } = "";
    }

    // One entry of Events/history.json: an event that existed in the live game. EventIds links it to
    // the event ids found in the server data.
    public class HistoryEvent
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("eventIds")]
        public List<long> EventIds { get; set; } = new();

        // Featured characters (images hosted in the community Drive archive, linked not copied)
        [JsonProperty("characters", NullValueHandling = NullValueHandling.Ignore)]
        public List<HistoryCharacter>? Characters { get; set; }

        [JsonProperty("driveFolder", NullValueHandling = NullValueHandling.Ignore)]
        public string? DriveFolder { get; set; }

        // Fan translation of the event story
        [JsonProperty("story", NullValueHandling = NullValueHandling.Ignore)]
        public HistoryLink? Story { get; set; }

        // Free text the server owner can add (tips, notes...)
        [JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
        public string? Notes { get; set; }
    }

    public class HistoryCharacter
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        // Google Drive file id of the picture
        [JsonProperty("image")]
        public string Image { get; set; } = "";
    }

    public class HistoryLink
    {
        [JsonProperty("title")]
        public string Title { get; set; } = "";

        [JsonProperty("url")]
        public string Url { get; set; } = "";
    }

    public class HistorySeries
    {
        [JsonProperty("category")]
        public string Category { get; set; } = "";

        [JsonProperty("series")]
        public string Series { get; set; } = "";

        [JsonProperty("events")]
        public List<HistoryEvent> Events { get; set; } = new();
    }

    public class EventHistory
    {
        [JsonProperty("_info")]
        public string? Info { get; set; }

        [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
        public string? Source { get; set; }

        [JsonProperty("series")]
        public List<HistorySeries> Series { get; set; } = new();

        [JsonProperty("otherStories", NullValueHandling = NullValueHandling.Ignore)]
        public List<HistoryLink>? OtherStories { get; set; }
    }

    // What a player (or the server) wants to play.
    public class EventSelection
    {
        public static readonly EventSelection None = new() { Enabled = false };

        public bool Enabled { get; set; }
        public HashSet<long> ActiveIds { get; set; } = new();
        public bool HideInactiveMaps { get; set; } = true;
        public int DurationDays { get; set; } = 30;

        public string Key => Enabled
            ? (HideInactiveMaps ? "h" : "s") + DurationDays + ":" + string.Join(",", ActiveIds.OrderBy(x => x))
            : "none";
    }

    // Stored in the player's save under "puniemu_events".
    public class PlayerEventChoice
    {
        // "server" = follow the server's selection, "custom" = use Ids.
        [JsonProperty("mode")]
        public string Mode { get; set; } = "server";

        [JsonProperty("ids")]
        public List<long> Ids { get; set; } = new();
    }
}
