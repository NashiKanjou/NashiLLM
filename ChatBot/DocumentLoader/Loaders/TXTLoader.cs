using ChatBot.Database.Interfaces;
using ChatBot.DocumentLoader.Utils;
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
namespace ChatBot.DocumentLoader.Loaders
{
    internal static class TXTLoader
    {
        public static bool Load(IRAGDatabase db, string file, int chunk_size, int overlap)
        {
            if (!File.Exists(file) || !file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return false;

            var content = File.ReadAllLines(file).ToList();

            SharedFunctions.ChunkText(db, file, content, chunk_size, overlap);
            //db.Connect(DBAccessMode.ReadWrite);
            //var insertTime = DateTime.UtcNow.ToString("o"); // ISO-8601 格式
            //// 在這裡你可以將 chunks 儲存到資料庫、送去嵌入模型等
            //foreach (var chunk in chunks)
            //{
            //    Console.WriteLine($"[Chunk] {chunk}");
            //    db.InsertDocument(insertTime, file, chunk, 0);
            //}
            //db.Close();
            return true;
        }

    }
}
