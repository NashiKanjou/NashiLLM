namespace ChatBot.Database.RAG
{
    internal static class SimilarityTest
    {

        internal static double GetSimilarity(string a,float[] a_v, string b,float[] b_v)
        {
            var leven = NormalizedLevenshtein(a, b);
            var cos = CosineSimilarity(a_v,b_v);
            return Math.Min(cos * 0.7 + leven * 0.3,1);
        }
        internal static int LevenshteinDistance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = new int[] {
            d[i - 1, j] + 1,     // deletion
            d[i, j - 1] + 1,     // insertion
            d[i - 1, j - 1] + cost // substitution
        }.Min();
                }

            return d[a.Length, b.Length];
        }
        internal static float NormalizedLevenshtein(string a, string b)
        {
            int dist = LevenshteinDistance(a, b);
            int maxLen = Math.Max(a.Length, b.Length);
            return 1 - (float)dist / maxLen;
        }
        internal static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                return -1;
                //throw new InvalidDataException($"向量長度不一致：{vectorA.Length} vs {vectorB.Length}");
            }
            double dot = 0.0;
            double magA = 0.0;
            double magB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dot += vectorA[i] * vectorB[i];
                magA += vectorA[i] * vectorA[i];
                magB += vectorB[i] * vectorB[i];
            }

            magA = Math.Sqrt(magA);
            magB = Math.Sqrt(magB);

            if (magA == 0 || magB == 0)
                return 0;

            return dot / (magA * magB);
        }
    }
    
}
