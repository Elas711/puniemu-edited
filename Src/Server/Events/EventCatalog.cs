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
