using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
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
    public class EmbeddingGenerator
    {
        private readonly InferenceSession _session;

        public EmbeddingGenerator(string onnxModelPath, bool useCUDA)
        {
            var options = new SessionOptions();
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            if (availableProviders.Contains("CUDAExecutionProvider"))
            {
                if (useCUDA)
                {
                    options.AppendExecutionProvider_CUDA();
                }
            }

            _session = new InferenceSession(onnxModelPath, options);
            Console.WriteLine("Embedded Model inputs:");
            foreach (var kv in _session.InputMetadata)
                Console.WriteLine($"  • {kv.Key} : {string.Join("x", kv.Value.Dimensions)}");
        }

        /// <summary>
        /// 產生句子嵌入：  
        /// 1) 如果 ONNX export 有 sentence_embedding 或 pooler_output，就直接拿來回傳；  
        /// 2) 否則對 last_hidden_state 做 attention_mask 篩選後的平均池化。
        /// </summary>
        public float[] GenerateEmbedding(uint[] inputIds, long[] attentionMask)
        {
            int seqLen = inputIds.Length;

            // 1) 準備 ONNX 輸入 tensors
            var idTensor = new DenseTensor<long>(new[] { 1, seqLen });
            var maskTensor = new DenseTensor<long>(new[] { 1, seqLen });
            //var typeTensor = new DenseTensor<long>(new[] { 1, seqLen });  // 全 0

            for (int i = 0; i < seqLen; i++)
            {
                idTensor[0, i] = inputIds[i];
                maskTensor[0, i] = attentionMask[i];
                //typeTensor[0, i] = 0L;
            }

            //var inputs = new List<NamedOnnxValue>
            //{
            //    NamedOnnxValue.CreateFromTensor("input_ids",       idTensor),
            //    NamedOnnxValue.CreateFromTensor("attention_mask",  maskTensor),
            //    NamedOnnxValue.CreateFromTensor("token_type_ids",  typeTensor),
            //};
            var inputs = new List<NamedOnnxValue>();
            foreach (var kv in _session.InputMetadata)
            {
                string name = kv.Key;
                Tensor<long> tensor;

                if (name == "input_ids")
                    tensor = idTensor;
                else if (name == "attention_mask")
                    tensor = maskTensor;
                else
                {
                    // 其他所有輸入（如 token_type_ids），都給一個全 0 的 tensor
                    // 將 metadata 裡的 -1 都替換成當前的 seqLen
                    var _dims = kv.Value.Dimensions
                                 .Select(d => d < 0 ? seqLen : d)
                                 .ToArray();
                    tensor = new DenseTensor<long>(_dims);
                    // DenseTensor ctor 預設元素即為 0
                }

                inputs.Add(NamedOnnxValue.CreateFromTensor(name, tensor));
            }
            // 2) 執行推論
            using var results = _session.Run(inputs);

            // 3) 嘗試拿 pooling head（如果有）
            string[] poolerNames = { "sentence_embedding", "pooler_output" };
            var poolerEntry = results
                .FirstOrDefault(r => poolerNames.Contains(r.Name, StringComparer.OrdinalIgnoreCase));

            if (poolerEntry != null)
            {
                // pooler_output shape = [1, hiddenDim]
                return poolerEntry
                    .AsTensor<float>()
                    .ToArray();
            }

            // 4) 找 last_hidden_state (shape = [1, seqLen, hiddenDim])
            var lastEntry = results.FirstOrDefault(r => r.AsTensor<float>().Dimensions.Length == 3);
            if (lastEntry == null)
                throw new InvalidOperationException("找不到 last_hidden_state 輸出。");

            // 2) 取 tensor 與尺寸
            var lastHidden = lastEntry.AsTensor<float>();
            var dims = lastHidden.Dimensions;     // [1, seqLen, hiddenDim]
            int seqLen_ = dims[1];
            int hiddenDim = dims[2];
            float[] raw = lastHidden.ToArray();      // flatten 長度 = seqLen * hiddenDim

            // 3) Mask + sum pooling
            double[] sumVecD = new double[hiddenDim];
            int realCount = 0;
            for (int i = 0; i < seqLen_; i++)
            {
                if (attentionMask[i] == 1)
                {
                    realCount++;
                    for (int j = 0; j < hiddenDim; j++)
                        sumVecD[j] += raw[i * hiddenDim + j];
                }
            }

            // 4) 平均
            if (realCount > 0)
                for (int j = 0; j < hiddenDim; j++)
                    sumVecD[j] /= realCount;

            // 5) L2 正規化
            double norm = Math.Sqrt(sumVecD.Sum(x => x * x));
            if (norm > 0)
                for (int j = 0; j < hiddenDim; j++)
                    sumVecD[j] /= norm;

            // 6) 回傳 float[]
            return sumVecD.Select(x => (float)x).ToArray();
        }

    }
}
