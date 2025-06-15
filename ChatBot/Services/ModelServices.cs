using ChatBot.Session;
using YamlDotNet.Serialization;
using ChatBot.Binding;
using ChatBot.Utils;
using LLama.Native;
using Binding;
using LLama.Common;
using LLama;
using ChatBot.Database.Interfaces;
using LLama.Sampling;

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
namespace ChatBot.Services
{
    public static class ModelServices
    {
        private static Dictionary<string, QueryQueue> queues = new Dictionary<string, QueryQueue>();
        private static SummaryQueue? summaryQueue;
        private static string default_model = string.Empty;
        internal static void Init()
        {
            queues.Clear();
            try
            {
                string summaryModelPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "summary_model";
                string globalSettingPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "GlobalData" + Path.DirectorySeparatorChar + "Setting";
                string yml = string.Join(Environment.NewLine, File.ReadAllLines(globalSettingPath));
                var deserializer = new DeserializerBuilder().Build();
                GlobalSetting globalSettingparam = deserializer.Deserialize<GlobalSetting>(yml);
                default_model = globalSettingparam.defaultModel;

                #region Query Models
                string path_models = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "models";

                string[] subFolders = Directory.GetDirectories(path_models, "*", SearchOption.TopDirectoryOnly);

                foreach (string modelPath in subFolders)
                {
                    try
                    {
                        string param_path = modelPath + Path.DirectorySeparatorChar + "param";
                        yml = string.Join(Environment.NewLine, File.ReadAllLines(param_path));
                        ModelParam param = deserializer.Deserialize<ModelParam>(yml);

                        string model_name = param.ModelName;
                        Console.WriteLine($"載入模型中: {model_name}");

                        queues[model_name] = new QueryQueue();

                        string anti_prompts_path = modelPath + Path.DirectorySeparatorChar + "anti_prompts";
                        List<string> anti_prompt = File.ReadAllLines(anti_prompts_path).ToList();

                        model_name = param.ModelName;

                        ModelParams parameters = new ModelParams(modelPath)
                        {
                            ContextSize = (uint?)param.ContextSize,
                            GpuLayerCount = param.GpuLayerCount,
                            BatchSize = param.BatchSize,
                            FlashAttention = param.FlashAttention,
                            MainGpu = param.MainGpu,
                            ModelPath = modelPath + Path.DirectorySeparatorChar + param.FileToLoad,
                            UseMemorymap = param.UseMemoryMap
                        };

                        var Setting = new ChatModelSetting();
                        Setting.MaxTokens = param.MaxTokens;
                        Setting.ContextSize = (uint)param.ContextSize;
                        Setting.UseStreaming = param.UseStreaming;
                        Setting.SystemPrompt = File.ReadAllText(modelPath + Path.DirectorySeparatorChar + "prompt");
                        Setting.UserFormat = File.ReadAllText(modelPath + Path.DirectorySeparatorChar + "format_user");
                        Setting.AssistantFormat = File.ReadAllText(modelPath + Path.DirectorySeparatorChar + "format_assistant");
                        var inferenceParams = new InferenceParams()
                        {
                            MaxTokens = param.MaxTokens,
                            AntiPrompts = anti_prompt,

                            SamplingPipeline = new DefaultSamplingPipeline
                            {
                                RepeatPenalty = param.RepeatPenalty,
                                Temperature = param.Temperature,
                                TopP = param.TopP
                            },
                        };
                        var weight = LLamaWeights.LoadFromFile(parameters);
                        NativeLogConfig.llama_log_set(new FileLogger("llamacpp_log.txt"));


                        for (int i = 0; i < param.Workers; i++)
                        {

                            ChatModel model = new ChatModel(weight, parameters, inferenceParams, Setting);
                            queues[model_name].addModelPool(model);
                        }
                        queues[model_name].Run(param.Workers);
                        Console.WriteLine($"模型載入完成: {model_name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"讀取失敗: {ex.Message}");
                    }
                }
                #endregion

                #region SummaryModel
                try
                {

                    yml = string.Join(Environment.NewLine, File.ReadAllLines(summaryModelPath + Path.DirectorySeparatorChar + "param"));
                    ModelParam summaryParam = deserializer.Deserialize<ModelParam>(yml);

                    summaryQueue = new SummaryQueue();

                    List<string> summary_anti_prompt = File.ReadAllLines(summaryModelPath + Path.DirectorySeparatorChar + "anti_prompts").ToList();


                    ModelParams summaryModel_params = new ModelParams(summaryModelPath)
                    {
                        ContextSize = (uint?)summaryParam.ContextSize,
                        GpuLayerCount = summaryParam.GpuLayerCount,
                        BatchSize = summaryParam.BatchSize,
                        FlashAttention = summaryParam.FlashAttention,
                        MainGpu = summaryParam.MainGpu,
                        ModelPath = summaryModelPath + Path.DirectorySeparatorChar + summaryParam.FileToLoad,
                        UseMemorymap = summaryParam.UseMemoryMap
                    };

                    var Setting = new ChatModelSetting();
                    Setting.MaxTokens = summaryParam.MaxTokens;
                    Setting.ContextSize = (uint)summaryParam.ContextSize;
                    Setting.UseStreaming = summaryParam.UseStreaming;
                    Setting.SummaryPrompt = File.ReadAllText(summaryModelPath + Path.DirectorySeparatorChar + "prompt");
                    Setting.SummaryQuery = File.ReadAllText(summaryModelPath + Path.DirectorySeparatorChar + "query");
                    Setting.UserFormat = File.ReadAllText(summaryModelPath + Path.DirectorySeparatorChar + "format_user");
                    Setting.AssistantFormat = File.ReadAllText(summaryModelPath + Path.DirectorySeparatorChar + "format_assistant");
                    var inferenceParams = new InferenceParams()
                    {
                        MaxTokens = summaryParam.MaxTokens,
                        AntiPrompts = summary_anti_prompt,

                        SamplingPipeline = new DefaultSamplingPipeline
                        {
                            RepeatPenalty = summaryParam.RepeatPenalty,
                            Temperature = summaryParam.Temperature,
                            TopP = summaryParam.TopP
                        },
                    };
                    var summary_weight = LLamaWeights.LoadFromFile(summaryModel_params);
                    NativeLogConfig.llama_log_set(new FileLogger("llamacpp_log.txt"));

                    for (int i = 0; i < summaryParam.Workers; i++)
                    {
                        ChatModel model = new ChatModel(summary_weight, summaryModel_params, inferenceParams, Setting);
                        summaryQueue.addModelPool(model);
                    }


                    summaryQueue.Run(summaryParam.Workers);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"讀取失敗: {ex.Message}");
                }
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine($"發生錯誤: {ex.Message}");
            }
        }
        public static List<string> getAllLoadedModel()
        {
            if (queues == null)
            {
                return new List<string>();
            }
            return queues.Keys.ToList();
        }

        internal static string getModelName(string model)
        {
            if (model == "default")
            {
                model = default_model;
            }
            return model;
        }

        internal static ModelResult SummaryQueue(ArsChatSession session, string summary, string query, string respond, ChatHistory old_chatHistory)
        {
            if (summaryQueue.Queue(session, summary, query, respond, old_chatHistory))
            {
                return ModelResult.Success;
            }
            return ModelResult.Error;
        }

        internal static ModelResult Queue(ArsChatSession session, string modelname, string query, string summary, string document, List<(string, double)> soureces, IHistoryDatabase historyDB)
        {
            if (modelname == "default")
            {
                modelname = default_model;
            }
            if (!queues.ContainsKey(modelname))
            {
                return ModelResult.ModelNotFound;
            }
            if (queues[modelname].Queue(session, query, summary, document, soureces, historyDB))
            {
                return ModelResult.Success;
            }
            return ModelResult.Error;
        }

        public enum ModelResult
        {
            Success = 0, ModelNotFound = -1, Error = -2
        }

    }
}
