using ChatBot.Services;
using ChatBot.Session;
using ChatBot.Utils;
using LLama.Native;
using static ChatBot.Services.ModelServices;
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
namespace Nashi
{
    public class Program
    {
        static void Main(string[] args)
        {
            NativeLibraryConfig.All
                .WithCuda()
                .SkipCheck(true)
                .WithAutoFallback(false);
            //.WithLogCallback((level, message) => Console.Write($"{level}: {message}"));
            NativeApi.llama_empty_call();
            #region Console Setting
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            #endregion

            #region Delete old log
            string logfilePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "llamacpp_log.txt";
            if (File.Exists(logfilePath))
            {
                File.Delete(logfilePath);
            }
            #endregion

            #region Initialize
            NativeLogConfig.llama_log_set(new FileLogger("llamacpp_log.txt"));
            UtilsLoader.Init();
            ModelServices.Init();
            ChatService chatService = new ChatService();
            #endregion

            #region Load Global Document
            Console.WriteLine("Loading Global Documents...");
            chatService.InsertDocumentToDB(false);
            #endregion

            #region User Login

            #region Account
            Console.WriteLine("請輸入Token (不輸入以使用預設token)：");
            string? token = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(token))
            {
                token = "sample_token";
            }
            #endregion

            #region Session
            Console.WriteLine("請輸入SessionId (不輸入進行創立)：");
            string? id = Console.ReadLine();
            ArsChatSession session;
            if (string.IsNullOrWhiteSpace(id))
            {
                session = chatService.CreateSession(token);
            }
            else
            {
                session = chatService.LoadSession(token, id);
            }
            #endregion

            #region Load Account Document
            Console.WriteLine("Loading Account Documents...");
            session.InsertDocumentToDB(false);
            #endregion

            #endregion

            #region User Input
            Console.WriteLine("請輸入內容（輸入 'exit' 結束）：");
            string? input;
            while (true)
            {
                input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }
                if (input.Trim().ToLower() == "exit")
                    break;

                ModelResult result = session.Query(input);

            }
            #endregion

            #region End Service
            chatService.CloseService();
            #endregion
        }

    }
}
