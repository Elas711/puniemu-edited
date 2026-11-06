using System.Collections.Concurrent;

namespace Puniemu.Src.Server.Infrastructure
{
    // Plausibility checks of battle results (ideas from WibWobPS/JibaAC, MIT, rewritten for Puniemu).
    // "AntiCheatMode": off | log (default: findings go to the log and the admin panel) | enforce
    // (a result with a finding is rejected). "AntiCheatMinBattleSeconds" (default 8) and
    // "AntiCheatMaxScorePerSecond" (default 5000000) tune the limits; real scores of late-game teams
    // are high, so keep "log" until you have looked at your own players' numbers.
    public static class AntiCheat
    {
        public record Finding(DateTime At, string Gdkey, string Rule, string Detail);

        private static readonly ConcurrentQueue<Finding> _recent = new();
        private static readonly ConcurrentDictionary<string, int> _perPlayer = new();
        private const int MAX_RECENT = 300;

        public static string Mode => DataManager.Logic.DataManager.AntiCheatMode;
        public static IEnumerable<Finding> Recent => _recent.Reverse();
        public static IReadOnlyDictionary<string, int> PerPlayer => _perPlayer;

        // Battle start time (server clock), stored with the session so it survives a restart
        public const string BATTLE_STARTED = "puniemu_battle_started";

        // Returns the findings of one gameEnd / gameRetire; the caller rejects the result when
        // Rejects() is true for them.
        public static List<Finding> CheckBattle(string gdkey, long? startedMs, bool cleared, int score, int clearTimeSec,
            int comboMax, int cheatFlg, IEnumerable<int> otherCounters)
        {
            var found = new List<Finding>();
            if (Mode == "off") return found;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            void Add(string rule, string detail) => found.Add(new Finding(DateTime.UtcNow, gdkey, rule, detail));

            // the game's own tamper detection
            if (cheatFlg != 0) Add("client.cheat_flag", $"client reported cheatFlg={cheatFlg}");

            if (score < 0 || comboMax < 0 || clearTimeSec < 0 || otherCounters.Any(v => v < 0))
                Add("result.negative", "negative score / counter in the result");

            if (startedMs is long started && started > 0)
            {
                var elapsedSec = Math.Max(0, (now - started) / 1000.0);
                int minSec = DataManager.Logic.DataManager.AntiCheatMinBattleSeconds;
                if (cleared && elapsedSec < minSec)
                    Add("battle.too_fast", $"cleared {elapsedSec:0.0}s after the start (minimum {minSec}s)");
                // the clear time the client reports can't be longer than the battle really took
                if (clearTimeSec > elapsedSec + 30)
                    Add("battle.impossible_time", $"clearTimeSec {clearTimeSec} but only {elapsedSec:0}s since the start");
                long cap = 1_000_000 + (long)(elapsedSec * DataManager.Logic.DataManager.AntiCheatMaxScorePerSecond);
                if (score > cap)
                    Add("battle.impossible_score", $"score {score} above {cap} for {elapsedSec:0}s");
            }

            foreach (var f in found) Record(f);
            return found;
        }

        public static bool Rejects(List<Finding> findings) => Mode == "enforce" && findings.Count > 0;

        // A befriend the client claims must be one the server's lottery allowed: gameStart sends a
        // lotResult per soultimate pattern ("10110": one bit per food level, none .. 4-heart).
        // Lenient on purpose: any pattern allowing that food level is enough (as in WibWobPS/wwps).
        // Returns false when the befriend must be dropped (enforce).
        public static bool BefriendAllowed(string gdkey, long enemyId, IEnumerable<string> lotResults, int foodLevel)
        {
            if (Mode == "off") return true;
            var results = lotResults.Where(r => !string.IsNullOrEmpty(r)).ToList();
            // no lottery at all = the enemy can't be befriended in this battle (no food type, before the
            // Jibanyan tutorial, soultimate maxed); story befriends get "11111"
            if (results.Any(r => foodLevel < r.Length && r[foodLevel] == '1')) return true;
            Record(new Finding(DateTime.UtcNow, gdkey, "befriend.not_rolled",
                $"befriended enemy {enemyId} with food level {foodLevel}, lottery {(results.Count == 0 ? "none" : string.Join(",", results.Distinct().Take(4)))}"));
            return Mode != "enforce";
        }

        private static void Record(Finding f)
        {
            _recent.Enqueue(f);
            while (_recent.Count > MAX_RECENT && _recent.TryDequeue(out _)) { }
            _perPlayer.AddOrUpdate(f.Gdkey, 1, (_, n) => n + 1);
            Metrics.Incr("anticheat_" + f.Rule.Replace('.', '_'));
            Log.Warn("anticheat", $"{f.Gdkey}: {f.Rule} - {f.Detail}" + (Mode == "enforce" ? " (rejected)" : ""));
        }
    }
}
