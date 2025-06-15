using ChatBot.Session;
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
namespace ChatBot.Services
{
    public class ChatService
    {
        private readonly Dictionary<string, ArsChatSession> sessions = new Dictionary<string, ArsChatSession>();

        private string sessions_root_path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "sessions";

        public ChatService(string sessions_root_path = "")
        {
            if (!string.IsNullOrEmpty(sessions_root_path))
            {
                this.sessions_root_path = sessions_root_path;
            }
            //check if folder exist
            if (!Directory.Exists(this.sessions_root_path))
            {
                Directory.CreateDirectory(this.sessions_root_path);
            }

        }

        public ArsChatSession CreateSession(string token)
        {
            ArsChatSession session = new ArsChatSession(token, sessions_root_path);
            CloseSession(token);
            sessions[token] = session;

            return session;
        }

        public ArsChatSession LoadSession(string token, string id)
        {
            ArsChatSession session = new ArsChatSession(token, sessions_root_path, id);
            CloseSession(token);
            sessions[token] = session;

            return session;
        }

        internal bool InsertDocumentToDB(string path, bool force)
        {
            return DatabaseUtils.InsertDocumentToDB(path, force);
        }

        internal bool InsertDocumentToDB(string path, int maxChunkLength, int overlap, bool force)
        {
            return DatabaseUtils.InsertDocumentToDB(path, maxChunkLength, overlap, force);
        }

        internal void InsertDocumentToDB(bool force)
        {
            string[] allFiles = Directory.GetFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "GlobalData" + Path.DirectorySeparatorChar + "Documents", "*.*", SearchOption.AllDirectories);
            foreach (string file in allFiles)
            {
                DatabaseUtils.InsertDocumentToDB(file, false);
            }
        }

        internal void InsertDocumentToDB(int maxChunkLength, int overlap, bool force)
        {
            string[] allFiles = Directory.GetFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "GlobalData" + Path.DirectorySeparatorChar + "Documents", "*.*", SearchOption.AllDirectories);
            foreach (string file in allFiles)
            {
                DatabaseUtils.InsertDocumentToDB(file, maxChunkLength, overlap, false);
            }
        }

        /// <summary>
        /// 用戶退出時呼叫
        /// </summary>
        /// <param name="token"></param>
        public void CloseSession(string token)
        {
            if (sessions.ContainsKey(token))
            {
                ArsChatSession session = sessions[token];
                session.CloseSession();
                sessions.Remove(token);
            }
        }

        public void CloseService()
        {
            foreach (string token in sessions.Keys)
            {
                ArsChatSession session = sessions[token];
                session.CloseSession();
            }
        }
    }
}
