# Дорожная карта реализации: Revit AI Client

## План работ (Gantt Chart)

```mermaid
gantt
    title Revit AI Client: Поэтапная реализация
    dateFormat  YYYY-MM-DD
    axisFormat  %d.%m
    
    section Шаг 1: Базовый плагин и UI
    Настройка проекта и манифеста (.addin)      :step1_1, 2026-09-12, 1d
    Реализация IExternalCommand                 :step1_2, after step1_1, 1d
    Разработка WPF окна (немодального, MVVM)    :step1_3, after step1_2, 2d
    
    section Шаг 2: Мост потоков (Thread Bridge)
    Реализация IExternalEventHandler            :step2_1, after step1_3, 2d
    Система TaskCompletionSource для Event      :step2_2, after step2_1, 1d
    
    section Шаг 3: LLM Провайдер
    Интерфейс ILLMProvider и базовые модели     :step3_1, after step2_2, 1d
    Реализация HTTP-клиента (SSE Stream)        :step3_2, after step3_1, 2d
    Обработка истории и токенов                 :step3_3, after step3_2, 1d
    
    section Шаг 4: Система Skills (Function Calling)
    Интерфейс IRevitSkill и Reflection (JSON)   :step4_1, after step3_3, 2d
    Разработка Read-Only навыка (Get Elements)  :step4_2, after step4_1, 1d
    Разработка Write-навыка (Update Param)      :step4_3, after step4_2, 1d
    Механизм подтверждения (Human-in-the-loop)  :step4_4, after step4_3, 1d
    
    section Шаг 5: Интеграция и тестирование
    Оркестратор: парсинг tool_calls от LLM      :step5_1, after step4_4, 2d
    Сборка, инлайнинг ресурсов, логирование     :step5_2, after step5_1, 1d
    Финальное тестирование на стенде            :step5_3, after step5_2, 1d
```

## Пошаговый план для оператора

- [ ] **Шаг 1: Базовый каркас.** Создание `.csproj`, манифеста типа Command, реализация команды и пустого немодального WPF-окна. *(Проверка: команда запускается через Add-In Manager, немодальное окно открывается и не блокирует Revit).*
- [ ] **Шаг 2: Потокобезопасность.** Реализация и тестирование `IExternalEventHandler`. *(Проверка: тестовая кнопка в WPF асинхронно вызывает чтение ID элемента из Revit без блокировки UI).*
- [ ] **Шаг 3: Интеграция LLM.** Подключение к API (например, DeepSeek/OpenAI), реализация чата. *(Проверка: чат стримит текстовые ответы).*
- [ ] **Шаг 4: Инфраструктура Skills.** Регистрация базового навыка (например, `GetActiveViewInfo`), передача схемы в LLM. *(Проверка: LLM вызывает навык, ловит ответ, выводит результат в чат).*
- [ ] **Шаг 5: Безопасность и запись.** Добавление навыка изменения модели с запросом подтверждения. *(Проверка: LLM пытается изменить параметр, UI запрашивает Approve, после подтверждения транзакция выполняется).*