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
        
            var inputs = new List<NamedOnnxValue>();
            
            foreach (var kv in _session.InputMetadata)
            {
                string name = kv.Key;
                var metadata = kv.Value;
                var elementType = metadata.ElementDataType;
                
                // Get the expected dimensions from metadata
                var expectedDims = metadata.Dimensions;
                
                // Create actual dimensions - handle dynamic dimensions (-1) properly
                int[] actualDims = new int[expectedDims.Length];
                for (int i = 0; i < expectedDims.Length; i++)
                {
                    if (expectedDims[i] == -1)
                    {
                        // Replace -1 with appropriate value based on position and input name
                        if (i == 0)
                            actualDims[i] = 1; // batch size
                        else if (i == 1)
                        {
                            // For past_key_values, the sequence dimension should start at 0 for fresh generation
                            if (name.StartsWith("past_key_values"))
                                actualDims[i] = 0; // No past context for embedding
                            else
                                actualDims[i] = seqLen; // Current sequence length
                        }
                        else if (i == 2)
                            actualDims[i] = name.StartsWith("past_key_values") ? 0 : seqLen;
                        else
                            actualDims[i] = (int)expectedDims[i]; // Keep other dimensions as-is
                    }
                    else
                    {
                        actualDims[i] = (int)expectedDims[i];
                    }
                }
        
                //Console.WriteLine($"Creating tensor for '{name}': expected dims [{string.Join(", ", expectedDims)}] -> actual dims [{string.Join(", ", actualDims)}], type: {elementType}");
        
                if (elementType == TensorElementType.Float)
                {
                    var floatTensor = new DenseTensor<float>(actualDims);
                    
                    // For past_key_values, we want empty tensors (all zeros) since we're not doing generation
                    // They should have shape [1, 8, 0, 128] for fresh inference
                    
                    inputs.Add(NamedOnnxValue.CreateFromTensor(name, floatTensor));
                }
                else if (elementType == TensorElementType.Int64)
                {
                    var longTensor = new DenseTensor<long>(actualDims);
                    
                    if (name == "input_ids")
                    {
                        for (int i = 0; i < Math.Min(seqLen, actualDims[1]); i++)
                            longTensor[0, i] = inputIds[i];
                    }
                    else if (name == "attention_mask")
                    {
                        for (int i = 0; i < Math.Min(seqLen, actualDims[1]); i++)
                            longTensor[0, i] = attentionMask[i];
                    }
                    else if (name == "position_ids")
                    {
                        // Position IDs should be [0, 1, 2, 3, ..., seqLen-1]
                        for (int i = 0; i < Math.Min(seqLen, actualDims[1]); i++)
                            longTensor[0, i] = i;
                    }
                    
                    inputs.Add(NamedOnnxValue.CreateFromTensor(name, longTensor));
                }
                else if (elementType == TensorElementType.Int32)
                {
                    var intTensor = new DenseTensor<int>(actualDims);
                    
                    if (name == "input_ids")
                    {
                        for (int i = 0; i < Math.Min(seqLen, actualDims[1]); i++)
                            intTensor[0, i] = (int)inputIds[i];
                    }
                    else if (name == "attention_mask")
                    {
                        for (int i = 0; i < Math.Min(seqLen, actualDims[1]); i++)
                            intTensor[0, i] = (int)attentionMask[i];
                    }
                    else if (name == "position_ids")
                    {
                        for (int i = 0; i < Math.Min(seqLen, actualDims[1]); i++)
                            intTensor[0, i] = i;
                    }
                    
                    inputs.Add(NamedOnnxValue.CreateFromTensor(name, intTensor));
                }
                else
                {
                    throw new NotSupportedException($"Unsupported input data type: {elementType} for input: {name}");
                }
            }
        /*
            // Debug: Print input shapes before inference
            Console.WriteLine("=== Input tensors ready for inference ===");
            foreach (var input in inputs)
            {
                var tensor = input.Value;
                string shapeStr = "unknown";
                
                if (tensor is Tensor<float> ft)
                    shapeStr = $"[{string.Join(", ", ft.Dimensions.ToString())}] (float)";
                else if (tensor is Tensor<long> lt)
                    shapeStr = $"[{string.Join(", ", lt.Dimensions.ToString())}] (long)";
                else if (tensor is Tensor<int> it)
                    shapeStr = $"[{string.Join(", ", it.Dimensions.ToString())}] (int)";
                    
                Console.WriteLine($"{input.Name}: {shapeStr}");
            }
        */
            // Execute inference
            using var results = _session.Run(inputs);
        /*
            // For generative models, we typically want the last hidden state
            // Look for outputs that might contain embeddings
            Console.WriteLine("=== Model Outputs ===");
            foreach (var result in results)
            {
                var tensor = result.Value;
                if (tensor is Tensor<float> ft)
                {
                    Console.WriteLine($"{result.Name}: [{string.Join(", ", ft.Dimensions.ToString())}]");
                }
            }
        */
            // Try to get the last hidden state or logits
            var hiddenStateEntry = results.FirstOrDefault(r => 
                r.Name.Contains("hidden") || r.Name.Contains("last") || r.Name == "logits" || results.Count() == 1);
            
            if (hiddenStateEntry != null)
            {
                var hiddenTensor = hiddenStateEntry.AsTensor<float>();
                var outputDims = hiddenTensor.Dimensions;
                
                //Console.WriteLine($"Using output '{hiddenStateEntry.Name}' with shape [{string.Join(", ", outputDims.ToString())}]");
                
                if (outputDims.Length >= 2)
                {
                    int batchSize = outputDims[0];
                    int seqLength = outputDims[1];
                    int hiddenDim = outputDims.Length > 2 ? outputDims[2] : 1;
                    
                    float[] raw = hiddenTensor.ToArray();
                    
                    // For generative models, we typically take the last token's representation
                    // or average across the sequence
                    
                    if (outputDims.Length == 3) // [batch, seq, hidden]
                    {
                        // Take the last valid token's embedding
                        int lastValidIndex = -1;
                        for (int i = Math.Min(seqLength, attentionMask.Length) - 1; i >= 0; i--)
                        {
                            if (attentionMask[i] == 1)
                            {
                                lastValidIndex = i;
                                break;
                            }
                        }
                        
                        if (lastValidIndex >= 0)
                        {
                            float[] embedding = new float[hiddenDim];
                            for (int j = 0; j < hiddenDim; j++)
                            {
                                embedding[j] = raw[lastValidIndex * hiddenDim + j];
                            }
                            return embedding;
                        }
                    }
                    else if (outputDims.Length == 2) // [batch, features] or [seq, vocab_size]
                    {
                        // If it's [batch, features], return the features directly
                        if (seqLength > 100) // Probably vocab size, take last token
                        {
                            float[] lastToken = new float[seqLength];
                            Array.Copy(raw, (batchSize - 1) * seqLength, lastToken, 0, seqLength);
                            return lastToken;
                        }
                        else // Probably features
                        {
                            return raw;
                        }
                    }
                }
            }
        
            throw new InvalidOperationException($"Cannot extract embeddings from this model. Available outputs: {string.Join(", ", results.Select(r => r.Name))}");
        }

    }
}
