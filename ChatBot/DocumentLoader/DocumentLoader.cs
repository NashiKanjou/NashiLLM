using ChatBot.Database.Interfaces;
using ChatBot.DocumentLoader.Interface;
using ChatBot.DocumentLoader.Loaders;
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
namespace ChatBot.DocumentLoader
{
    internal class DocumentLoader : IDocumentLoader
    {
        public bool Load(IRAGDatabase db, string file, int chunk_size, int overlap, bool force)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"[Error] File not found: {file}");
                return false;
            }

            if (!force)
            {
                var fileInfo = new FileInfo(file);
                DateTime lastModified = fileInfo.LastWriteTime;
                DateTime? insertTime;
                insertTime = db.GetLatestInsertTimeBySource(file);
                if (insertTime.HasValue && insertTime.Value >= lastModified)
                {
                    // 資料庫中記錄時間「等於或晚於」檔案最後修改時間 → 不用重建
                    return false;
                }
            }
            db.RemoveDocument(file);
            string extension = Path.GetExtension(file).ToLowerInvariant();

            switch (extension)
            {
                case ".txt":
                    return TXTLoader.Load(db, file, chunk_size, overlap);

                case ".pdf":
                    return PDFLoader.Load(db, file, chunk_size, overlap);

                case ".docx":
                    return WordLoader.Load(db, file, chunk_size, overlap);

                case ".pptx":
                    return PowerPointLoader.Load(db, file, chunk_size, overlap);

                default:
                    Console.WriteLine($"[Error] Unsupported file type: {extension}");
                    return false;
            }
        }

        public bool Load(IRAGDatabase db, string file, bool force)
        {
            return Load(db, file, TokenizerUtils.param.MaxChunkLength, TokenizerUtils.param.Overlap, force);
        }
    }
}
