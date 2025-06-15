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
    internal interface IHistoryDatabase : IDatabase
    {
        void CreateHistoryTable();

        IEnumerable<T> Query<T>(DBAccessMode mode, string sql) where T : new();

        int ExecuteNonQuery(DBAccessMode mode, string sql);

        int ExecuteNonQuery(DBAccessMode mode, string sql, (string?, object?)[] param);

        void InsertHistory(string id, string query, string respond, int award);

        int UpdateAward(string id, int newAward);

        IEnumerable<(string Query, string Respond)> getHistory(int max = -1, int award_threshold = 0);
    }
}
