using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAIClient.LLM
{
    public interface ILLMProvider
    {
        /// <summary>
        /// Отправляет историю сообщений в LLM и возвращает ответ в виде потока токенов (через callback).
        /// Возвращает список вызовов функций (если LLM решила использовать инструменты).
        /// </summary>
        /// <param name="history">История переписки</param>
        /// <param name="tools">Доступные инструменты</param>
        /// <param name="onTokenReceived">Делегат, вызываемый при получении новой порции текста</param>
        /// <param name="cancellationToken">Токен отмены</param>
        Task<List<ToolCall>> SendMessageStreamAsync(List<ChatMessage> history, List<ToolSchema> tools, Action<string> onTokenReceived, CancellationToken cancellationToken);
    }
}