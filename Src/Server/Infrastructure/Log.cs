namespace Puniemu.Src.Server.Infrastructure
{
    // Tiny console logger with timestamps and levels. Also keeps the last lines in memory so the
    // admin panel can show them.
    public static class Log
    {
        private static readonly object _lock = new();
        private static readonly LinkedList<string> _recent = new();
        private const int MAX_RECENT = 300;

        public static void Info(string area, string msg) => Write("INFO", area, msg, ConsoleColor.Gray);
        public static void Warn(string area, string msg) => Write("WARN", area, msg, ConsoleColor.Yellow);
        public static void Error(string area, string msg) => Write("ERROR", area, msg, ConsoleColor.Red);

        private static void Write(string level, string area, string msg, ConsoleColor color)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level,-5} [{area}] {msg}";
            lock (_lock)
            {
                var old = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(line);
                Console.ForegroundColor = old;
                _recent.AddLast(line);
                while (_recent.Count > MAX_RECENT) _recent.RemoveFirst();
            }
        }

        public static List<string> Recent()
        {
            lock (_lock) return _recent.ToList();
        }
    }
}
