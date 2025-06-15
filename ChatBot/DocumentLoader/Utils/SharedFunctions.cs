using ChatBot.Database.Interfaces;
using System.Text.RegularExpressions;
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
namespace ChatBot.DocumentLoader.Utils
{
    internal static class SharedFunctions
    {
        internal static void ChunkText(IRAGDatabase db, string file, List<string> paragraphs, int chunkSize, int overlap)
        {
            if (chunkSize <= 0) throw new ArgumentException("chunkSize 必須大於 0");
            if (overlap < 0) throw new ArgumentException("overlap 不能小於 0");
            if (overlap >= chunkSize) throw new ArgumentException("overlap 必須小於 chunkSize");

            // 1) 把所有段落合成一個大字串
            var text = string.Join("\n", paragraphs);
            var chunks = new List<string>();

            int pos = 0;
            int textLen = text.Length;
            var insertTime = DateTime.UtcNow.ToString("o");
            // 2) 以固定字元長度切分，並跟前一塊重疊 overlap 個字
            while (pos < textLen)
            {
                int len = Math.Min(chunkSize, textLen - pos);
                //chunks.Add(text.Substring(pos, len));
                string chunk = text.Substring(pos, len);
                db.InsertDocument(insertTime, file, chunk, 0);
                //Console.WriteLine($"[Chunk] {chunk}");
                pos += len;
                if (pos >= textLen)
                {
                    break;
                }
                pos -= overlap;            // 回退 overlap 個字以製造重疊
            }
            //return chunks;
        }

        //private static void test(IRAGDatabase db, List<string> paragraphs, int chunkSize, int overlap)
        //{
        //    db.Connect(DBAccessMode.ReadWrite);
        //    var insertTime = DateTime.UtcNow.ToString("o"); // ISO-8601 格式
        //    // 在這裡你可以將 chunks 儲存到資料庫、送去嵌入模型等
        //    foreach (var chunk in chunks)
        //    {
        //        Console.WriteLine($"[Chunk] {chunk}");
        //        db.InsertDocument(insertTime, file, chunk, 0);
        //    }
        //    db.Close();
        //}
        internal static List<string> ChunkTextBySentence(List<string> paragraphs, int chunkSize, int overlap)
        {
            var allText = string.Join("\n", paragraphs);

            // 使用中英文標點做切割
            var sentences = Regex.Split(allText, @"(?<=[。！？；?!;])")
                                 .Select(s => s.Trim())
                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                 .ToList();

            var chunks = new List<string>();
            var currentChunk = new List<string>();
            int currentLength = 0;

            for (int i = 0; i < sentences.Count; i++)
            {
                var sentence = sentences[i];
                currentChunk.Add(sentence);
                currentLength += sentence.Length;

                if (currentLength >= chunkSize || i == sentences.Count - 1)
                {
                    chunks.Add(string.Join(" ", currentChunk));

                    if (overlap > 0 && currentChunk.Count > 1)
                    {
                        var overlapChunk = currentChunk.Skip(Math.Max(0, currentChunk.Count - overlap)).ToList();
                        currentChunk = new List<string>(overlapChunk);
                        currentLength = overlapChunk.Sum(s => s.Length);
                    }
                    else
                    {
                        currentChunk = new List<string>();
                        currentLength = 0;
                    }
                }
            }

            return chunks;
        }
    }
}
