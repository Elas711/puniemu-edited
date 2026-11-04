using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;

namespace Puniemu.Src.Server.CustomAuth
{
    public static class AuthManager
    {
        // code -> (email, udkey, expiration)
        public static ConcurrentDictionary<int, (string email, bool isLink, string udkey, DateTime expires)> CodeCache = new();

        // udkey -> (failed attempts, locked until)
        private static readonly ConcurrentDictionary<string, (int fails, DateTime lockedUntil)> _attempts = new();
        private const int MAX_FAILED_ATTEMPTS = 5;
        private static readonly TimeSpan LOCKOUT = TimeSpan.FromMinutes(15);

        public static bool IsLockedOut(string? udkey)
        {
            if (string.IsNullOrEmpty(udkey)) return false;
            return _attempts.TryGetValue(udkey, out var a) && a.lockedUntil > DateTime.UtcNow;
        }

        public static void RegisterFailure(string? udkey)
        {
            if (string.IsNullOrEmpty(udkey)) return;
            _attempts.AddOrUpdate(udkey, _ => (1, DateTime.MinValue), (_, a) =>
            {
                var fails = a.fails + 1;
                return fails >= MAX_FAILED_ATTEMPTS ? (0, DateTime.UtcNow + LOCKOUT) : (fails, a.lockedUntil);
            });
        }

        public static void RegisterSuccess(string? udkey)
        {
            if (!string.IsNullOrEmpty(udkey)) _attempts.TryRemove(udkey, out _);
        }

        // Returns a fresh 6 digit code from a cryptographic source.
        public static int NewCode()
        {
            CleanupExpiredCodes();
            int code;
            do
            {
                code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000);
            }
            while (CodeCache.ContainsKey(code));
            return code;
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email.Trim();
            }
            catch
            {
                return false;
            }
        }

        public static void CleanupExpiredCodes()
        {
            foreach (var entry in CodeCache)
            {
                if (DateTime.UtcNow > entry.Value.expires)
                {
                    CodeCache.TryRemove(entry.Key, out _);
                }
            }
        }

        public static async Task SendCodeEmail(string email, int code)
        {
            var myEmail = DataManager.Logic.DataManager.EmailForAuthMessages;
            var pass = DataManager.Logic.DataManager.AppPasswordForAuthMessages;
            using var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                EnableSsl = true,
                Credentials = new NetworkCredential(
                    myEmail,
                    pass
                )
            };

            var mail = new MailMessage
            {
                From = new MailAddress(myEmail),
                Subject = "Your account management code",
                Body = $"Your account management code is: {code}\n\nTo continue this process, enter the code in the \"Confirm action\" menu in the game settings.\nThis code expires in 15 minutes.",
                IsBodyHtml = false
            };

            mail.To.Add(email);

            await client.SendMailAsync(mail);
        }

    }
}
