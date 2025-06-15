using ChatBot.Utils.LLM;
using LLama;
using LLama.Common;
using System.Runtime.CompilerServices;
using ChatResponse = ChatBot.Binding.ChatResponse;

namespace ChatBot.Services
{
    internal class ChatModelSetting
    {
        internal uint ContextSize;
        internal int MaxTokens;
        internal bool UseStreaming;
        internal string SystemPrompt = string.Empty;
        internal string UserFormat = string.Empty;
        internal string AssistantFormat = string.Empty;
        internal string SummaryPrompt = string.Empty;
        internal string SummaryQuery = string.Empty;
    }
    internal class ChatModel : IDisposable
    {
        //public string? ModelName { get { return model_name; } }
        //private string? model_name;

        internal LLamaWeights weights { get { return model; } }
        private ModelParams model_params;
        private LLamaWeights model;
        private InferenceParams inferenceParams;
        //private LLamaContext context;
        //private InteractiveExecutor executor;

        internal ChatModelSetting Setting;
        internal ChatModel(LLamaWeights weight, ModelParams parameters, InferenceParams inference, ChatModelSetting setting)
        {
            model_params = parameters;
            model = weight;
            inferenceParams = inference;
            //context = weight.CreateContext(model_params);
            //executor = new InteractiveExecutor(context);
            Setting = setting;
        }

