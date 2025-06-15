using ChatBot.Database.Interfaces;
using ChatBot.Utils.Databasse;
using DuckDB.NET.Data;

namespace ChatBot.Database.History
{
    internal class HistoryDuckDB : IHistoryDatabase
    {
        private readonly ReaderWriterLockSlim _dbLock = new(LockRecursionPolicy.SupportsRecursion);
        private readonly string _connectionString;
        public string BuildConnString(DBAccessMode mode)
        {
            var connString = _connectionString;

            if (mode == DBAccessMode.ReadOnly)
                connString += ";ACCESS_MODE=READ_ONLY";

            return connString;
        }
        public HistoryDuckDB(string path)
        {
            var dbDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dbDirectory))
            {
                if (!Directory.Exists(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory);
                }
            }
            _connectionString = @"Data Source=" + path;

        }

        #region 開關和dispose
        private void Close()
        {

        }

        public void Dispose()
        {
            Close();
        }
        private void Close(DuckDBConnection _connection, DBAccessMode mode)
        {
            if (_connection != null)
            {
                try
                {
                    _connection.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"關閉連線時發生錯誤：{ex.Message}");
                }
                finally
                {
                    _connection.Dispose();
                    _connection = null;

                    if (mode == DBAccessMode.ReadWrite)
                        _dbLock.ExitWriteLock();
                    else
                        _dbLock.ExitReadLock();
                }
            }
        }

        #endregion

        #region Init用
        public void CreateHistoryTable()
        {
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                                CREATE TABLE IF NOT EXISTS TB00_SessionHistory(
                                    InsertTime TIMESTAMP,
                                    Query TEXT,
                                    Respond TEXT,
                                    Award INTEGER
                                )
                            ";
                cmd.ExecuteNonQuery();
            }
            Close(_connection, DBAccessMode.ReadWrite);
        }
        #endregion

        #region SQL
        public IEnumerable<T> Query<T>(DBAccessMode mode, string sql) where T : new()
        {
            var results = new List<T>();
            if (mode == DBAccessMode.ReadOnly)
            {
                _dbLock.EnterReadLock();
            }
            else
            {
                _dbLock.EnterWriteLock();
            }
            DuckDBConnection _connection = new DuckDBConnection(BuildConnString(mode));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader())
                {
                    var props = typeof(T).GetProperties();
                    while (reader.Read())
                    {
                        var item = new T();
                        foreach (var prop in props)
                        {
                            if (!reader.HasColumn(prop.Name) || reader[prop.Name] is DBNull)
                                continue;

                            prop.SetValue(item, Convert.ChangeType(reader[prop.Name], prop.PropertyType));
                        }
                        results.Add(item);
                    }
                }
            }
            Close(_connection, mode);
            return results;
        }

        public int ExecuteNonQuery(DBAccessMode mode, string sql)
        {
            int affect = 0;
            if (mode == DBAccessMode.ReadOnly)
            {
                _dbLock.EnterReadLock();
            }
            else
            {
                _dbLock.EnterWriteLock();
            }
            DuckDBConnection _connection = new DuckDBConnection(BuildConnString(mode));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = sql;
                affect = cmd.ExecuteNonQuery();
            }
            Close(_connection, mode);
            return affect;
        }

        public int ExecuteNonQuery(DBAccessMode mode, string sql, (string?, object?)[] param)
        {
            int affect = 0;
            if (mode == DBAccessMode.ReadOnly)
            {
                _dbLock.EnterReadLock();
            }
            else
            {
                _dbLock.EnterWriteLock();
            }
            DuckDBConnection _connection = new DuckDBConnection(BuildConnString(mode));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (var p in param)
                {
                    cmd.Parameters.Add(new DuckDBParameter(null, p.Item2));
                }
                affect = cmd.ExecuteNonQuery();
            }
            Close(_connection, mode);

            return affect;
        }

        private void InsertSessionHistory(DuckDBConnection _connection, string id, string query, string respond, int award)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO TB00_SessionHistory (InsertTime, Query, Respond, Award) 
                            VALUES (?, ?, ?, ?)";
                cmd.Parameters.Add(new DuckDBParameter(null, id));
                cmd.Parameters.Add(new DuckDBParameter(null, query));
                cmd.Parameters.Add(new DuckDBParameter(null, respond));
                cmd.Parameters.Add(new DuckDBParameter(null, award));

                cmd.ExecuteNonQuery();
            }
        }

        public void InsertHistory(string id, string query, string respond, int award)
        {
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            InsertSessionHistory(_connection, id, query, respond, award);
            Close(_connection, DBAccessMode.ReadWrite);
        }

        public int UpdateAward(string id, int newAward)
        {
            int result = 0;
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"UPDATE TB00_SessionHistory SET Award = ? WHERE Id = ?";
                cmd.Parameters.Add(new DuckDBParameter(null, newAward));
                cmd.Parameters.Add(new DuckDBParameter(null, id));
                result = cmd.ExecuteNonQuery();
            }
            Close(_connection, DBAccessMode.ReadWrite);
            return result;
        }

        public IEnumerable<(string Query, string Respond)> getHistory(int max = -1, int award_threshold = 0)
        {
            _dbLock.EnterReadLock();
            var result = new List<(string, string)>();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadOnly));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                if (max == -1)
                {
                    cmd.CommandText = @"
            SELECT Query, Respond
            FROM (
                SELECT Query, Respond, InsertTime
                FROM TB00_SessionHistory
                WHERE Award >= ?
                ORDER BY InsertTime DESC
            ) AS Sub
            ORDER BY InsertTime ASC
        ";
                    cmd.Parameters.Add(new DuckDBParameter { Value = award_threshold });
                }
                else
                {
                    cmd.CommandText = @"
            SELECT Query, Respond
            FROM (
                SELECT Query, Respond, InsertTime
                FROM TB00_SessionHistory
                WHERE Award >= ?
                ORDER BY InsertTime DESC
                LIMIT ?
            ) AS Sub
            ORDER BY InsertTime ASC
        ";
                    cmd.Parameters.Add(new DuckDBParameter { Value = award_threshold });
                    cmd.Parameters.Add(new DuckDBParameter { Value = max });
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add((reader.GetString(0), reader.GetString(1)));
                }
            }
            Close(_connection, DBAccessMode.ReadOnly);
            return result;
        }

        #endregion

    }
}
