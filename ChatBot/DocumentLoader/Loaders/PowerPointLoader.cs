using ChatBot.Database.Interfaces;
using ChatBot.DocumentLoader.Utils;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
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
    internal static class PowerPointLoader
    {
        public static bool Load(IRAGDatabase db, string file, int chunkSize, int overlap)
        {
            if (!File.Exists(file) || !file.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
                return false;

            var paragraphs = new List<string>();

            using (PresentationDocument presentationDoc = PresentationDocument.Open(file, false))
            {
                var presentationPart = presentationDoc.PresentationPart;
                if (presentationPart == null) return false;

                var slideParts = presentationPart.SlideParts;

                foreach (var slidePart in slideParts)
                {
                    foreach (var text in GetSlideTexts(slidePart))
                    {
                        paragraphs.Add(text);
                    }
                }
            }

            // 分段處理文字（可配合 chunkSize/overlap）
            SharedFunctions.ChunkText(db, file, paragraphs, chunkSize, overlap);

            return true;
        }

        private static IEnumerable<string> GetSlideTexts(SlidePart slidePart)
        {
            var texts = new List<string>();
            if (slidePart.Slide == null) return texts;

            foreach (var shape in slidePart.Slide.Descendants<Shape>())
            {
                var textBody = shape.TextBody;
                if (textBody == null) continue;

                foreach (var paragraph in textBody.Descendants<A.Paragraph>())
                {
                    string paraText = string.Join("", paragraph.Descendants<A.Text>().Select(t => t.Text));
                    if (!string.IsNullOrWhiteSpace(paraText))
                        texts.Add(paraText.Trim());
                }
            }

            return texts;
        }

    }
}
