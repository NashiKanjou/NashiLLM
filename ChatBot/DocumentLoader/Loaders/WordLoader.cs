using ChatBot.Database.Interfaces;
using ChatBot.DocumentLoader.Utils;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
    internal static class WordLoader
    {
        public static bool Load(IRAGDatabase db, string file, int chunk_size, int overlap)
        {
            if (!File.Exists(file) || !file.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return false;

            var paragraphs = new List<string>();

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(file, false))
            {
                var body = wordDoc.MainDocumentPart.Document.Body;
                if (body == null) return false;

                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                        paragraphs.Add(text.Trim());
                }
            }

            SharedFunctions.ChunkText(db, file, paragraphs, chunk_size, overlap);

            return true;
        }

    }
}
