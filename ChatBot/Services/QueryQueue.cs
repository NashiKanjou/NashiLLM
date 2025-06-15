using ChatBot.Binding;
using ChatBot.Database.Interfaces;
using ChatBot.Session;
using ChatBot.Utils.LLM;
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
    internal class QueryQueue
    {
        private readonly ConcurrentQueue<(ArsChatSession, string, string, ChatHistory, List<(string, double)>)> queue;

        private readonly List<ChatModel> _modelPool;
        private readonly object _modelLock = new();
        private int _nextModelIndex;
        private readonly SemaphoreSlim _queueSignal;
        private CancellationTokenSource _cts;
        private ChatModelSetting _modelSetting;
        private string query_respond = string.Empty;
        private LLamaWeights weights;
        public QueryQueue(ChatModel model) : this()
        {
            this.addModelPool(model);
        }
        public QueryQueue()
        {
            //this.model = model;
            _nextModelIndex = 0;
            _modelPool = new List<ChatModel>() { };
            _queueSignal = new(0);
            //_semaphore = new SemaphoreSlim(1, 1);
            queue = new ConcurrentQueue<(ArsChatSession, string, string, ChatHistory, List<(string, double)>)>();
            //running = false;
            _cts = new CancellationTokenSource();
        }

        public void Stop()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
        }
        public void Run(int workers)
        {
            //running = true;
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
            _modelSetting = model.Setting;
            query_respond = "\n" + _modelSetting.AssistantFormat;
            weights = model.weights;
            _modelPool.Add(model);
            return true;
        }

        public bool Queue(ArsChatSession session, string query, string summary, string document, List<(string, double)> sources, IHistoryDatabase historyDB)
        {
            ChatHistory chatHistory = createChatHistory(summary, query, document, historyDB);

            Enqueue(session, query, summary, chatHistory, sources);
            return true;
        }
        private void Enqueue(ArsChatSession session, string query, string history, ChatHistory chatHistory, List<(string, double)> sources)
        {
            queue.Enqueue((session, query, history, chatHistory, sources));
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

                        string answer = "";
                        string query = _modelSetting.UserFormat.Replace(PromptParams.Query, data.Item2);
                        await foreach (ChatResponse chunk in model.GenerateAsync(query, query_respond, data.Item4))
                        {
                            answer += chunk.Delta;
                            data.Item1.ProcessParticleResult(chunk.Delta, model.Setting.UseStreaming);
                        }
                        data.Item1.ProcessResult(data.Item2, answer, data.Item5, model.Setting.UseStreaming);
                        if (data.Item4.Messages.Count >= 2)
                        {
                            int count = data.Item4.Messages.Count;
                            data.Item4.Messages.RemoveAt(count - 1); // 移除最後一筆
                            data.Item4.Messages.RemoveAt(count - 2); // 移除倒數第二筆（要在最後一筆移除「之後」做）
                        }
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                var result = data.Item1.SummaryQuery(data.Item2, answer, data.Item4);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"背景處理失敗：{ex.Message}");
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("背景 worker 已取消");
                    break;
                }
            }
        }

        private ChatHistory createChatHistory(string summary, string query, string document, IHistoryDatabase historyDB)
        {
            var chatHistory = new ChatHistory();
            string prompt = _modelSetting.SystemPrompt.Replace(PromptParams.Summary, summary).Replace(PromptParams.Document, document);
            chatHistory.AddMessage(AuthorRole.System, prompt);
            long tokenLimit = _modelSetting.ContextSize - _modelSetting.MaxTokens;//(_modelSetting.ContextSize / 2 > int.MaxValue) ? int.MaxValue : (int)(_modelSetting.ContextSize / 2);
            long currentTokens = Utils.LLM.Utils.CountTokens(weights, prompt);
            currentTokens += Utils.LLM.Utils.CountTokens(weights, query);

            //Load history
            var history = historyDB.getHistory();
            var tempHistory = new List<(AuthorRole role, string content)>();

            foreach (var item in history)
            {
                tempHistory.Add((AuthorRole.User, item.Query));
                tempHistory.Add((AuthorRole.Assistant, item.Respond));
            }
            tempHistory.Reverse();
            var finalHistory = new List<(AuthorRole role, string content)>();

            foreach (var (role, content) in tempHistory)
            {
                string str = string.Empty;
                if (role == AuthorRole.System)
                {
                    continue;
                }

                int tokens = Utils.LLM.Utils.CountTokens(weights, content);
                if (currentTokens + tokens > tokenLimit)
                    break;

                finalHistory.Insert(0, (role, content));
                currentTokens += tokens;
            }

            foreach (var (role, content) in finalHistory)
            {
                string str = string.Empty;
                switch (role)
                {
                    case AuthorRole.System:
                        continue;
                    case AuthorRole.Assistant:
                        str = _modelSetting.AssistantFormat.Replace(PromptParams.Respond, content);
                        break;
                    case AuthorRole.User:
                        str = _modelSetting.UserFormat.Replace(PromptParams.Query, content);
                        break;
                }
                chatHistory.AddMessage(role, str);
            }
            //chatHistory.AddMessage(AuthorRole.Assistant, document_str);
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
