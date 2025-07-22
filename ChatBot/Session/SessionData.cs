using ChatBot.Binding;
using ChatBot.Database.History;
using ChatBot.Database.Interfaces;
using ChatBot.Database.KAG;
using ChatBot.Database.RAG;
using ChatBot.Utils.Embedding;
using ChatBot.Utils.Databasse;
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
namespace ChatBot.Session
{
    class SessionData
    {

        internal IHistoryDatabase sessionHistoryDatabase;
        private IRAGDatabase accountRAGDatabase;

        private IKAGDatabase accountKAGDatabase;
        
        #region 初始化
        public SessionData(string account_DBString, string session_DBString, string account_KAGString)
        {
            #region RAG
            sessionHistoryDatabase = new HistoryDuckDB(session_DBString);
            accountRAGDatabase = new RAGDuckDB(account_DBString);
            #endregion

            #region KAG
            accountKAGDatabase = new KuzuDBClient(account_KAGString);
            #endregion

            Init();
        }

        private void Init()
        {
            InitData();
            InitRAG();
            InitKAG(); //not working properly
        }
        private void InitData()
        {
            try
            {
                sessionHistoryDatabase.CreateHistoryTable();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private void InitRAG()
        {
            try
            {
                accountRAGDatabase.CreateDocumentTable();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void InitKAG()
        {
            accountKAGDatabase.CreateDocumentTable();
            accountKAGDatabase.CreateKeyWordTable();
            //這會需要在後面
            accountKAGDatabase.CreateKeyWordExistTable();
        }

        #endregion
        #region RAG
        internal (float[] embedding, string id, string text, double score)[] SearchTopNDocument(string query, int topN, double score_threshold)
        {
            var allResults = new List<(float[] embedding, string id, string text, double score)>();

            allResults.AddRange(accountRAGDatabase.FindTopMatches(query, topN, score_threshold));

            allResults.AddRange(DatabaseUtils.getGlobalRAGDatabase().FindTopMatches(query, topN, score_threshold));

            return allResults
                .OrderByDescending(r => r.score) // 如果 `FindTopMatches` 已排序可省略
                .Take(topN)
                .ToArray();
        }

        internal bool InsertDocumentToDB(string path, bool force)
        {
            DocumentLoader.DocumentLoader loader = new DocumentLoader.DocumentLoader();
            return loader.Load(accountRAGDatabase, path, TokenizerUtils.param.MaxChunkLength, TokenizerUtils.param.Overlap, force);
        }
        internal bool InsertDocumentToDB(string path, int maxChunkLength, int overlap, bool force)
        {
            DocumentLoader.DocumentLoader loader = new DocumentLoader.DocumentLoader();
            return loader.Load(accountRAGDatabase, path, maxChunkLength, overlap, force);
        }
        #endregion

        #region KAG
        public void UpSertData(string id, string title, string content)
        {
            accountKAGDatabase.UpSertData(id, title, content);
        }

        public void CreateData(string id, string title, string content)
        {
            accountKAGDatabase.CreateData(id, title, content);
        }

        public void CreateKeyword(string keyword)
        {
            accountKAGDatabase.CreateKeyword(keyword);
        }

        public void LinkData(string doc_id, string keyword)
        {
            accountKAGDatabase.LinkData(doc_id, keyword);
        }
        #endregion

        #region 歷史相關
        public string? AddHistory(string query, string respond, int award = 1)
        {
            var id = DateTime.Now.ToString("o");
            try
            {
                sessionHistoryDatabase.InsertHistory(id, query, respond, award);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
            return id;
        }
        internal IEnumerable<ConversationRound>? GetHistory(int max = -1)
        {
            IEnumerable<ConversationRound>? result = null;
            try
            {
                string sql = $@"SELECT Query, Respond FROM TB00_SessionHistory ORDER BY InsertTime DESC";
                if (max != -1)
                {
                    sql += $" LIMIT {max}";
                }
                result = sessionHistoryDatabase.Query<ConversationRound>(DBAccessMode.ReadOnly, sql);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return result;
        }

        internal void cleanData()
        {
            sessionHistoryDatabase.ExecuteNonQuery(DBAccessMode.ReadWrite, @"DROP TABLE TB00_SessionHistory;");
        }

        internal bool UpdateAward(string id, int newAward)
        {
            int rowsAffected = 0;
            try
            {
                rowsAffected = sessionHistoryDatabase.UpdateAward(id, newAward);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return rowsAffected > 0;
        }
        #endregion
        internal static string CreateSessionId()
        {
            return DateTime.UtcNow.Ticks.ToString();
        }
    }
}
