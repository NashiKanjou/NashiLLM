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
namespace ChatBot.Utils.LLM
{
    public static class PromptParams
    {
        private static string keyword_Query = "{query}";
        private static string keyword_Summary = "{summary}";
        private static string keyword_Document = "{document}";
        private static string keyword_Respond = "{respond}";
        public static string Query { get { return keyword_Query; } }
        public static string Summary { get { return keyword_Summary; } }
        public static string Document { get { return keyword_Document; } }
        public static string Respond { get { return keyword_Respond; } }

    }
}
