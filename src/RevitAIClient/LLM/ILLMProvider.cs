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
        /// </summary>
        /// <param name="history">История переписки</param>
        /// <param name="onTokenReceived">Делегат, вызываемый при получении новой порции текста</param>
        /// <param name="cancellationToken">Токен отмены</param>
        Task SendMessageStreamAsync(List<ChatMessage> history, Action<string> onTokenReceived, CancellationToken cancellationToken);
    }
}