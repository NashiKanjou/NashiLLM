/*
 *  This file is part of ArsCore.
 *
 *  ArsCore is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  ArsCore is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with ArsCore.  If not, see <https://www.gnu.org/licenses/>.
 */
using ChatBot.Database.Interfaces;
using ChatBot.Utils.Databasse;
using ChatBot.Utils.Embedding;
using DuckDB.NET.Data;
using System.Data;

namespace ChatBot.Database.RAG
{
    internal class RAGDuckDB : IRAGDatabase
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
        public RAGDuckDB(string path)
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
        //public void Connect(DBAccessMode mode)
        //{
        //    // 避免多個 Connect 同時競爭
        //    if (mode == DBAccessMode.ReadOnly)
        //        _dbLock.EnterReadLock();  // 多人可共用讀
        //    else
        //        _dbLock.EnterWriteLock();  // 僅允許一人寫

        //    try
        //    {
        //        Close(); // 先關掉舊連線

        //        _connection = new DuckDBConnection(BuildConnString(mode));
        //        _connection.Open();
        //    }
        //    catch
        //    {
        //        // 錯誤發生時，釋放鎖避免鎖死
        //        if (mode == DBAccessMode.ReadOnly)
        //            _dbLock.ExitReadLock();
        //        else
        //            _dbLock.ExitWriteLock();
        //        throw;
        //    }
        //}
        //public void Dispose()
        //{
        //    Close();
        //}
        #endregion

        #region Init用
        public void CreateDocumentTable()
        {
            _dbLock.EnterWriteLock();
            var connstr = BuildConnString(DBAccessMode.ReadWrite);
            var _connection = new DuckDBConnection(connstr);
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Documents (
                    InsertTime TEXT,
                    Text TEXT,
                    Embedding BLOB,
                    Source TEXT,
                    Award INTEGER
                )";
                cmd.ExecuteNonQuery();
            }
            Close(_connection, DBAccessMode.ReadWrite);
            RemoveDocument();
        }
        #endregion

        #region 清除舊資料
        private void RemoveDocument()
        {
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT rowid, Source FROM Documents";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                long rowId = reader.GetInt64(0);
                string sourcePath = reader.GetString(1);

                if (!File.Exists(sourcePath))
                {
                    Console.WriteLine($"檔案不存在: {sourcePath}，刪除資料列...");

                    using var deleteCmd = _connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM Documents WHERE rowid = ?";
                    deleteCmd.Parameters.Add(new DuckDBParameter(null, rowId));
                    //deleteCmd.Parameters.AddWithValue("@rowid", rowId);
                    deleteCmd.ExecuteNonQuery();
                }
            }
            Close(_connection, DBAccessMode.ReadWrite);
        }

        public void RemoveDocument(string filename)
        {
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, Source FROM Documents WHERE Source = ?";
                cmd.Parameters.Add(new DuckDBParameter(null, filename));
                using (var reader = cmd.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        long rowId = reader.GetInt64(0);
                        string sourcePath = reader.GetString(1);

                        
                            Console.WriteLine($"刪除向量資料庫中有關{sourcePath}的資料列...");

                            using (var deleteCmd = _connection.CreateCommand())
                            {
                                {
                                    deleteCmd.CommandText = "DELETE FROM Documents WHERE rowid = ?";
                                    deleteCmd.Parameters.Add(new DuckDBParameter(null, rowId));
                                    //deleteCmd.Parameters.AddWithValue("@rowid", rowId);
                                    deleteCmd.ExecuteNonQuery();
                                }
                            }
                        
                    }
                }
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

        public int UpdateAward(string id, int newAward)
        {
            int result = 0;
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"UPDATE Documents SET Award = ? WHERE Source = ?";
                cmd.Parameters.Add(new DuckDBParameter(null, newAward));
                cmd.Parameters.Add(new DuckDBParameter(null, id));
                result = cmd.ExecuteNonQuery();
            }

            Close(_connection, DBAccessMode.ReadWrite);
            return result;
        }

        #endregion

        #region RAG

        public (float[] embedding, string id, string text, double score)[] FindTopMatches(string query, int topN = 5, double score_threshold = 0.6)
        {
            float[] queryEmbedding = TokenizerUtils.GetEmbedding(query);
            var result = new List<(float[] embedding, string id, string text, double score)>();
            var seenKeys = new HashSet<string>();
            _dbLock.EnterReadLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadOnly));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                string selectSql = @"
                SELECT Source AS Id, Text, Embedding, InsertTime, Award
                FROM Documents
                WHERE Award >= 0
                ORDER BY InsertTime DESC";

                cmd.CommandText = selectSql;

                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                string id = reader.GetString(0);
                                string text = !reader.IsDBNull(1) ? reader.GetString(1) : "";

                                using var stream = (Stream)reader[2];
                                using var ms = new MemoryStream();
                                stream.CopyTo(ms);
                                byte[] blob = ms.ToArray();

                                float[] embedding = TokenizerUtils.DeserializeEmbedding(blob);
                                double score = CosineSimilarity(queryEmbedding, embedding);

                                if (score >= score_threshold)
                                {
                                    result.Add((embedding, id, text, score));
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[Row Error] {e}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Query Error] {e}");
                }

            }
            Close(_connection, DBAccessMode.ReadOnly);
            return result
                .OrderByDescending(r => r.score)
                .Take(topN)
                .Select(r => (r.embedding, r.id, r.text, r.score))
                .ToArray();
        }
        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                return -1;
                //throw new InvalidDataException($"向量長度不一致：{vectorA.Length} vs {vectorB.Length}");
            }
            double dot = 0.0;
            double magA = 0.0;
            double magB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dot += vectorA[i] * vectorB[i];
                magA += vectorA[i] * vectorA[i];
                magB += vectorB[i] * vectorB[i];
            }

            magA = Math.Sqrt(magA);
            magB = Math.Sqrt(magB);

            if (magA == 0 || magB == 0)
                return 0;

            return dot / (magA * magB);
        }

        public void InsertDocument(string insertTime, string source, string text, int award)
        {
            var embedding = TokenizerUtils.GetEmbedding(text);
            var embeddingBytes = TokenizerUtils.FloatArrayToBytes(embedding);
            _dbLock.EnterWriteLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadWrite));
            _connection.Open();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
        INSERT INTO Documents (InsertTime, Text, Embedding, Source, Award)
        VALUES (?, ?, ?, ?, ?)";

                cmd.Parameters.Add(new DuckDBParameter(null, insertTime));
                cmd.Parameters.Add(new DuckDBParameter(null, text));
                cmd.Parameters.Add(new DuckDBParameter(null, embeddingBytes));
                cmd.Parameters.Add(new DuckDBParameter(null, source));
                cmd.Parameters.Add(new DuckDBParameter(null, award));

                cmd.ExecuteNonQuery();
            }
            Close(_connection, DBAccessMode.ReadWrite);
        }

        public DateTime? GetLatestInsertTimeBySource(string source)
        {
            _dbLock.EnterReadLock();
            var _connection = new DuckDBConnection(BuildConnString(DBAccessMode.ReadOnly));
            _connection.Open();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
        SELECT InsertTime
        FROM Documents
        WHERE Source = ?
        ORDER BY InsertTime DESC
        LIMIT 1";

            cmd.Parameters.Add(new DuckDBParameter(null, source));

            var result = cmd.ExecuteScalar();
            Close(_connection, DBAccessMode.ReadOnly);
            return result != null ? DateTime.Parse(result.ToString()) : null;
        }

        #endregion
    }

}
