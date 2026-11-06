using System.Collections.Concurrent;

namespace Puniemu.Src.Server.Infrastructure
{
    // Fixed one-minute window per client IP. Static downloads and health checks are exempt because a
    // single game install fetches thousands of asset files.
    public static class RateLimiter
    {
        private class Window
        {
            public long Minute;
            public int Count;
        }

        private static readonly ConcurrentDictionary<string, Window> _windows = new();
        private static long _lastCleanupMinute;

        public static async Task InvokeAsync(HttpContext ctx, Func<Task> next)
        {
            int limit = DataManager.Logic.DataManager.RateLimitPerMinute;
            var path = ctx.Request.Path.Value ?? "";
            if (limit <= 0 || !IsLimited(path))
            {
                await next();
                return;
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            long minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            var w = _windows.GetOrAdd(ip, _ => new Window { Minute = minute });
            int count;
            lock (w)
            {
                if (w.Minute != minute)
                {
                    w.Minute = minute;
                    w.Count = 0;
                }
                count = ++w.Count;
            }
            Cleanup(minute);

            // Admin and auth endpoints get a much smaller budget (they send mail / guard operator data).
            int effective = path.StartsWith("/admin/api") || path.StartsWith("/auth/") ? Math.Max(30, limit / 10) : limit;
            if (count > effective)
            {
                Metrics.Incr("rate_limited");
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.Response.Headers["Retry-After"] = "60";
                if (path.Contains(".nhn"))
                    await GameRequestMiddleware.WriteDialog(ctx, "Too many requests.\nPlease wait a moment.", "Busy");
                return;
            }
            await next();
        }

        private static bool IsLimited(string path)
        {
            return path.Contains(".nhn") || path.StartsWith("/admin/api") || path.StartsWith("/auth/") ||
                   path.StartsWith("/web/api") || path.StartsWith("/l5id") || path.StartsWith("/api/");
        }

        private static void Cleanup(long minute)
        {
            if (Interlocked.Exchange(ref _lastCleanupMinute, minute) == minute) return;
            foreach (var kv in _windows)
            {
                if (kv.Value.Minute < minute - 1) _windows.TryRemove(kv.Key, out _);
            }
        }
    }
}

namespace Puniemu.Src.Server.Infrastructure
{
    // Per-save token buckets for the calls bots farm (idea from WibWobPS/JibaAC). Far above what a
    // person can do in the game (a crank-a-kai or a battle takes seconds); "OperationRateLimit": false
    // turns it off.
    public static class OperationLimiter
    {
        private record Bucket(double Capacity, double PerSecond);

        private static readonly Dictionary<string, Bucket> LIMITS = new(StringComparer.OrdinalIgnoreCase)
        {
            ["executeGacha"] = new(10, 0.5),
            ["gameEnd"] = new(10, 0.5),
            ["gameEndScoreAttack"] = new(10, 0.5),
            ["receivePresentBox"] = new(12, 1),
            ["missionReward"] = new(30, 2),
            ["missionRewardAll"] = new(10, 0.5),
            ["login"] = new(10, 0.2),
            ["createUser"] = new(5, 0.05),
        };

        private class State { public double Tokens; public DateTime At; }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, State> _state = new();

        public static bool Allow(string gdkey, string path)
        {
            if (!DataManager.Logic.DataManager.OperationRateLimit) return true;
            var name = path.Trim('/');
            var dot = name.IndexOf(".nhn", StringComparison.OrdinalIgnoreCase);
            if (dot >= 0) name = name[..dot];
            if (!LIMITS.TryGetValue(name, out var b)) return true;
            var s = _state.GetOrAdd(gdkey + "|" + name, _ => new State { Tokens = b.Capacity, At = DateTime.UtcNow });
            lock (s)
            {
                var now = DateTime.UtcNow;
                s.Tokens = Math.Min(b.Capacity, s.Tokens + (now - s.At).TotalSeconds * b.PerSecond);
                s.At = now;
                if (s.Tokens < 1)
                {
                    Metrics.Incr("operation_limited");
                    return false;
                }
                s.Tokens -= 1;
                return true;
            }
        }
    }
}
