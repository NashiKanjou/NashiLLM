using Path = System.IO.Path;
using ChatBot.Binding;
using ChatBot.Services;
using YamlDotNet.Serialization;
using static ChatBot.Services.ModelServices;
using ChatBot.Utils.Databasse;
using LLama.Common;
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
    public class ArsChatSession
    {
        #region Setting/data
        private readonly string token;
        private readonly string id = "";
        private readonly string dbPath = "";

        private AccountSetting? setting;
        private SessionData? sessionData;

        private string model_used;

        private string summary = "";
        #endregion 

        public ArsChatSession(string token, string save_path, string id = "", string model = "default")
        {
            this.token = token;
            model_used = model;
            dbPath = save_path + Path.DirectorySeparatorChar + token;

            if (string.IsNullOrWhiteSpace(id.Trim()))
            {
                this.id = SessionData.CreateSessionId();
            }
            else
            {
                this.id = id;
            }
            Init();
        }

        #region Initialize
        private void GenNewSetting(string file)
        {

            if (!File.Exists(file))
            {
                // 檔案不存在，直接寫入全部設定
                var lines = AccountSetting.default_setting.Select(kvp => $"{kvp.Key}: {kvp.Value}");
                File.WriteAllLines(file, lines);
                return;
            }

            // 檔案存在，讀取現有設定行
            var existingLines = File.ReadAllLines(file).ToList();

            // 解析現有行的 key（冒號前的欄位名稱）
            var existingKeys = existingLines
                .Select(line => line.Split(new[] { ':' }, 2)[0].Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 找出缺少的設定並加入
            var missingLines = AccountSetting.default_setting
                .Where(kvp => !existingKeys.Contains(kvp.Key))
                .Select(kvp => $"{kvp.Key}: {kvp.Value}");

            if (missingLines.Any())
            {
                File.AppendAllLines(file, missingLines);
            }
        }
        private void Init()
        {
            string path = dbPath + Path.DirectorySeparatorChar + "AccountSetting.yaml";
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                GenNewSetting(path);
                string yml = string.Join(Environment.NewLine, File.ReadAllLines(path));
                var deserializer = new DeserializerBuilder().Build();

                setting = deserializer.Deserialize<AccountSetting>(yml);
                setting.lastLoginDate = DateTime.Now;
            }
            string AccountDocDir = dbPath + Path.DirectorySeparatorChar + "Documents";
            if (!Directory.Exists(AccountDocDir))
            {
                Directory.CreateDirectory(AccountDocDir);
            }
            LoadSummary();
            string accountRAG = dbPath + Path.DirectorySeparatorChar + "AccountData.duckdb";
            string sessionDataPath = dbPath + Path.DirectorySeparatorChar + this.id + Path.DirectorySeparatorChar + "SessionData.duckdb";
            string accountKAG = dbPath + Path.DirectorySeparatorChar + "kuzu_data";
            sessionData = new SessionData(accountRAG, sessionDataPath, accountKAG);
        }
        private void LoadSummary()
        {
            string file = dbPath + Path.DirectorySeparatorChar + this.id + Path.DirectorySeparatorChar + "summary";
            if (File.Exists(file))
            {
                summary = File.ReadAllText(file);
            }
            else
            {
                summary = "";
            }
        }
        #endregion

        #region Query
        private void AddHistory(string query, string respond, int award = 0)
        {
            var doc_id = sessionData.AddHistory(query, respond, award);
        }
        public void AddToKnowledge(string doc_id, string query, string respond)
        {
            sessionData.UpSertData(doc_id, query, respond);
            var tags = KAGUtils.GenerateTags(respond);
            var tags_query = KAGUtils.GenerateTags(query);
            var tags_set = new HashSet<string>();
            foreach (var tag in tags)
            {
                tags_set.Add(tag);
            }
            foreach (var tag in tags_query)
            {
                tags_set.Add(tag);
            }
            foreach (var tag in tags_set)
            {
                sessionData.CreateKeyword(tag);
                sessionData.LinkData(doc_id, tag);
            }
        }
        public ModelResult Query(string query)
        {
            (float[] embedding, string id, string text, double score)[] documents = GetDocuments(query, 5, 0.45);

            // 合併所有 text 為 document
            string document = string.Join("\n", documents.Select(d => d.text));

            // 抽取 (source, score) 清單（無論是否來自檔案）
            var sourceScoreList = documents
            .Select(d => (d.id, d.score))
            .ToList();

            return ModelServices.Queue(this, model_used, query, summary, document, sourceScoreList, sessionData.sessionHistoryDatabase);
        }

        public ModelResult Query(string query, string document)
        {
            return ModelServices.Queue(this, model_used, query, summary, document, new List<(string, double)>(), sessionData.sessionHistoryDatabase);
        }

        public ModelResult Query(string query, string document, string summary)
        {
            return ModelServices.Queue(this, model_used, query, summary, document, new List<(string, double)>(), sessionData.sessionHistoryDatabase);
        }

        internal ModelResult SummaryQuery(string query, string respond, ChatHistory old_chatHistory)
        {
            return ModelServices.SummaryQueue(this, this.summary, query, respond, old_chatHistory);
        }

        private (float[] embedding, string id, string text, double score)[] GetDocuments(string query, int topN, double similarity)
        {
            return sessionData.SearchTopNDocument(query, topN, similarity);
        }

        /// <summary>
        /// -1 for all
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public IEnumerable<ConversationRound>? GetHistory(int max = -1)
        {
            return sessionData.GetHistory(max);
        }
        internal void UpdateSummary(string new_summary)
        {
            summary = new_summary;
            //TODO  Send back to user
            Console.WriteLine("已更新記憶");
        }
        internal void ProcessResult(string query, string result, List<(string, double)> sources, bool useStreaming)
        {
            if (!useStreaming)
            {
                Console.WriteLine(result);
            }
            else
            {
                Console.WriteLine();
            }
            AddHistory(query, result);
            //TODO  Send back to user
            foreach (var source in sources)
            {
                Console.WriteLine(source.Item1 + " (Similarity" + source.Item2 + ")");
            }
            Console.WriteLine("已記錄此對話至資料庫");
        }
        internal void ProcessParticleResult(string result, bool useStreaming)
        {
            if (useStreaming)
            {
                Console.Write(result);
            }
        }

        #endregion

        #region 額外設定
        public string getCurrentModel()
        {
            return ModelServices.getModelName(model_used);
        }
        public void setModel(string model = "default")
        {
            model_used = model;
        }
        public string getToken()
        {
            return token;
        }
        #endregion

        #region 關閉/存檔Session

        public void SaveSummary()
        {
            string file = dbPath + Path.DirectorySeparatorChar + this.id + Path.DirectorySeparatorChar + "summary";
            File.WriteAllText(file, summary);
        }
        public void SaveAccountSetting()
        {
            string file = dbPath + Path.DirectorySeparatorChar + "AccountSetting.yaml";
            var serializer = new SerializerBuilder().Build();
            if (setting != null)
            {
                setting.lastLoginDate = DateTime.Now;
                var yaml = serializer.Serialize(setting);
                File.WriteAllText(file, yaml);
            }
        }

        /// <summary>
        /// delete the file if false
        /// </summary>
        /// <param name="save"></param>
        public void SaveSession(bool deleteHistory = false)
        {
            if (deleteHistory)
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            }
        }

        public void CloseSession()
        {
            SaveAccountSetting();
            SaveSession(false);
            SaveSummary();
        }
        #endregion

        #region RAG
        internal bool InsertDocumentToDB(string path, bool force)
        {
            return sessionData.InsertDocumentToDB(path, force);
        }

        internal bool InsertDocumentToDB(string path, int maxChunkLength, int overlap, bool force)
        {
            return sessionData.InsertDocumentToDB(path, maxChunkLength, overlap, force);
        }

        internal void InsertDocumentToDB(bool force)
        {
            string[] allFiles = Directory.GetFiles(dbPath + Path.DirectorySeparatorChar + "Documents", "*.*", SearchOption.AllDirectories);
            foreach (string file in allFiles)
            {
                sessionData.InsertDocumentToDB(file, false);
            }
        }
        internal void InsertDocumentToDB(int maxChunkLength, int overlap, bool force)
        {
            string[] allFiles = Directory.GetFiles(dbPath + Path.DirectorySeparatorChar + "Documents", "*.*", SearchOption.AllDirectories);
            foreach (string file in allFiles)
            {
                sessionData.InsertDocumentToDB(file, maxChunkLength, overlap, false);
            }
        }

        #endregion
    }
}