        public static uint EstimateContextSize(LLamaWeights weight, ChatHistory chatHistory, string input, int outputBufferTokens = 256)
        {
            var messages = chatHistory.Messages;
            uint totalTokens = 0;

            foreach (var msg in messages)
            {
                totalTokens += (uint)Utils.LLM.Utils.CountTokens(weight, msg.Content);
            }

            totalTokens += (uint)Utils.LLM.Utils.CountTokens(weight, input);
            totalTokens += (uint)outputBufferTokens;

            // 限制範圍，避免過小或過大
            return Math.Min(Math.Max(totalTokens, 512), uint.MaxValue); // 可依模型上限調整
        }
        private static string ExtractBeforeRespond(string input)
        {
            string marker = PromptParams.Respond;
            int index = input.IndexOf(marker);

            if (index == -1)
                return input; // 沒有 {respond} 就回傳整個輸入或可改為 throw

            return input.Substring(0, index);
        }
        internal async IAsyncEnumerable<ChatResponse> GenerateAsync(string query, string format_assistant, ChatHistory chatHistory, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string input = query + ExtractBeforeRespond(format_assistant);
            ModelParams param = new ModelParams(model_params.ModelPath)
            {
                ContextSize = EstimateContextSize(model, chatHistory, input, inferenceParams.MaxTokens),
                GpuLayerCount = model_params.GpuLayerCount,
                BatchSize = model_params.BatchSize,
                FlashAttention = model_params.FlashAttention,
                MainGpu = model_params.MainGpu,
                ModelPath = model_params.ModelPath,
                UseMemorymap = model_params.UseMemorymap
            };
            LLamaContext context = model.CreateContext(param);
            InteractiveExecutor executor = new InteractiveExecutor(context);
            ChatSession session = new ChatSession(executor, chatHistory);

            var outputBuffer = "";
            string? previousToken = null;
            bool need_init = true;
            bool stop = false;
            string pendingBuffer = "";
            if (Setting.UseStreaming)
            {
                string temp = "";
                int maxPromptLen = 0;
                if (inferenceParams.AntiPrompts != null && inferenceParams.AntiPrompts.Count > 0)
                {
                    maxPromptLen = inferenceParams.AntiPrompts.Max(p => p?.Length ?? 0);
                }
                await foreach (var token in
                    session.ChatAsync(new ChatHistory.Message(AuthorRole.User, input),
                    inferenceParams,
                 cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }

                    if (need_init)
                    {
                        need_init = false;
                        previousToken = token;
                        continue;
                    }

                    // ========= 將上一個 token 放到暫存區 pendingBuffer =========
                    pendingBuffer += previousToken;

                    // ========= 檢查暫存區裡是否已經拼出任一個 AntiPrompt =========
                    if (inferenceParams.AntiPrompts != null)
                    {
                        foreach (var antiprompt in inferenceParams.AntiPrompts)
                        {
                            if (string.IsNullOrEmpty(antiprompt))
                                continue;

                            int idx = pendingBuffer.IndexOf(antiprompt, StringComparison.Ordinal);
                            if (idx >= 0)
                            {
                                // 找到完整的 AntiPrompt 了：把拼在它之前的內容寫到 outputBuffer，然後停掉
                                outputBuffer += pendingBuffer.Substring(0, idx);
                                stop = true;
                                break;
                            }
                            //if (pendingBuffer.EndsWith(antiprompt))
                            //{
                            //    outputBuffer = outputBuffer.Substring(0, outputBuffer.Length - antiprompt.Length);
                            //    stop = true;
                            //    break;
                            //}
                        }
                    }

                    if (stop)
                    {
                        // 一旦命中，就跳出最外層迴圈，不要再處理後續 token
                        break;
                    }

                    // ========= 如果暫存區長度超過 maxPromptLen，就把最早那部分 safeFlush 到 outputBuffer =========
                    if (pendingBuffer.Length > maxPromptLen)
                    {
                        int safeFlushLen = pendingBuffer.Length - maxPromptLen;
                        temp = pendingBuffer.Substring(0, safeFlushLen);
                        outputBuffer += temp;
                        pendingBuffer = pendingBuffer.Substring(safeFlushLen);
                    }

                    // ========= 更新 previousToken，準備下一輪 =========
                    previousToken = token;

                    yield return new ChatResponse
                    {
                        Messages = outputBuffer,
                        Delta = temp
                    };
                }

                // ========== 迴圈結束後，如果沒有因為 AntiPrompt 而停止，就把剩下的都 flush 出去 =========
                if (!stop)
                {
                    if (previousToken != null)
                    {
                        pendingBuffer += previousToken;
                    }
                    // 把整個 pendingBuffer 清空到 outputBuffer
                    outputBuffer += pendingBuffer;


                    // 最後再做一次 Sanitize（或 Trim），確保沒有遺留不必要的字元
                    var finalText = SanitizeOutput(outputBuffer);
                    if (!string.IsNullOrEmpty(finalText))
                    {
                        yield return new ChatResponse
                        {
                            Messages = finalText,
                            Delta = finalText
                        };
                    }
                }
            }
            else
            {
                await foreach (var token in
                    session.ChatAsync(new ChatHistory.Message(AuthorRole.User, input),
                    inferenceParams,
                 cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    outputBuffer += token;
                    foreach (var antiprompt in inferenceParams.AntiPrompts)
                    {
                        if (string.IsNullOrEmpty(antiprompt))
                            continue;

                        int idx = outputBuffer.IndexOf(antiprompt, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            // 一旦對到，就把 outputBuffer 截斷到 prompt 出現之前
                            outputBuffer = outputBuffer.Substring(0, idx);
                            stop = true;
                            break;
                        }
                        //if (pendingBuffer.EndsWith(antiprompt))
                        //{
                        //    outputBuffer = outputBuffer.Substring(0, outputBuffer.Length - antiprompt.Length);
                        //    stop = true;
                        //    break;
                        //}
                    }
                    if (stop)
                    {
                        break;
                    }
                }
                outputBuffer = SanitizeOutput(outputBuffer);

                yield return new ChatResponse
                {
                    Delta = outputBuffer,
                    Messages = outputBuffer
                };
            }

            context.Dispose();
        }
        private string SanitizeOutput(string res)
        {
            if (inferenceParams.AntiPrompts != null)
            {
                foreach (var prompt in inferenceParams.AntiPrompts)
                {
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        res = res.Replace(prompt, string.Empty);
                    }
                }
            }
            return res;
        }

        public void Dispose()
        {
            //try
            //{
            //    context?.Dispose();
            //}
            //catch (ObjectDisposedException)
            //{
            //    // 已經被釋放，忽略
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Dispose context failed: {ex.Message}");
            //}

            try
            {
                model?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // 已經被釋放，忽略
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispose model failed: {ex.Message}");
            }
        }
    }
}
