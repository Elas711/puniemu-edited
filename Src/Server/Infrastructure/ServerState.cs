using Newtonsoft.Json;

namespace Puniemu.Src.Server.Infrastructure
{
    // A ban placed from the admin panel.
    public class BanEntry
    {
        [JsonProperty("reason")]
        public string Reason { get; set; } = "";

        [JsonProperty("date")]
        public string Date { get; set; } = "";
    }

    public class ItemGrant
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    // A server-wide gift: every player receives it once, on their next login.
    public class GiftEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        [JsonProperty("ymoney")]
        public int YMoney { get; set; }

        [JsonProperty("spirits")]
        public int Spirits { get; set; }

        [JsonProperty("items")]
        public List<ItemGrant> Items { get; set; } = new();

        [JsonProperty("youkai")]
        public List<long> Youkai { get; set; } = new();

        [JsonProperty("created")]
        public string Created { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // Which events the server serves. See EventManager for how this is applied.
    public class EventSettings
    {
        // "default": serve the master data exactly as shipped in Resources.
        // "custom":  serve only the events listed in ActiveEventIds (plus always-on events).
        // "rotation": cycle automatically through the playable events (or the ActiveEventIds).
        [JsonProperty("mode")]
        public string Mode { get; set; } = "default";

        [JsonProperty("activeEventIds")]
        public List<long> ActiveEventIds { get; set; } = new();

        // Players may pick their own events from the in-game event page.
        [JsonProperty("allowPlayerChoice")]
        public bool AllowPlayerChoice { get; set; } = true;

        // In custom mode, remove the maps of events that are not selected.
        [JsonProperty("hideInactiveEventMaps")]
        public bool HideInactiveEventMaps { get; set; } = true;

        // How many days a re-activated event stays open (counted from "now" on every request).
        [JsonProperty("eventDurationDays")]
        public int EventDurationDays { get; set; } = 30;

        // Rotation mode: how many days each slot lasts and how many events run at once.
        [JsonProperty("rotationDays")]
        public int RotationDays { get; set; } = 7;

        [JsonProperty("rotationCount")]
        public int RotationCount { get; set; } = 2;
    }

    // Everything an operator can change at runtime from the admin panel. Persisted to
    // server_state.json so it survives restarts.
    public class GachaSettings
    {
        // Coins for the crank-a-kai machines from the in-game Crank-a-kai page:
        // "off", "free" (FreeCoinsPerDay a day) or "ymoney" (CoinPriceYMoney each)
        [JsonProperty("coinShop")]
        public string CoinShop { get; set; } = "free";

        [JsonProperty("freeCoinsPerDay")]
        public int FreeCoinsPerDay { get; set; } = 3;

        [JsonProperty("coinPriceYMoney")]
        public int CoinPriceYMoney { get; set; } = 3000;

        // Players may mix machines: their Y-Money crank-a-kai draws from the merged pools
        [JsonProperty("allowMix")]
        public bool AllowMix { get; set; } = true;

        // Mix used for players who did not choose one (empty = the normal machine)
        [JsonProperty("serverMix")]
        public List<int> ServerMix { get; set; } = new();
    }

    public class ServerState
    {
        [JsonProperty("gacha")]
        public GachaSettings Gacha { get; set; } = new();

        [JsonProperty("maintenanceMode")]
        public bool MaintenanceMode { get; set; } = false;

        [JsonProperty("maintenanceMessage")]
        public string MaintenanceMessage { get; set; } = "The server is under maintenance.\nPlease try again later.";

        // Shown on the in-game notice page.
        [JsonProperty("motd")]
        public string Motd { get; set; } = "Welcome to Puniemu!";

        // Multiplies the Y-Money players earn from battles.
        [JsonProperty("ymoneyMultiplier")]
        public double YMoneyMultiplier { get; set; } = 1.0;

        // Multiplies the EXP players and Yo-kai earn from battles.
        [JsonProperty("expMultiplier")]
        public double ExpMultiplier { get; set; } = 1.0;

        // Never run out of spirits (hitodama).
        [JsonProperty("infiniteSpirits")]
        public bool InfiniteSpirits { get; set; } = false;

        [JsonProperty("events")]
        public EventSettings Events { get; set; } = new();

        [JsonProperty("gifts")]
        public List<GiftEntry> Gifts { get; set; } = new();

        // gdkey -> ban
        [JsonProperty("bans")]
        public Dictionary<string, BanEntry> Bans { get; set; } = new();
    }

    public static class ServerStateStore
    {
        private static readonly object _lock = new();
        private static string _path = "server_state.json";

        public static ServerState Current { get; private set; } = new();

        // Raised after the state changed (used to drop caches of transformed tables).
        public static event Action? Changed;

        public static void Load(string path)
        {
            _path = path;
            lock (_lock)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        Current = JsonConvert.DeserializeObject<ServerState>(File.ReadAllText(path)) ?? new ServerState();
                        Log.Info("state", $"Loaded runtime state from {path}");
                    }
                    else
                    {
                        Current = new ServerState();
                        SaveNoLock();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("state", $"Could not read {path}, using defaults: {ex.Message}");
                    Current = new ServerState();
                }
            }
        }

        // Applies a change and persists it.
        public static void Update(Action<ServerState> change)
        {
            lock (_lock)
            {
                change(Current);
                SaveNoLock();
            }
            Changed?.Invoke();
        }

        private static void SaveNoLock()
        {
            try
            {
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Current, Formatting.Indented));
                File.Move(tmp, _path, true);
            }
            catch (Exception ex)
            {
                Log.Error("state", $"Could not save {_path}: {ex.Message}");
            }
        }

        public static bool IsBanned(string? gdkey, out BanEntry? ban)
        {
            ban = null;
            if (string.IsNullOrEmpty(gdkey)) return false;
            return Current.Bans.TryGetValue(gdkey, out ban);
        }
    }
}
