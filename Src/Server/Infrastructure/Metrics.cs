using System.Collections.Concurrent;
using System.Diagnostics;

namespace Puniemu.Src.Server.Infrastructure
{
    // In-process request statistics for the admin panel and /status.
    public static class Metrics
    {
        public class EndpointStats
        {
            public long Count;
            public long Errors;
            public long TotalMs;
        }

        public static readonly DateTime StartedAt = DateTime.UtcNow;
        private static readonly ConcurrentDictionary<string, EndpointStats> _endpoints = new();
        private static readonly ConcurrentDictionary<string, long> _counters = new();
        private static readonly ConcurrentDictionary<string, DateTime> _activePlayers = new();
        private const int MAX_ENDPOINTS = 200;

        public static void Record(string path, long ms, bool error)
        {
            // Unknown paths are chosen by whoever sends them; fold them together once the table is full.
            if (!_endpoints.ContainsKey(path) && _endpoints.Count >= MAX_ENDPOINTS) path = "<other>";
            var s = _endpoints.GetOrAdd(path, _ => new EndpointStats());
            Interlocked.Increment(ref s.Count);
            Interlocked.Add(ref s.TotalMs, ms);
            if (error) Interlocked.Increment(ref s.Errors);
        }

        public static void Incr(string name, long by = 1) => _counters.AddOrUpdate(name, by, (_, v) => v + by);

        public static void SeenPlayer(string gdkey) => _activePlayers[gdkey] = DateTime.UtcNow;

        // Players that sent a request during the last `minutes` minutes.
        public static int ActivePlayers(int minutes = 15)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            foreach (var kv in _activePlayers)
            {
                if (kv.Value < cutoff) _activePlayers.TryRemove(kv.Key, out _);
            }
            return _activePlayers.Count;
        }

        public static object Snapshot()
        {
            return new
            {
                uptimeSec = (long)(DateTime.UtcNow - StartedAt).TotalSeconds,
                activePlayers15m = ActivePlayers(),
                memoryMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024),
                counters = _counters.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
                endpoints = _endpoints
                    .OrderByDescending(x => x.Value.Count)
                    .Select(x => new
                    {
                        path = x.Key,
                        count = x.Value.Count,
                        errors = x.Value.Errors,
                        avgMs = x.Value.Count == 0 ? 0 : x.Value.TotalMs / x.Value.Count
                    })
                    .ToList()
            };
        }
    }
}
