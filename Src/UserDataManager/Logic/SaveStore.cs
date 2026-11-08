using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using Puniemu.Src.UserDataManager.DataClasses;
using System.Data.Common;

namespace Puniemu.Src.UserDataManager.Logic
{
    // Where saves live. "Database" in appsettings.json:
    //   "sqlite"   - one file next to the server ("SqlitePath", default saves.db). Nothing to install.
    //   "postgres" - a PostgreSQL server ("PostgresConnectionString"); better for many players.
    // UserDataManager keeps its cache, locks and background saving on top of either.
    public interface ISaveStore
    {
        string Name { get; }
        void EnsureSchema();
        Task<Account?> LoadAccountAsync(string gdkey);
        Task InsertAccountAsync(Account account);   // throws DuplicateSaveKeyException on a friend code / user id clash
        Task UpdateAccountAsync(Account account);
        Task DeleteAccountAsync(string gdkey);
        Task<string> GdkeyByCharacterIdAsync(string characterId);
        Task<string> GdkeyByUserIdAsync(string userId);
        Task<List<string>> AllGdkeysAsync();
        Task<List<UserDataManager.AccountSummary>> SearchAccountsAsync(string query, int limit);
        Task<(long accounts, long devices)> CountsAsync();

        Task<string> NewDeviceAsync(string udkey);
        Task<bool> DeviceExistsAsync(string udkey);
        Task<List<string>?> DeviceGdkeysAsync(string udkey);
        Task AddGdkeyToDeviceAsync(string udkey, string gdkey);
        Task RemoveGdkeyFromDeviceAsync(string udkey, string gdkey);
        // moves every save of one device to another in one transaction
        Task TransferGdkeysAsync(string udkeyFrom, string udkeyTo);

        Task<Mail?> GetMailAsync(string mail);
        Task UpsertMailAsync(string mail, string udkey);
    }

    public class DuplicateSaveKeyException : Exception { }

    // ---- PostgreSQL ----------------------------------------------------------------------------
    public class PostgresSaveStore : ISaveStore
    {
        private readonly NpgsqlDataSource _ds;
        public string Name => "PostgreSQL";
        public PostgresSaveStore(string connectionString) { _ds = NpgsqlDataSource.Create(connectionString); }

