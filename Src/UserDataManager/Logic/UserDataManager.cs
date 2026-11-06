using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
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

        private static NpgsqlDataSource? _dataSource;

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

        // connect to postgres
        public static void Initialize()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DataManager.Logic.DataManager.PostgresConnectionString))
                    throw new InvalidOperationException("'PostgresConnectionString' is missing from appsettings.json");
                _dataSource = NpgsqlDataSource.Create(DataManager.Logic.DataManager.PostgresConnectionString!);
                EnsureSchema();

                _flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
                _flushTask = FlushLoopAsync(_cts.Token);
                Console.WriteLine("db service started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't connect to PostgreSQL: {ex.Message}");
                Console.WriteLine("Check 'PostgresConnectionString' in appsettings.json and that the database server is running.");
                Environment.Exit(1);
            }
        }

        // Creates the tables when they don't exist yet, so a fresh database works without running
        // schema.sql by hand. Existing databases are left untouched.
        private static void EnsureSchema()
        {
            using var cmd = _dataSource!.CreateCommand(@"
create table if not exists public.account (
  gdkey text primary key,
  ywp_user_tables jsonb,
  last_lgn_time text,
  opening_tutorial_flag boolean,
  start_date text,
  character_id text unique,
  user_id text unique,
  udkey text
);
create table if not exists public.device (
  udkey text primary key,
  gdkeys text[] not null
);
create table if not exists public.mail (
  mail text primary key,
  ""currentUdkey"" text
);");
            cmd.ExecuteNonQuery();
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

                await using var cmd = _dataSource!.CreateCommand(
                    "SELECT gdkey, character_id, udkey, user_id, ywp_user_tables, last_lgn_time, start_date, opening_tutorial_flag FROM account WHERE gdkey = @gdkey");
                cmd.Parameters.AddWithValue("gdkey", gdkey);
                await using var reader = await cmd.ExecuteReaderAsync();
                Account? account = null;
                if (await reader.ReadAsync())
                {
                    account = ReadAccount(reader);
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
            await using var cmd = _dataSource!.CreateCommand(
                "INSERT INTO device (udkey, gdkeys) VALUES (@udkey, @gdkeys) RETURNING udkey");
            cmd.Parameters.AddWithValue("udkey", udkey);
            cmd.Parameters.AddWithValue("gdkeys", new List<string>());
            var newUdkey = (string)(await cmd.ExecuteScalarAsync())!;
            return newUdkey;
        }

        // checks if udkey exists
        public static async Task<bool> IsDeviceExists(string udkey)
        {
            await using var cmd = _dataSource!.CreateCommand("SELECT COUNT(*) FROM device WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("udkey", udkey);
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            return count > 0;
        }

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
                await using var cmd = _dataSource!.CreateCommand(
                    "INSERT INTO account (gdkey, character_id, udkey, user_id, ywp_user_tables, last_lgn_time, start_date, opening_tutorial_flag) " +
                    "VALUES (@gdkey, @character_id, @udkey, @user_id, @ywp_user_tables, @last_lgn_time, @start_date, @opening_tutorial_flag)");
                AddAccountParameters(cmd, acc);
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                    break;
                }
                // friend code / user id are unique and the user id is a 32-bit hash: on a clash draw again
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && attempt < 8) { }
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
                await using var cmd = _dataSource!.CreateCommand("DELETE FROM account WHERE gdkey = @gdkey");
                cmd.Parameters.AddWithValue("gdkey", gdkey);
                await cmd.ExecuteNonQueryAsync();

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
                await using var cmd = _dataSource!.CreateCommand(
                    "UPDATE device SET gdkeys = array_remove(gdkeys, @gdkey) WHERE udkey = @udkey");
                cmd.Parameters.AddWithValue("gdkey", gdkey);
                cmd.Parameters.AddWithValue("udkey", udkey);
                await cmd.ExecuteNonQueryAsync();
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
            await using var cmd = _dataSource!.CreateCommand("SELECT gdkey FROM account WHERE character_id = @character_id");
            cmd.Parameters.AddWithValue("character_id", charId);
            var gdkey = await cmd.ExecuteScalarAsync() as string;
            return gdkey ?? string.Empty;
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
                    // Both updates happen in one transaction so a crash can't leave the transfer half-applied
                    await using var conn = await _dataSource!.OpenConnectionAsync();
                    await using var tx = await conn.BeginTransactionAsync();

                    var fromGdkeys = await GetDeviceGdkeysAsync(udkeyFrom, conn, tx);
                    var toGdkeys = await GetDeviceGdkeysAsync(udkeyTo, conn, tx);

                    if (fromGdkeys == null)
                        throw new Exception($"Device {udkeyFrom} not found");

                    if (toGdkeys == null)
                        throw new Exception($"Device {udkeyTo} not found");

                    var newToGdkeys = new List<string>();

                    foreach (var gdkey in fromGdkeys)
                    {
                        if (!newToGdkeys.Contains(gdkey))
                            newToGdkeys.Add(gdkey);
                    }

                    await SetDeviceGdkeysAsync(udkeyTo, newToGdkeys, conn, tx);
                    await SetDeviceGdkeysAsync(udkeyFrom, new List<string>(), conn, tx);

                    await tx.CommitAsync();
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
        public static async Task<Mail> GetDataByMail(string email)
        {
            await using var cmd = _dataSource!.CreateCommand("SELECT mail, \"currentUdkey\" FROM mail WHERE mail = @mail");
            cmd.Parameters.AddWithValue("mail", email);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Mail
                {
                    MailAddress = reader.GetString(0),
                    CurrentUdkey = reader.IsDBNull(1) ? null : reader.GetString(1)
                };
            }
            return null!;
        }
        public static async Task AddOrEditEmail(string email, string udkey)
        {
            await using var cmd = _dataSource!.CreateCommand(
                "INSERT INTO mail (mail, \"currentUdkey\") VALUES (@mail, @udkey) " +
                "ON CONFLICT (mail) DO UPDATE SET \"currentUdkey\" = EXCLUDED.\"currentUdkey\"");
            cmd.Parameters.AddWithValue("mail", email);
            cmd.Parameters.AddWithValue("udkey", udkey);
            await cmd.ExecuteNonQueryAsync();
        }
        public static async Task<string> GetGdkeyFromUserId(string userId)
        {
            await using var cmd = _dataSource!.CreateCommand("SELECT gdkey FROM account WHERE user_id = @user_id");
            cmd.Parameters.AddWithValue("user_id", userId);
            var gdkey = await cmd.ExecuteScalarAsync() as string;
            return gdkey ?? string.Empty;
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
                await using var cmd = _dataSource!.CreateCommand(
                    "UPDATE device SET gdkeys = array_append(gdkeys, @gdkey) WHERE udkey = @udkey");
                cmd.Parameters.AddWithValue("gdkey", gdkey);
                cmd.Parameters.AddWithValue("udkey", udkey);
                await cmd.ExecuteNonQueryAsync();
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
            await using var cmd = _dataSource!.CreateCommand(
                "SELECT gdkey, character_id, user_id, last_lgn_time, ywp_user_tables->'ywp_user_data'->>'playerName' FROM account " +
                "WHERE @q = '' OR gdkey = @q OR character_id ILIKE @like OR user_id = @q OR (ywp_user_tables->'ywp_user_data'->>'playerName') ILIKE @like " +
                "ORDER BY last_lgn_time DESC NULLS LAST LIMIT @limit");
            var q = (query ?? "").Trim();
            cmd.Parameters.AddWithValue("q", q);
            cmd.Parameters.AddWithValue("like", "%" + q.Replace("%", "\\%").Replace("_", "\\_") + "%");
            cmd.Parameters.AddWithValue("limit", limit);
            var list = new List<AccountSummary>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AccountSummary
                {
                    Gdkey = reader.GetString(0),
                    CharacterId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    LastLoginTime = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PlayerName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Online = _accountCache.ContainsKey(reader.GetString(0))
                });
            }
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
            await using (var cmd = _dataSource!.CreateCommand("SELECT COUNT(*) FROM account"))
                stats["accounts"] = (long)(await cmd.ExecuteScalarAsync())!;
            await using (var cmd = _dataSource!.CreateCommand("SELECT COUNT(*) FROM device"))
                stats["devices"] = (long)(await cmd.ExecuteScalarAsync())!;
            stats["cachedAccounts"] = _accountCache.Count;
            stats["dirtyAccounts"] = _accountCache.Values.Count(x => x.IsDirty);
            return stats;
        }

        // Every gdkey in the database (used for server-wide gifts).
        public static async Task<List<string>> GetAllGdkeysAsync()
        {
            var list = new List<string>();
            await using var cmd = _dataSource!.CreateCommand("SELECT gdkey FROM account");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(reader.GetString(0));
            return list;
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

        private static async Task UpdateAccountNoLockAsync(Account account)
        {
            await using var cmd = _dataSource!.CreateCommand(
                "UPDATE account SET character_id = @character_id, udkey = @udkey, user_id = @user_id, ywp_user_tables = @ywp_user_tables, " +
                "last_lgn_time = @last_lgn_time, start_date = @start_date, opening_tutorial_flag = @opening_tutorial_flag WHERE gdkey = @gdkey");
            AddAccountParameters(cmd, account);
            await cmd.ExecuteNonQueryAsync();
        }

        private static void AddAccountParameters(NpgsqlCommand cmd, Account account)
        {
            cmd.Parameters.AddWithValue("gdkey", account.Gdkey!);
            cmd.Parameters.AddWithValue("character_id", (object?)account.CharacterId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("udkey", (object?)account.Udkey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("user_id", (object?)account.UserId ?? DBNull.Value);
            cmd.Parameters.Add(new NpgsqlParameter("ywp_user_tables", NpgsqlDbType.Jsonb)
            {
                Value = account.YwpUserTables != null ? JsonConvert.SerializeObject(account.YwpUserTables) : DBNull.Value
            });
            cmd.Parameters.AddWithValue("last_lgn_time", (object?)account.LastLoginTime ?? DBNull.Value);
            // start_date is a text column in the schema
            cmd.Parameters.AddWithValue("start_date", account.StartDate.ToString());
            cmd.Parameters.AddWithValue("opening_tutorial_flag", account.OpeningTutorialFlag);
        }

        private static Account ReadAccount(NpgsqlDataReader reader)
        {
            var tablesJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            var startDateText = reader.IsDBNull(6) ? null : reader.GetString(6);
            return new Account
            {
                Gdkey = reader.GetString(0),
                CharacterId = reader.IsDBNull(1) ? null : reader.GetString(1),
                Udkey = reader.IsDBNull(2) ? null : reader.GetString(2),
                UserId = reader.IsDBNull(3) ? null : reader.GetString(3),
                YwpUserTables = tablesJson != null ? JsonConvert.DeserializeObject<Dictionary<string, object?>>(tablesJson) : null,
                LastLoginTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                StartDate = long.TryParse(startDateText, out var startDate) ? startDate : 0,
                OpeningTutorialFlag = !reader.IsDBNull(7) && reader.GetBoolean(7)
            };
        }

        // Returns null if the device doesn't exist. Pass conn/tx to run inside an existing transaction.
        private static async Task<List<string>?> GetDeviceGdkeysAsync(string udkey, NpgsqlConnection? conn = null, NpgsqlTransaction? tx = null)
        {
            await using var cmd = conn != null
                ? new NpgsqlCommand("SELECT gdkeys FROM device WHERE udkey = @udkey", conn, tx)
                : _dataSource!.CreateCommand("SELECT gdkeys FROM device WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("udkey", udkey);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader.GetFieldValue<List<string>>(0);
            }
            return null;
        }

        private static async Task SetDeviceGdkeysAsync(string udkey, List<string> gdkeys, NpgsqlConnection? conn = null, NpgsqlTransaction? tx = null)
        {
            await using var cmd = conn != null
                ? new NpgsqlCommand("UPDATE device SET gdkeys = @gdkeys WHERE udkey = @udkey", conn, tx)
                : _dataSource!.CreateCommand("UPDATE device SET gdkeys = @gdkeys WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("gdkeys", gdkeys);
            cmd.Parameters.AddWithValue("udkey", udkey);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
