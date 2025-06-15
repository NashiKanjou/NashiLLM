using ChatBot.Binding;
using ChatBot.Session;
using ChatBot.Utils.LLM;
using DocumentFormat.OpenXml.Drawing.Charts;
using LLama;
using LLama.Common;
using System.Collections.Concurrent;
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
    internal class SummaryQueue
    {
        private readonly ConcurrentQueue<(ArsChatSession, ChatHistory chatHistory)> queue;

        private readonly List<ChatModel> _modelPool;
        private readonly object _modelLock = new();
        private int _nextModelIndex;
        private readonly SemaphoreSlim _queueSignal;
        private CancellationTokenSource _cts;
        private static string summary_prompt = string.Empty;
        private static string summary_query = string.Empty;
        private static string query_respond = string.Empty;
        private static string format_user = string.Empty;
        private static string format_assistant = string.Empty;
        private static uint ContextSize;
        private static LLamaWeights weights;
        public SummaryQueue(ChatModel model) : this()
        {
            this.addModelPool(model);
        }
        public SummaryQueue()
        {
            _nextModelIndex = 0;
            _modelPool = new List<ChatModel>() { };
            _queueSignal = new(0);
            queue = new ConcurrentQueue<(ArsChatSession, ChatHistory)>();
            _cts = new CancellationTokenSource();
        }

        public void Stop()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
        }

        public void Run(int workers)
        {
            for (int i = 0; i < workers; i++)
            {
                _ = Task.Run(() => DequeueWorkerAsync(_cts.Token));
            }
        }

        public bool addModelPool(ChatModel model)
        {
            if (_modelPool.Contains(model))
            {
                return false;
            }
            _modelPool.Add(model);
            summary_prompt = model.Setting.SummaryPrompt;
            summary_query = model.Setting.SummaryQuery;
            query_respond = "\n" + model.Setting.AssistantFormat;
            format_assistant = model.Setting.AssistantFormat;
            format_user = model.Setting.UserFormat;
            ContextSize = model.Setting.ContextSize;
            weights = model.weights;
            return true;
        }

        public bool Queue(ArsChatSession session, string summary, string query, string respond, ChatHistory old_chatHistory)
        {
            ChatHistory chatHistory = createChatHistory(summary, query, respond, old_chatHistory);
            Enqueue(session, summary, query, respond, chatHistory);
            return true;
        }
        private void Enqueue(ArsChatSession session, string summary, string query, string respond, ChatHistory chatHistory)
        {
            queue.Enqueue((session, chatHistory));
            _queueSignal.Release(); // 通知工作者有新任務
        }

        private async Task DequeueWorkerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _queueSignal.WaitAsync(token);
                    if (queue.TryDequeue(out var data))
                    {
                        var model = GetNextModel();
                        string newSummary = string.Empty;
                        await foreach (ChatResponse chunk in model.GenerateAsync(summary_query, query_respond, data.Item2))
                        {
                            newSummary += chunk.Delta;
                        }
                        data.Item1.UpdateSummary(newSummary);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("背景 worker 已取消");
                    break;
                }
            }
        }

        private ChatHistory createChatHistory(string summary, string query, string respond, ChatHistory old_chatHistory)
        {
            var chatHistory = new ChatHistory();
            string prompt = summary_prompt.Replace(PromptParams.Summary, summary);
            chatHistory.AddMessage(AuthorRole.System, prompt);
            int maxTokens = (ContextSize / 2 > int.MaxValue) ? int.MaxValue : (int)(ContextSize / 2);
            int currentTokens = Utils.LLM.Utils.CountTokens(weights, prompt);
            currentTokens += Utils.LLM.Utils.CountTokens(weights, query);
            currentTokens += Utils.LLM.Utils.CountTokens(weights, respond);

            //Load history
            List<ChatHistory.Message> history = old_chatHistory.Messages;

            var tempHistory = new List<(AuthorRole role, string content)>();

            foreach (var item in history)
            {
                if (item.AuthorRole == AuthorRole.System)
                {
                    continue;
                }
                tempHistory.Add((item.AuthorRole, item.Content));
            }
            tempHistory.Reverse();
            var finalHistory = new List<(AuthorRole role, string content)>();

            foreach (var (role, content) in tempHistory)
            {
                int tokens = Utils.LLM.Utils.CountTokens(weights, content);
                if (currentTokens + tokens > maxTokens)
                    break;

                finalHistory.Insert(0, (role, content));
                currentTokens += tokens;
            }

            foreach (var (role, content) in finalHistory)
            {
                chatHistory.AddMessage(role, content);
            }
            chatHistory.AddMessage(AuthorRole.User, format_user.Replace(PromptParams.Query, query));
            chatHistory.AddMessage(AuthorRole.Assistant, format_assistant.Replace(PromptParams.Respond, respond));
            return chatHistory;
        }

        #region LoadBalance
        private ChatModel GetNextModel()
        {
            lock (_modelLock)
            {
                var model = _modelPool[_nextModelIndex];
                _nextModelIndex = (_nextModelIndex + 1) % _modelPool.Count;
                return model;
            }
        }
        #endregion
    }
}