        public void EnsureSchema()
        {
            using var cmd = _ds.CreateCommand(@"
create table if not exists public.account (
  gdkey text primary key, ywp_user_tables jsonb, last_lgn_time text, opening_tutorial_flag boolean,
  start_date text, character_id text unique, user_id text unique, udkey text);
create table if not exists public.device (udkey text primary key, gdkeys text[] not null);
create table if not exists public.mail (mail text primary key, ""currentUdkey"" text);");
            cmd.ExecuteNonQuery();
        }

        private const string ACCOUNT_COLS = "gdkey, character_id, udkey, user_id, ywp_user_tables, last_lgn_time, start_date, opening_tutorial_flag";

        public async Task<Account?> LoadAccountAsync(string gdkey)
        {
            await using var cmd = _ds.CreateCommand($"SELECT {ACCOUNT_COLS} FROM account WHERE gdkey = @gdkey");
            cmd.Parameters.AddWithValue("gdkey", gdkey);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? SaveStoreUtil.ReadAccount(r) : null;
        }

        private static void AddParams(NpgsqlCommand cmd, Account a)
        {
            foreach (var (k, v) in SaveStoreUtil.Params(a))
                if (k == "ywp_user_tables") cmd.Parameters.Add(new NpgsqlParameter(k, NpgsqlDbType.Jsonb) { Value = v });
                else cmd.Parameters.AddWithValue(k, v);
        }

        public async Task InsertAccountAsync(Account a)
        {
            await using var cmd = _ds.CreateCommand($"INSERT INTO account ({ACCOUNT_COLS}) VALUES (@gdkey, @character_id, @udkey, @user_id, @ywp_user_tables, @last_lgn_time, @start_date, @opening_tutorial_flag)");
            AddParams(cmd, a);
            try { await cmd.ExecuteNonQueryAsync(); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation) { throw new DuplicateSaveKeyException(); }
        }

        public async Task UpdateAccountAsync(Account a)
        {
            await using var cmd = _ds.CreateCommand("UPDATE account SET character_id = @character_id, udkey = @udkey, user_id = @user_id, ywp_user_tables = @ywp_user_tables, " +
                "last_lgn_time = @last_lgn_time, start_date = @start_date, opening_tutorial_flag = @opening_tutorial_flag WHERE gdkey = @gdkey");
            AddParams(cmd, a);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAccountAsync(string gdkey)
        {
            await using var cmd = _ds.CreateCommand("DELETE FROM account WHERE gdkey = @gdkey");
            cmd.Parameters.AddWithValue("gdkey", gdkey);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<string> Scalar(string sql, string name, string value)
        {
            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue(name, value);
            return await cmd.ExecuteScalarAsync() as string ?? "";
        }

        public Task<string> GdkeyByCharacterIdAsync(string id) => Scalar("SELECT gdkey FROM account WHERE character_id = @v", "v", id);
        public Task<string> GdkeyByUserIdAsync(string id) => Scalar("SELECT gdkey FROM account WHERE user_id = @v", "v", id);

        public async Task<List<string>> AllGdkeysAsync()
        {
            var list = new List<string>();
            await using var cmd = _ds.CreateCommand("SELECT gdkey FROM account");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(r.GetString(0));
            return list;
        }

        public async Task<List<UserDataManager.AccountSummary>> SearchAccountsAsync(string q, int limit)
        {
            await using var cmd = _ds.CreateCommand(
                "SELECT gdkey, character_id, user_id, last_lgn_time, ywp_user_tables->'ywp_user_data'->>'playerName' FROM account " +
                "WHERE @q = '' OR gdkey = @q OR character_id ILIKE @like OR user_id = @q OR (ywp_user_tables->'ywp_user_data'->>'playerName') ILIKE @like " +
                "ORDER BY last_lgn_time DESC NULLS LAST LIMIT @limit");
            cmd.Parameters.AddWithValue("q", q);
            cmd.Parameters.AddWithValue("like", SaveStoreUtil.Like(q));
            cmd.Parameters.AddWithValue("limit", limit);
            await using var r = await cmd.ExecuteReaderAsync();
            return await SaveStoreUtil.ReadSummaries(r);
        }

        public async Task<(long, long)> CountsAsync()
        {
            await using var a = _ds.CreateCommand("SELECT COUNT(*) FROM account");
            await using var d = _ds.CreateCommand("SELECT COUNT(*) FROM device");
            return ((long)(await a.ExecuteScalarAsync())!, (long)(await d.ExecuteScalarAsync())!);
        }

        public async Task<string> NewDeviceAsync(string udkey)
        {
            await using var cmd = _ds.CreateCommand("INSERT INTO device (udkey, gdkeys) VALUES (@udkey, @gdkeys) RETURNING udkey");
            cmd.Parameters.AddWithValue("udkey", udkey);
            cmd.Parameters.AddWithValue("gdkeys", new List<string>());
            return (string)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task<bool> DeviceExistsAsync(string udkey)
        {
            await using var cmd = _ds.CreateCommand("SELECT COUNT(*) FROM device WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("udkey", udkey);
            return (long)(await cmd.ExecuteScalarAsync())! > 0;
        }

        private async Task<List<string>?> Gdkeys(string udkey, NpgsqlConnection? conn = null, NpgsqlTransaction? tx = null)
        {
            await using var cmd = conn != null ? new NpgsqlCommand("SELECT gdkeys FROM device WHERE udkey = @udkey", conn, tx)
                                               : _ds.CreateCommand("SELECT gdkeys FROM device WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("udkey", udkey);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? r.GetFieldValue<List<string>>(0) : null;
        }

        private async Task SetGdkeys(string udkey, List<string> gdkeys, NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            await using var cmd = new NpgsqlCommand("UPDATE device SET gdkeys = @gdkeys WHERE udkey = @udkey", conn, tx);
            cmd.Parameters.AddWithValue("gdkeys", gdkeys);
            cmd.Parameters.AddWithValue("udkey", udkey);
            await cmd.ExecuteNonQueryAsync();
        }

        public Task<List<string>?> DeviceGdkeysAsync(string udkey) => Gdkeys(udkey);

        public async Task AddGdkeyToDeviceAsync(string udkey, string gdkey)
        {
            await using var cmd = _ds.CreateCommand("UPDATE device SET gdkeys = array_append(gdkeys, @gdkey) WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("gdkey", gdkey);
            cmd.Parameters.AddWithValue("udkey", udkey);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RemoveGdkeyFromDeviceAsync(string udkey, string gdkey)
        {
            await using var cmd = _ds.CreateCommand("UPDATE device SET gdkeys = array_remove(gdkeys, @gdkey) WHERE udkey = @udkey");
            cmd.Parameters.AddWithValue("gdkey", gdkey);
            cmd.Parameters.AddWithValue("udkey", udkey);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task TransferGdkeysAsync(string from, string to)
        {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            var fromKeys = await Gdkeys(from, conn, tx) ?? throw new Exception($"Device {from} not found");
            _ = await Gdkeys(to, conn, tx) ?? throw new Exception($"Device {to} not found");
            await SetGdkeys(to, fromKeys.Distinct().ToList(), conn, tx);
            await SetGdkeys(from, new List<string>(), conn, tx);
            await tx.CommitAsync();
        }

        public async Task<Mail?> GetMailAsync(string mail)
        {
            await using var cmd = _ds.CreateCommand("SELECT mail, \"currentUdkey\" FROM mail WHERE mail = @mail");
            cmd.Parameters.AddWithValue("mail", mail);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? new Mail { MailAddress = r.GetString(0), CurrentUdkey = r.IsDBNull(1) ? null : r.GetString(1) } : null;
        }

        public async Task UpsertMailAsync(string mail, string udkey)
        {
            await using var cmd = _ds.CreateCommand("INSERT INTO mail (mail, \"currentUdkey\") VALUES (@mail, @udkey) ON CONFLICT (mail) DO UPDATE SET \"currentUdkey\" = EXCLUDED.\"currentUdkey\"");
            cmd.Parameters.AddWithValue("mail", mail);
            cmd.Parameters.AddWithValue("udkey", udkey);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ---- SQLite (one file, nothing to install) ---------------------------------------------------
    public class SqliteSaveStore : ISaveStore
    {
        private readonly string _cs;
        // SQLite allows one writer at a time; writes queue here instead of failing with "database is locked"
        private readonly SemaphoreSlim _write = new(1, 1);
        public string Name { get; }

        public SqliteSaveStore(string path)
        {
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            _cs = new SqliteConnectionStringBuilder { DataSource = full, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared, DefaultTimeout = 30 }.ToString();
            Name = "SQLite (" + full + ")";
        }

        private async Task<SqliteConnection> Open()
        {
            var c = new SqliteConnection(_cs);
            await c.OpenAsync();
            await using var p = c.CreateCommand();
            p.CommandText = "PRAGMA busy_timeout=30000; PRAGMA foreign_keys=ON;";
            await p.ExecuteNonQueryAsync();
            return c;
        }

        private static SqliteCommand Cmd(SqliteConnection c, string sql, params (string, object?)[] ps)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (k, v) in ps) cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            return cmd;
        }

        private async Task<T> Write<T>(Func<SqliteConnection, Task<T>> f)
        {
            await _write.WaitAsync();
            try { await using var c = await Open(); return await f(c); }
            finally { _write.Release(); }
        }

        private async Task<T> Read<T>(Func<SqliteConnection, Task<T>> f)
        {
            await using var c = await Open();
            return await f(c);
        }

        public void EnsureSchema()
        {
            using var c = new SqliteConnection(_cs);
            c.Open();
            using var cmd = c.CreateCommand();
            // WAL: readers never wait for the writer; synchronous=NORMAL is safe with WAL
            cmd.CommandText = @"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;
create table if not exists account (
  gdkey text primary key, ywp_user_tables text, last_lgn_time text, opening_tutorial_flag integer,
  start_date text, character_id text unique, user_id text unique, udkey text);
create table if not exists device (udkey text primary key, gdkeys text not null default '[]');
create table if not exists mail (mail text primary key, currentUdkey text);";
            cmd.ExecuteNonQuery();
        }

        private const string ACCOUNT_COLS = "gdkey, character_id, udkey, user_id, ywp_user_tables, last_lgn_time, start_date, opening_tutorial_flag";

        private static (string, object?)[] AccountParams(Account a) =>
            SaveStoreUtil.Params(a).Select(p => (p.Key, p.Key == "opening_tutorial_flag" ? (object)((bool)p.Value ? 1 : 0) : p.Value)).ToArray();

        public Task<Account?> LoadAccountAsync(string gdkey) => Read(async c =>
        {
            await using var cmd = Cmd(c, $"SELECT {ACCOUNT_COLS} FROM account WHERE gdkey = @gdkey", ("gdkey", gdkey));
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? SaveStoreUtil.ReadAccount(r) : null;
        });

        public Task InsertAccountAsync(Account a) => Write(async c =>
        {
            await using var cmd = Cmd(c, $"INSERT INTO account ({ACCOUNT_COLS}) VALUES (@gdkey, @character_id, @udkey, @user_id, @ywp_user_tables, @last_lgn_time, @start_date, @opening_tutorial_flag)", AccountParams(a));
            try { await cmd.ExecuteNonQueryAsync(); }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { throw new DuplicateSaveKeyException(); } // SQLITE_CONSTRAINT
            return 0;
        });

        public Task UpdateAccountAsync(Account a) => Write(async c =>
        {
            await using var cmd = Cmd(c, "UPDATE account SET character_id = @character_id, udkey = @udkey, user_id = @user_id, ywp_user_tables = @ywp_user_tables, " +
                "last_lgn_time = @last_lgn_time, start_date = @start_date, opening_tutorial_flag = @opening_tutorial_flag WHERE gdkey = @gdkey", AccountParams(a));
            return await cmd.ExecuteNonQueryAsync();
        });

        public Task DeleteAccountAsync(string gdkey) => Write(async c =>
        {
            await using var cmd = Cmd(c, "DELETE FROM account WHERE gdkey = @g", ("g", gdkey));
            return await cmd.ExecuteNonQueryAsync();
        });

        private Task<string> Scalar(string sql, string value) => Read(async c =>
        {
            await using var cmd = Cmd(c, sql, ("v", value));
            return await cmd.ExecuteScalarAsync() as string ?? "";
        });

        public Task<string> GdkeyByCharacterIdAsync(string id) => Scalar("SELECT gdkey FROM account WHERE character_id = @v", id);
        public Task<string> GdkeyByUserIdAsync(string id) => Scalar("SELECT gdkey FROM account WHERE user_id = @v", id);

        public Task<List<string>> AllGdkeysAsync() => Read(async c =>
        {
            var list = new List<string>();
            await using var cmd = Cmd(c, "SELECT gdkey FROM account");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(r.GetString(0));
            return list;
        });

        public Task<List<UserDataManager.AccountSummary>> SearchAccountsAsync(string q, int limit) => Read(async c =>
        {
            // LIKE is case-insensitive for ASCII in SQLite
            await using var cmd = Cmd(c,
                "SELECT gdkey, character_id, user_id, last_lgn_time, json_extract(ywp_user_tables, '$.ywp_user_data.playerName') FROM account " +
                "WHERE @q = '' OR gdkey = @q OR character_id LIKE @like ESCAPE '\\' OR user_id = @q OR json_extract(ywp_user_tables, '$.ywp_user_data.playerName') LIKE @like ESCAPE '\\' " +
                "ORDER BY last_lgn_time IS NULL, last_lgn_time DESC LIMIT @limit", ("q", q), ("like", SaveStoreUtil.Like(q)), ("limit", limit));
            await using var r = await cmd.ExecuteReaderAsync();
            return await SaveStoreUtil.ReadSummaries(r);
        });

        public Task<(long accounts, long devices)> CountsAsync() => Read(async c =>
        {
            await using var a = Cmd(c, "SELECT COUNT(*) FROM account");
            await using var d = Cmd(c, "SELECT COUNT(*) FROM device");
            return ((long)(await a.ExecuteScalarAsync())!, (long)(await d.ExecuteScalarAsync())!);
        });

        public Task<string> NewDeviceAsync(string udkey) => Write(async c =>
        {
            await using var cmd = Cmd(c, "INSERT INTO device (udkey, gdkeys) VALUES (@u, '[]')", ("u", udkey));
            await cmd.ExecuteNonQueryAsync();
            return udkey;
        });

        public Task<bool> DeviceExistsAsync(string udkey) => Read(async c =>
        {
            await using var cmd = Cmd(c, "SELECT COUNT(*) FROM device WHERE udkey = @u", ("u", udkey));
            return (long)(await cmd.ExecuteScalarAsync())! > 0;
        });

        private static async Task<List<string>?> Gdkeys(SqliteConnection c, string udkey, SqliteTransaction? tx = null)
        {
            await using var cmd = Cmd(c, "SELECT gdkeys FROM device WHERE udkey = @u", ("u", udkey));
            cmd.Transaction = tx;
            var raw = await cmd.ExecuteScalarAsync() as string;
            return raw == null ? null : JsonConvert.DeserializeObject<List<string>>(raw) ?? new();
        }

        private static async Task SetGdkeys(SqliteConnection c, string udkey, List<string> keys, SqliteTransaction? tx = null)
        {
            await using var cmd = Cmd(c, "UPDATE device SET gdkeys = @k WHERE udkey = @u", ("k", JsonConvert.SerializeObject(keys)), ("u", udkey));
            cmd.Transaction = tx;
            await cmd.ExecuteNonQueryAsync();
        }

        public Task<List<string>?> DeviceGdkeysAsync(string udkey) => Read(c => Gdkeys(c, udkey));

        // read-modify-write under the write lock (no array type in SQLite: the list is JSON text)
        public Task AddGdkeyToDeviceAsync(string udkey, string gdkey) => Write(async c =>
        {
            var keys = await Gdkeys(c, udkey);
            if (keys == null) return 0;
            keys.Add(gdkey);
            await SetGdkeys(c, udkey, keys);
            return 0;
        });

        public Task RemoveGdkeyFromDeviceAsync(string udkey, string gdkey) => Write(async c =>
        {
            var keys = await Gdkeys(c, udkey);
            if (keys == null) return 0;
            keys.RemoveAll(k => k == gdkey);
            await SetGdkeys(c, udkey, keys);
            return 0;
        });

        public Task TransferGdkeysAsync(string from, string to) => Write(async c =>
        {
            await using var tx = (SqliteTransaction)await c.BeginTransactionAsync();
            var fromKeys = await Gdkeys(c, from, tx) ?? throw new Exception($"Device {from} not found");
            _ = await Gdkeys(c, to, tx) ?? throw new Exception($"Device {to} not found");
            await SetGdkeys(c, to, fromKeys.Distinct().ToList(), tx);
            await SetGdkeys(c, from, new List<string>(), tx);
            await tx.CommitAsync();
            return 0;
        });

        public Task<Mail?> GetMailAsync(string mail) => Read(async c =>
        {
            await using var cmd = Cmd(c, "SELECT mail, currentUdkey FROM mail WHERE mail = @m", ("m", mail));
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? new Mail { MailAddress = r.GetString(0), CurrentUdkey = r.IsDBNull(1) ? null : r.GetString(1) } : null;
        });

        public Task UpsertMailAsync(string mail, string udkey) => Write(async c =>
        {
            await using var cmd = Cmd(c, "INSERT INTO mail (mail, currentUdkey) VALUES (@m, @u) ON CONFLICT(mail) DO UPDATE SET currentUdkey = excluded.currentUdkey", ("m", mail), ("u", udkey));
            return await cmd.ExecuteNonQueryAsync();
        });
    }

    internal static class SaveStoreUtil
    {
        public static Dictionary<string, object> Params(Account a) => new()
        {
            ["gdkey"] = a.Gdkey!,
            ["character_id"] = (object?)a.CharacterId ?? DBNull.Value,
            ["udkey"] = (object?)a.Udkey ?? DBNull.Value,
            ["user_id"] = (object?)a.UserId ?? DBNull.Value,
            ["ywp_user_tables"] = a.YwpUserTables != null ? JsonConvert.SerializeObject(a.YwpUserTables) : DBNull.Value,
            ["last_lgn_time"] = (object?)a.LastLoginTime ?? DBNull.Value,
            ["start_date"] = a.StartDate.ToString(),            // a text column in both schemas
            ["opening_tutorial_flag"] = a.OpeningTutorialFlag,
        };

        public static string Like(string q) => "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        public static Account ReadAccount(DbDataReader r)
        {
            var tables = r.IsDBNull(4) ? null : r.GetString(4);
            var start = r.IsDBNull(6) ? null : r.GetString(6);
            return new Account
            {
                Gdkey = r.GetString(0),
                CharacterId = r.IsDBNull(1) ? null : r.GetString(1),
                Udkey = r.IsDBNull(2) ? null : r.GetString(2),
                UserId = r.IsDBNull(3) ? null : r.GetString(3),
                YwpUserTables = tables != null ? JsonConvert.DeserializeObject<Dictionary<string, object?>>(tables) : null,
                LastLoginTime = r.IsDBNull(5) ? null : r.GetString(5),
                StartDate = long.TryParse(start, out var s) ? s : 0,
                OpeningTutorialFlag = !r.IsDBNull(7) && Convert.ToBoolean(r.GetValue(7)),
            };
        }

        public static async Task<List<UserDataManager.AccountSummary>> ReadSummaries(DbDataReader r)
        {
            var list = new List<UserDataManager.AccountSummary>();
            while (await r.ReadAsync())
                list.Add(new UserDataManager.AccountSummary
                {
                    Gdkey = r.GetString(0),
                    CharacterId = r.IsDBNull(1) ? null : r.GetString(1),
                    UserId = r.IsDBNull(2) ? null : r.GetString(2),
                    LastLoginTime = r.IsDBNull(3) ? null : r.GetString(3),
                    PlayerName = r.IsDBNull(4) ? null : r.GetString(4),
                });
            return list;
        }
    }
}
