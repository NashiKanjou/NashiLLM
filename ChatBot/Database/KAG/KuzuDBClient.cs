using ChatBot.Database.Interfaces;
using KuzuClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
namespace ChatBot.Database.KAG
{
    internal class KuzuDBClient : IKAGDatabase
    {
        private readonly string kuzuConn;
        public KuzuDBClient(string connString)
        {
            kuzuConn = connString;
        }

        public void Dispose()
        {
            return;
        }
        #region Init
        public void CreateDocumentTable()
        {
            using (var conn = new KuzuConnection(this.kuzuConn))
            {
                try { conn.Execute("CREATE NODE TABLE Document(id STRING, title STRING, content STRING, PRIMARY KEY(id))"); }
                catch (Exception e) { /* 忽略 */ }
            }
        }

        public void CreateKeyWordTable()
        {
            using (var conn = new KuzuConnection(this.kuzuConn))
            {
                try { conn.Execute("CREATE NODE TABLE Keyword(word STRING, PRIMARY KEY(word))"); }
                catch (Exception e) { /* 忽略 */ }
            }
        }

        public void CreateKeyWordExistTable()
        {
            using (var conn = new KuzuConnection(this.kuzuConn))
            {
                try { conn.Execute("CREATE REL TABLE KeywordExist(FROM Document TO Keyword)"); }
                catch (Exception e) { /* 忽略 */ }
                }
            }
        #endregion

        public void CreateData(string id, string title, string content)
        {
            using (var conn = new KuzuConnection(kuzuConn))
            {
                var safeId = id.Replace("'", "\\'");
                var safeTitle = title.Replace("'", "\\'");
                var safeContent = content.Replace("'", "\\'");
                try
                {
                    conn.Execute($"CREATE (d:Document {{id:'{safeId}', title:'{safeTitle}', content:'{safeContent}'}})");
                }
                catch (Exception e) when (e.Message.Contains("already exists")) { }
            }
        }

        public void UpSertData(string id, string title, string content)
        {
            using var conn = new KuzuConnection(kuzuConn);
            var safeId = id.Replace("'", "\\'");
            var safeTitle = title.Replace("'", "\\'");
            var safeContent = content.Replace("'", "\\'");

            using var result = conn.ExecuteQuery($"MATCH (d:Document) WHERE d.id = '{safeId}' RETURN d");
            if (result.Read())
            {
                conn.Execute($@"
            MATCH (d:Document) 
            WHERE d.id = '{safeId}' 
            SET d.title = '{safeTitle}', d.content = '{safeContent}'
        ");
            }
            else
            {
                conn.Execute($@"
            CREATE (d:Document {{id: '{safeId}', title: '{safeTitle}', content: '{safeContent}'}})
        ");
            }
        }

        public void CreateKeyword(string keyword)
        {
            using (var conn = new KuzuConnection(kuzuConn))
            {
                var safeKeyword = keyword.Replace("'", "\\'");
                try
                {
                    conn.Execute($"CREATE (k:Keyword {{word:'{safeKeyword}'}})");
                }
                catch (Exception e) when (e.Message.Contains("already exists")) { }
            }
        }

        public void LinkData(string doc_id, string keyword)
        {
            using (var conn = new KuzuConnection(kuzuConn))
            {
                var safeKeyword = keyword.Replace("'", "\\'");
                var safeId = doc_id.Replace("'", "\\'");
                try
                {
                    // 用 WHERE d.id=... AND k.word=... 限制條件
                    conn.Execute($@"
                MATCH (d:Document), (k:Keyword)
                WHERE d.id='{safeId}' AND k.word='{safeKeyword}'
                CREATE (d)-[:HasKeyword]->(k)
            ");
                }
                catch (Exception e)
                {
                    // 若失敗，可以選擇 log 或略過
                    Console.WriteLine($"[警告] 建立關聯失敗: {e.Message}");
                }
            }
        }

        public void DeleteKeyword(string keyword)
        {
            throw new NotImplementedException();
        }

        public void UnlinkData(string doc_id, string keyword)
        {
            throw new NotImplementedException();
        }
    }
}
