using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puniemu.Src.Server.GameServer.DataClasses;
using Puniemu.Src.UserDataManager.DataClasses;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Puniemu.Src.UserDataManager.Logic
{
    public static class UserDataManager
    {
        public class TableNotFoundException : Exception
        {
            public TableNotFoundException() : base() { }
        }

        // Thrown when the account cache is at capacity and a not-yet-cached player tries to load
        public class ServerFullException : Exception
        {
            public ServerFullException() : base() { }
        }

        private static ISaveStore? _store;
        public static string StoreName => _store?.Name ?? "-";

        private static ConcurrentDictionary<string, Account> _accountCache = new();

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _loadLocks = new();

        // Timer variables for background flushing
        private static PeriodicTimer? _flushTimer;
        private static Task? _flushTask;
        private static CancellationTokenSource _cts = new();

        private static SemaphoreSlim GetAccountLock(string gdkey) => _accountLocks.GetOrAdd(gdkey, _ => new SemaphoreSlim(1, 1));

        private static SemaphoreSlim GetDeviceLock(string udkey) => _deviceLocks.GetOrAdd(udkey, _ => new SemaphoreSlim(1, 1));

        // Opens the save store: SQLite (one file, default when no PostgreSQL connection string is set)
        // or PostgreSQL ("Database" in appsettings.json).
        public static void Initialize()
        {
            var dm = DataManager.Logic.DataManager.Database;
            try
            {
                if (dm == "postgres")
                {
                    if (string.IsNullOrWhiteSpace(DataManager.Logic.DataManager.PostgresConnectionString))
                        throw new InvalidOperationException("'PostgresConnectionString' is missing from appsettings.json");
                    _store = new PostgresSaveStore(DataManager.Logic.DataManager.PostgresConnectionString!);
                }
                else
                    _store = new SqliteSaveStore(DataManager.Logic.DataManager.ResolvePath(DataManager.Logic.DataManager.SqlitePath));
                _store.EnsureSchema();

                _flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
                _flushTask = FlushLoopAsync(_cts.Token);
                Console.WriteLine($"db service started: {_store.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't open the save database ({dm}): {ex.Message}");
                Console.WriteLine(dm == "postgres"
                    ? "Check 'PostgresConnectionString' in appsettings.json and that the database server is running."
                    : "Check 'SqlitePath' in appsettings.json (the folder must be writable).");
                Environment.Exit(1);
            }
        }

        // Accounts that were not touched for this long and have nothing left to save are dropped from the cache.
        private static readonly TimeSpan CACHE_IDLE_EVICT = TimeSpan.FromMinutes(10);

        private static async Task FlushLoopAsync(CancellationToken token)
        {
            try
            {
                while (await _flushTimer!.WaitForNextTickAsync(token))
                {
                    // one failed round must not stop saving for the rest of the server's life
                    try { await FlushAllDirtyAccountsAsync(); }
                    catch (Exception ex) { Console.WriteLine($"Error in background flush: {ex.Message}"); }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("bg flush stopped gracefully");
            }
        }

        private static async Task FlushAllDirtyAccountsAsync()
        {
            await Parallel.ForEachAsync(_accountCache.Keys, async (gdkey, _) =>
            {
                await FlushAccount(gdkey);
            });
        }

        private static async Task FlushAccount(string gdkey)
        {
            if (!_accountCache.TryGetValue(gdkey, out var account)) return;

            var accountLock = GetAccountLock(gdkey);
            await accountLock.WaitAsync();
            try
            {
                if (account.IsDirty)
                {
                    account.IsDirty = false;

                    try
                    {
                        await UpdateAccountNoLockAsync(account);
                        Console.WriteLine($"Saved account. gdkey:" + gdkey);
                    }
                    catch (Exception ex)
                    {
                        account.IsDirty = true;
                        Console.WriteLine($"Failed to flush account {gdkey}: {ex.Message}");
                    }
                }
                else if (DateTime.UtcNow - account.LastAccessUtc > CACHE_IDLE_EVICT)
                {
                    // Only evict accounts nobody used recently, otherwise a handler still holding the
                    // object would write into an instance that is no longer cached (and never saved).
                    _accountCache.TryRemove(gdkey, out _);
                }
            }
            finally
            {
                accountLock.Release();
            }
        }
        public static async Task ShutdownAsync()
        {
            _cts.Cancel();
            await FlushAllDirtyAccountsAsync();
        }

        public static async Task<Account> GetAccountFromGdkeyAsync(string gdkey)
        {
            if (string.IsNullOrEmpty(gdkey)) return null!;
            if (_accountCache.TryGetValue(gdkey, out Account? acc))
            {
                acc.LastAccessUtc = DateTime.UtcNow;
                return acc;
            }

            // Serialize loads of the same account so two concurrent requests can't each load
            // their own copy (the loser's changes would otherwise be lost).
            var loadLock = _loadLocks.GetOrAdd(gdkey, _ => new SemaphoreSlim(1, 1));
            await loadLock.WaitAsync();
            try
            {
                if (_accountCache.TryGetValue(gdkey, out acc))
                {
                    acc.LastAccessUtc = DateTime.UtcNow;
                    return acc;
                }

                ThrowIfCacheFull();

                var account = await _store!.LoadAccountAsync(gdkey);
                if (account != null)
                {
                    account.YwpUserTables ??= new();
                    account.LastAccessUtc = DateTime.UtcNow;
                    _accountCache[gdkey] = account;
                }
                return account!;
            }
            finally
            {
                loadLock.Release();
            }
        }

        public static async Task<string> NewDeviceAsync(string? udkey = null)
        {
            udkey ??= Guid.NewGuid().ToString();
            return await _store!.NewDeviceAsync(udkey);
        }

        // checks if udkey exists
        public static async Task<bool> IsDeviceExists(string udkey) => await _store!.DeviceExistsAsync(udkey);

        private static void ThrowIfCacheFull()
        {
            if (_accountCache.Count >= DataManager.Logic.DataManager.MaxCachedAccounts)
            {
                throw new ServerFullException();
            }
        }

        // returns the gdkey of the newly created account
        public static async Task<string> NewAccountAsync()
        {
            ThrowIfCacheFull();

            Account acc;
            for (int attempt = 0; ; attempt++)
            {
                var fc = GenerateFriendCode();
                acc = new Account()
                {
                    Gdkey = Guid.NewGuid().ToString(),
                    YwpUserTables = new(),
                    LastLoginTime = "",
                    CharacterId = fc,
                    UserId = System.IO.Hashing.Crc32.HashToUInt32(System.Text.Encoding.UTF8.GetBytes(fc)).ToString(),
                    LastAccessUtc = DateTime.UtcNow
                };
                try
                {
                    await _store!.InsertAccountAsync(acc);
                    break;
                }
                // friend code / user id are unique and the user id is a 32-bit hash: on a clash draw again
                catch (DuplicateSaveKeyException) when (attempt < 8) { }
            }

            // Immediately cache the new account
            _accountCache[acc.Gdkey!] = acc;
            return acc.Gdkey!;
        }

        private static string GenerateFriendCode()
        {
            char[] LetterBytes = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
            const int CodeLength = 8;
            var code = new char[CodeLength];

            byte[] buffer = new byte[CodeLength];

            RandomNumberGenerator.Fill(buffer);

            for (int i = 0; i < code.Length; i++)
            {
                code[i] = LetterBytes[buffer[i] % LetterBytes.Length];
            }

            return new string(code);
        }

        // Sets user data for specific account
        public static async Task SetYwpUserAsync(string gdkey, string tableId, object data)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey);

            var accountLock = GetAccountLock(gdkey);
            await accountLock.WaitAsync();
            try
            {
                account.YwpUserTables![tableId] = data;

                // Mark as dirty instead of updating database immediately
                account.IsDirty = true;
            }
            finally
            {
                accountLock.Release();
            }
        }

        public static async Task SetYwpUserDictAsync(string gdkey, Dictionary<string, object?> data)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey);

            var accountLock = GetAccountLock(gdkey);
            await accountLock.WaitAsync();
            try
            {
                foreach (var kvp in data)
                {
                    account.YwpUserTables![kvp.Key] = kvp.Value!;
                }

                // Mark as dirty instead of updating database immediately
                account.IsDirty = true;
            }
            finally
            {
                accountLock.Release();
            }
        }

        public static async Task SetEntireUserData(string gdkey, Dictionary<string, object?> data)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey);

            var accountLock = GetAccountLock(gdkey);
            await accountLock.WaitAsync();
            try
            {
                account.YwpUserTables = data;

                account.IsDirty = true;
            }
            finally
            {
                accountLock.Release();
            }
        }

        public static async Task DeleteUser(string udkey, string gdkey)
        {
            await RemoveGdkeyFromUdkey(udkey, gdkey);

            var accountLock = GetAccountLock(gdkey);
            await accountLock.WaitAsync();
            try
            {
                await _store!.DeleteAccountAsync(gdkey);

                _accountCache.TryRemove(gdkey, out _);
            }
            finally
            {
                accountLock.Release();
            }
        }

        private static async Task RemoveGdkeyFromUdkey(string udkey, string gdkey)
        {
            Puniemu.Src.Server.Infrastructure.GameRequestMiddleware.ForgetOwnership();
            var deviceLock = GetDeviceLock(udkey);
            await deviceLock.WaitAsync();
            try
            {
                await _store!.RemoveGdkeyFromDeviceAsync(udkey, gdkey);
            }
            finally
            {
                deviceLock.Release();
            }
        }

        // Gets user data from specific account
        public static async Task<T?> GetYwpUserAsync<T>(string gdkey, string tableId)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey);
            if (account == null || account.YwpUserTables == null) return default;
            if (!account.YwpUserTables.TryGetValue(tableId, out var tbl) || tbl == null)
                return default;
            JToken token = JToken.FromObject(tbl);
            var obj = token.ToObject<T>();

            if (obj is YwpUserData ywp)
            {
                await ywp.HitodamaRecover(gdkey);
            }

            return obj;
        }

        public static async Task DeleteYwpUserAsync(string gdkey, string tableId)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey);

            var accountLock = GetAccountLock(gdkey);
            await accountLock.WaitAsync();
            try
            {
                account.YwpUserTables!.Remove(tableId);
                account.IsDirty = true;
            }
            finally
            {
                accountLock.Release();
            }
        }
        public static async Task<T?> GetYwpUserFromJson<T>(string tableId, Dictionary<string, object?> YwpUserTables, string? gdkey)
        {
            if (!YwpUserTables.TryGetValue(tableId, out var tbl) || tbl == null)
                return default;
            JToken token = JToken.FromObject(tbl);
            var obj = token.ToObject<T>();

            if (gdkey != null && obj is YwpUserData ywp)
            {
                await ywp.HitodamaRecover(gdkey);
            }
            return obj;
        }

        public static async Task<Dictionary<string, object?>> GetEntireUserData(string gdkey)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey);
            return account.YwpUserTables!;
        }

        public static async Task<string> GetGdkeyFromCharacterId(string charId)
        {
            return await _store!.GdkeyByCharacterIdAsync(charId);
        }

        public static async Task TransferGdkeys(string udkeyFrom, string udkeyTo)
        {
            Puniemu.Src.Server.Infrastructure.GameRequestMiddleware.ForgetOwnership();
            // Always take the locks in the same order to avoid deadlocks
            var firstKey = string.CompareOrdinal(udkeyFrom, udkeyTo) <= 0 ? udkeyFrom : udkeyTo;
            var secondKey = firstKey == udkeyFrom ? udkeyTo : udkeyFrom;
            var firstLock = GetDeviceLock(firstKey);
            var secondLock = GetDeviceLock(secondKey);

            await firstLock.WaitAsync();
            try
            {
                if (secondKey != firstKey) await secondLock.WaitAsync();
                try
                {
                    // both updates happen in one transaction so a crash can't leave the transfer half-applied
                    await _store!.TransferGdkeysAsync(udkeyFrom, udkeyTo);
                }
                finally
                {
                    if (secondKey != firstKey) secondLock.Release();
                }
            }
            finally
            {
                firstLock.Release();
            }
        }
        public static async Task<Mail> GetDataByMail(string email) => (await _store!.GetMailAsync(email))!;

        public static async Task AddOrEditEmail(string email, string udkey) => await _store!.UpsertMailAsync(email, udkey);

        public static async Task<string> GetGdkeyFromUserId(string userId)
        {
            return await _store!.GdkeyByUserIdAsync(userId);
        }

        public static async Task<string> GetLastLoginTime(string gdkey)
        {
            var account = await GetAccountFromGdkeyAsync(gdkey); // Pull from cache first
            return account?.LastLoginTime!;
        }

        // Gets all corresponding GDKeys from under a specified UDKey.
        public static async Task<List<string>> GetGdkeysFromUdkeyAsync(string udkey)
        {
            var gdkeys = await GetDeviceGdkeysAsync(udkey);

            if (gdkeys != null)
            {
                foreach (var gdkey in gdkeys)
                {
                    var account = await GetAccountFromGdkeyAsync(gdkey);

                    if (string.IsNullOrEmpty(account.Udkey))
                    {
                        var accountLock = GetAccountLock(gdkey);
                        await accountLock.WaitAsync();
                        try
                        {
                            account.Udkey = udkey;
                            account.IsDirty = true;
                        }
                        finally
                        {
                            accountLock.Release();
                        }
                    }
                }
            }

            return gdkeys ?? new List<string>();
        }

        // Add a gdkey association to a udkey
        public static async Task AddAccountToDevice(string udkey, string gdkey)
        {
            var deviceLock = GetDeviceLock(udkey);
            await deviceLock.WaitAsync();
            try
            {
                await _store!.AddGdkeyToDeviceAsync(udkey, gdkey);
            }
            finally
            {
                deviceLock.Release();
            }
        }

        // Gdkeys registered to a device (empty if the device doesn't exist).
        public static async Task<List<string>> GetDeviceGdkeysPublicAsync(string udkey)
        {
            return await GetDeviceGdkeysAsync(udkey) ?? new List<string>();
        }

        // ---- Admin helpers -------------------------------------------------

        public class AccountSummary
        {
            public string Gdkey { get; set; } = "";
            public string? CharacterId { get; set; }
            public string? UserId { get; set; }
            public string? PlayerName { get; set; }
            public string? LastLoginTime { get; set; }
            public bool Online { get; set; }
        }

        // Searches accounts by friend code, user id, gdkey or player name.
        public static async Task<List<AccountSummary>> SearchAccountsAsync(string query, int limit)
        {
            limit = Math.Clamp(limit, 1, 200);
            var q = (query ?? "").Trim();
            var list = await _store!.SearchAccountsAsync(q, limit);
            foreach (var a in list) a.Online = _accountCache.ContainsKey(a.Gdkey);
            // Names in the cache may be newer than the database copy.
            foreach (var a in list)
            {
                if (_accountCache.TryGetValue(a.Gdkey, out var cached) && cached.YwpUserTables != null &&
                    cached.YwpUserTables.TryGetValue("ywp_user_data", out var ud) && ud != null)
                {
                    try { a.PlayerName = JToken.FromObject(ud)["playerName"]?.ToString() ?? a.PlayerName; } catch { }
                }
            }
            return list;
        }

        public static async Task<Dictionary<string, long>> GetStatsAsync()
        {
            var stats = new Dictionary<string, long>();
            var (accounts, devices) = await _store!.CountsAsync();
            stats["accounts"] = accounts;
            stats["devices"] = devices;
            stats["cachedAccounts"] = _accountCache.Count;
            stats["dirtyAccounts"] = _accountCache.Values.Count(x => x.IsDirty);
            return stats;
        }

        // Every gdkey in the database (used for server-wide gifts).
        public static async Task<List<string>> GetAllGdkeysAsync()
        {
            return await _store!.AllGdkeysAsync();
        }

        // Forces every changed account to be written to the database now.
        public static async Task FlushNowAsync() => await FlushAllDirtyAccountsAsync();

        // Writes the account back to the database immediately
        public static async Task UpdateAccountAsync(Account account)
        {
            var accountLock = GetAccountLock(account.Gdkey!);
            await accountLock.WaitAsync();
            try
            {
                await UpdateAccountNoLockAsync(account);
            }
            finally
            {
                accountLock.Release();
            }
        }

        private static async Task UpdateAccountNoLockAsync(Account account) => await _store!.UpdateAccountAsync(account);

        // Returns null if the device doesn't exist.
        private static async Task<List<string>?> GetDeviceGdkeysAsync(string udkey) => await _store!.DeviceGdkeysAsync(udkey);
    }
}
