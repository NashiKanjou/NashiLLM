using ChatBot.Binding;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Tokenizers.DotNet;
using YamlDotNet.Serialization;
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
namespace ChatBot.Utils.Embedding
{
    internal static class TokenizerUtils
    {
        private static EmbeddingGeneratorPool? embeddingPool;
        private static TokenizerPool? tokenizerPool;
        internal static EmbeddedModelParam? param;
        internal static void Init()
        {
            string modelDir = Path.Combine(Directory.GetCurrentDirectory(), "embedded_model");
            string paramPath = Path.Combine(modelDir, "param");
            string yml = string.Join(Environment.NewLine, File.ReadAllLines(paramPath));
            var deserializer = new DeserializerBuilder().Build();
            param = deserializer.Deserialize<EmbeddedModelParam>(yml);


            string embeddedModelPath = Path.Combine(modelDir, "model.onnx");
            embeddingPool = new EmbeddingGeneratorPool(embeddedModelPath, param.Workers, param.UseCUDA);

            string tokenizerPath = Path.Combine(modelDir, "tokenizer.json");
            tokenizerPool = new TokenizerPool(tokenizerPath, param.Workers);
        }

        public static float[] GetEmbedding(string text)
        {
            if (embeddingPool == null || tokenizerPool == null || param == null)
                throw new InvalidOperationException("Model not initialized.");

            var tokenizer = tokenizerPool.Rent();
            var inputIds = tokenizer.Encode(text);
            tokenizerPool.Return(tokenizer);

            // 截斷或 padding
            if (inputIds.Length > param.MaxChunkLength)
            {
                inputIds = inputIds.Take(param.MaxChunkLength).ToArray();
            }
            else if (inputIds.Length < param.MaxChunkLength)
            {
                inputIds = inputIds.Concat(Enumerable.Repeat(0U, param.MaxChunkLength - inputIds.Length)).ToArray();
            }

            // attention mask: 1 for token, 0 for padding
            long[] attentionMask = inputIds.Select(id => id == 0 ? 0L : 1L).ToArray();

            var generator = embeddingPool.Rent();
            try
            {
                return generator.GenerateEmbedding(inputIds, attentionMask);
            }
            finally
            {
                embeddingPool.Return(generator);
            }
        }

        public static uint[] Encode(string text)
        {
            var tokenizer = tokenizerPool?.Rent() ?? throw new InvalidOperationException("Tokenizer not initialized.");
            try
            {
                return tokenizer.Encode(text);
            }
            finally
            {
                tokenizerPool.Return(tokenizer);
            }
        }

        #region 文本切割
        internal static List<string> SplitTextIntoChunks(string text)
        {
            return SplitTextIntoChunks(text, param.MaxChunkLength, param.Overlap);
        }

        internal static List<string> SplitTextIntoChunks(string text, int maxChunkLength, int overlap = 20)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            // 依據中英文標點做初步切段
            var sentences = Regex.Split(text, @"(?<=[。！？!?；;,.，、])");

            var buffer = "";
            foreach (var sentence in sentences)
            {
                if (buffer.Length + sentence.Length <= maxChunkLength)
                {
                    buffer += sentence;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(buffer))
                    {
                        chunks.Add(PadToLength(buffer.Trim(), maxChunkLength));
                    }

                    // 若單句已超長，進一步硬切（避免單句超長）
                    if (sentence.Length > maxChunkLength)
                    {
                        var subChunks = SplitByLengthWithOverlap(sentence, maxChunkLength, overlap)
                            .Select(chunk => PadToLength(chunk, maxChunkLength));
                        chunks.AddRange(subChunks);
                        buffer = "";
                    }
                    else
                    {
                        buffer = sentence;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(buffer))
                chunks.Add(PadToLength(buffer.Trim(), maxChunkLength));

            return chunks;
        }
        private static string PadToLength(string input, int targetLength, char paddingChar = ' ')
        {
            if (input.Length >= targetLength)
                return input;
            return input.PadRight(targetLength, paddingChar);
        }

        private static List<string> SplitByLengthWithOverlap(string text, int maxLen, int overlap)
        {
            var result = new List<string>();

            for (int i = 0; i < text.Length; i += maxLen - overlap)
            {
                int len = Math.Min(maxLen, text.Length - i);
                string chunk = text.Substring(i, len);

                // 補齊到 maxLen
                if (chunk.Length < maxLen)
                {
                    chunk = chunk.PadRight(maxLen, ' ');
                }

                result.Add(chunk);

                if (i + len >= text.Length)
                    break;
            }

            return result;
        }
        public static byte[] FloatArrayToBytes(float[] floats)
        {
            byte[] bytes = new byte[floats.Length * sizeof(float)];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static float[] DeserializeEmbedding(byte[] bytes)
        {
            int floatCount = bytes.Length / sizeof(float);
            float[] floats = new float[floatCount];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }
        #endregion
    }
    #region Pool
    public class EmbeddingGeneratorPool
    {
        private readonly ConcurrentBag<EmbeddingGenerator> _pool = new();
        private readonly SemaphoreSlim _semaphore;

        public EmbeddingGeneratorPool(string modelPath, int size, bool useCUDA)
        {
            _semaphore = new SemaphoreSlim(size, size);
            for (int i = 0; i < size; i++)
            {
                _pool.Add(new EmbeddingGenerator(modelPath, useCUDA));
            }
        }

        public EmbeddingGenerator Rent()
        {
            _semaphore.Wait();
            if (_pool.TryTake(out var generator))
                return generator;

            throw new InvalidOperationException("Pool empty unexpectedly.");
        }

        public void Return(EmbeddingGenerator generator)
        {
            _pool.Add(generator);
            _semaphore.Release();
        }
    }

    public class TokenizerPool
    {
        private readonly ConcurrentBag<Tokenizer> _pool = new();
        private readonly SemaphoreSlim _semaphore;

        public TokenizerPool(string tokenizerPath, int size)
        {
            _semaphore = new SemaphoreSlim(size, size);
            for (int i = 0; i < size; i++)
            {
                _pool.Add(new Tokenizer(tokenizerPath));
            }
        }

        public Tokenizer Rent()
        {
            _semaphore.Wait();
            if (_pool.TryTake(out var tokenizer))
                return tokenizer;

            throw new InvalidOperationException("Tokenizer pool empty.");
        }

        public void Return(Tokenizer tokenizer)
        {
            _pool.Add(tokenizer);
            _semaphore.Release();
        }
    }
    #endregion
}
