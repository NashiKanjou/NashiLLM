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

namespace ChatBot.Database.Interfaces
{
    internal interface IKAGDatabase : IDatabase
    {
        void CreateDocumentTable();
        void CreateKeyWordTable();
        void CreateKeyWordExistTable();

        void CreateData(string id, string title, string content);

        void UpSertData(string id, string title, string content);

        void CreateKeyword(string keyword);

        void DeleteKeyword(string keyword);

        void LinkData(string doc_id, string keyword);

        void UnlinkData(string doc_id, string keyword);
    }
}
