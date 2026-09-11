using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIClient.Core;
using RevitAIClient.UI;
using System;
using System.Windows.Interop;

namespace RevitAIClient.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MainCommand : IExternalCommand
    {
        private static ChatWindow _chatWindow;
        
        // Статический глобальный обработчик задач для вызова Revit API из фоновых потоков
        public static RevitTaskHandler TaskHandler { get; private set; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Инициализация TaskHandler при первом запуске команды
                if (TaskHandler == null)
                {
                    TaskHandler = new RevitTaskHandler();
                    TaskHandler.Initialize();
                }

                if (_chatWindow == null || !_chatWindow.IsLoaded)
                {
                    _chatWindow = new ChatWindow();
                    
                    // Привязка окна к главному окну Revit
                    IntPtr revitWindowHandle = commandData.Application.MainWindowHandle;
                    WindowInteropHelper helper = new WindowInteropHelper(_chatWindow);
                    helper.Owner = revitWindowHandle;

                    _chatWindow.Closed += (s, e) => _chatWindow = null;
                    
                    _chatWindow.Show();
                }
                else
                {
                    if (_chatWindow.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _chatWindow.WindowState = System.Windows.WindowState.Normal;
                    }
                    _chatWindow.Activate();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}