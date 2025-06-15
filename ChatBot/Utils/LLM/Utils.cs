using LLama;
using System.Text;

namespace ChatBot.Utils.LLM
{
    internal static class Utils
    {
        internal static int CountTokens(LLamaWeights weights, string text)
        {
            var tokens = weights.Tokenize(text, false, true, Encoding.UTF8);
            return tokens.Length;
        }

        /// <summary>
        /// Rough count
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        internal static int RoughCountTokens(string text)
        {
            int ascii = 0, nonAscii = 0;
            foreach (char c in text)
            {
                if (c <= 0x7F)
                    ascii++;
                else
                    nonAscii++;
            }
            return ascii / 4 + nonAscii + 1;
        }

    }
}
