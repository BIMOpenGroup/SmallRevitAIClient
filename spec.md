# Спецификация: Интеграция AI-ассистента в Revit (Client Mode)

## 1. Архитектура решения
Плагин реализуется как надстройка над Revit API (IExternalCommand) с интеграцией WPF-интерфейса через немодальное окно, запускаемое по запросу.
*   **UI Layer (WPF):** Чат-интерфейс, настройки провайдера, управление контекстом.
*   **Core Controller:** Оркестратор взаимодействия между UI, LLM и Revit API.
*   **LLM Integration Layer:** Адаптеры для API (OpenAI, DeepSeek, локальные модели) с поддержкой SSE (Server-Sent Events) и Function Calling.
*   **Skill Registry:** Модульная система инструментов (DRY-подход), регистрирующая доступные команды для LLM.
*   **Revit Execution Engine:** Безопасный вызов Revit API через `IExternalEventHandler` для обхода ограничений однопоточности.

## 2. Базовые интерфейсы (C#)

### 2.1. LLM Провайдер
Унифицированный интерфейс для работы с различными нейросетями.
```csharp
public interface ILLMProvider
{
    // Отправка сообщения с поддержкой стриминга
    IAsyncEnumerable<string> SendMessageStreamAsync(List<ChatMessage> history, List<ToolSchema> availableTools);
    
    // Выполнение вызова функции (Tool Call), запрошенного LLM
    Task<ToolCallResult> HandleToolCallAsync(ToolCallRequest request);
}
```

### 2.2. Система навыков (Skills)
Каждый навык реализует единый интерфейс. Генерация JSON-схемы для LLM происходит автоматически через Reflection.
```csharp
public interface IRevitSkill
{
    string Name { get; }
    string Description { get; }
    
    // Возвращает JSON Schema параметров для LLM
    string GetSchema(); 
    
    // Выполнение навыка. Должно быть потокобезопасным (ExternalEvent)
    Task<string> ExecuteAsync(string argumentsJson);
    
    // Требует ли навык подтверждения пользователя (Human-in-the-loop)
    bool RequiresConfirmation { get; } 
}
```

## 3. Механизм потокобезопасности (Thread Bridging)
Запросы к REST API LLM выполняются асинхронно. Доступ к Revit API возможен **только** из главного потока.
*   Реализуется единый `IExternalEventHandler` (назовем его `RevitTaskHandler`).
*   Инструменты (`IRevitSkill`), требующие обращения к `Document`, ставят делегаты в очередь `RevitTaskHandler`.
*   Используется `TaskCompletionSource<T>` для ожидания завершения транзакции в главном потоке и возврата результата в асинхронный поток LLM.

## 4. Жизненный цикл обработки запроса (Data Flow)
1.  **User Input:** Пользователь пишет запрос в Dockable Pane.
2.  **To LLM:** Контроллер формирует контекст (история + доступные инструменты) и отправляет асинхронный запрос в LLM.
3.  **Stream UI:** Текстовый ответ стримится в UI.
4.  **Tool Call:** Если LLM решает использовать инструмент, стриминг приостанавливается, контроллер парсит `tool_call`.
5.  **Confirmation (Опционально):** Если `RequiresConfirmation == true`, в UI выводится запрос (Approve/Reject).
6.  **Execution:** Контроллер вызывает `IRevitSkill.ExecuteAsync()`, который через `ExternalEvent` обращается к Revit API (внутри `Transaction`, если есть изменения).
7.  **Callback to LLM:** Результат выполнения возвращается в LLM, цикл повторяется до окончательного текстового ответа.

## 5. Требования к UI и сборке
*   **WPF:** Использование паттерна MVVM. Все ресурсы (иконки, стили) инлайнятся в сборку (Build Action: Resource) для удобства дистрибуции.
*   **Логирование:** Лаконичный вывод ошибок в панель чата и в локальный лог-файл (формат: дата/время UTC+4, уровень, сообщение).