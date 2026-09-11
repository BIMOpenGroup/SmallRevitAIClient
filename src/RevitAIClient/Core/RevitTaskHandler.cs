using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace RevitAIClient.Core
{
    /// <summary>
    /// Обработчик внешних событий для проброса задач из фоновых потоков (WPF/LLM) в главный поток Revit.
    /// </summary>
    public class RevitTaskHandler : IExternalEventHandler
    {
        // Очередь делегатов, принимающих UIApplication и возвращающих некий результат.
        private readonly ConcurrentQueue<Action<UIApplication>> _tasks = new ConcurrentQueue<Action<UIApplication>>();
        private ExternalEvent _externalEvent;

        /// <summary>
        /// Инициализация обработчика. Должна вызываться из главного потока (например, в IExternalCommand.Execute).
        /// </summary>
        public void Initialize()
        {
            if (_externalEvent == null)
            {
                _externalEvent = ExternalEvent.Create(this);
            }
        }

        /// <summary>
        /// Асинхронное выполнение функции в главном потоке Revit.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения</typeparam>
        /// <param name="func">Функция, работающая с Revit API</param>
        /// <returns>Task, ожидающий завершения в главном потоке</returns>
        public Task<T> ExecuteAsync<T>(Func<UIApplication, T> func)
        {
            if (_externalEvent == null)
            {
                throw new InvalidOperationException("RevitTaskHandler is not initialized. Call Initialize() first.");
            }

            var tcs = new TaskCompletionSource<T>();

            _tasks.Enqueue(app =>
            {
                try
                {
                    // Выполнение переданной функции в контексте главного потока
                    T result = func(app);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            // Сигнализируем Revit, что есть задачи для выполнения
            _externalEvent.Raise();

            return tcs.Task;
        }

        /// <summary>
        /// Метод, вызываемый самим Revit в его главном потоке.
        /// </summary>
        public void Execute(UIApplication app)
        {
            // Извлекаем все задачи из очереди и выполняем их
            while (_tasks.TryDequeue(out var action))
            {
                action(app);
            }
        }

        public string GetName()
        {
            return "Revit AI Client Task Handler";
        }
    }
}