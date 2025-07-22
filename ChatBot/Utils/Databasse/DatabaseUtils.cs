using ChatBot.Database.RAG;
using ChatBot.Database.Interfaces;
using ChatBot.Utils.Embedding;
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
namespace ChatBot.Utils.Databasse
{
    internal static class DatabaseUtils
    {
        private static readonly string globalRAGPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "GlobalData" + Path.DirectorySeparatorChar + "GlobalDocumentData.duckdb";

        private static readonly string global_KAG_data = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "kuzu_data";

        internal static IRAGDatabase? _RAGdatabase;

        public static void Init()
        {
            #region Global RAG database
            #region Create folder for embedded database
            string GlobalDbDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "GlobalData";
            if (!Directory.Exists(GlobalDbDir))
            {
                Directory.CreateDirectory(GlobalDbDir);
            }
            #endregion

            string GlobalDataDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "GlobalData" + Path.DirectorySeparatorChar + "Documents";
            if (!Directory.Exists(GlobalDataDir))
            {
                Directory.CreateDirectory(GlobalDataDir);
            }

            _RAGdatabase = new RAGDuckDB(globalRAGPath);
            InitGlobalRAG(_RAGdatabase);
            #endregion

        }

        public static void InitGlobalRAG(IRAGDatabase RAGdatabase)
        {
                RAGdatabase.CreateDocumentTable();
        }

        public static void InitGlobalKAG(IKAGDatabase KAGdatabase)
        {
            //global_KnowledgeDB = new KAG(global_KAG_data);
        }

        internal static bool InsertDocumentToDB(string path, bool force)
        {
            DocumentLoader.DocumentLoader loader = new DocumentLoader.DocumentLoader();
            return loader.Load(_RAGdatabase, path, TokenizerUtils.param.MaxChunkLength, TokenizerUtils.param.Overlap, force);
        }

        internal static bool InsertDocumentToDB(string path, int maxChunkLength, int overlap, bool force)
        {
            DocumentLoader.DocumentLoader loader = new DocumentLoader.DocumentLoader();
            return loader.Load(_RAGdatabase, path, maxChunkLength, overlap, force);
        }
    }
}
