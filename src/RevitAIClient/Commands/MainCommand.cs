using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIClient.UI;
using System;
using System.Windows.Interop;

namespace RevitAIClient.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MainCommand : IExternalCommand
    {
        // Статическое поле для хранения единственного экземпляра окна
        private static ChatWindow _chatWindow;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_chatWindow == null || !_chatWindow.IsLoaded)
                {
                    _chatWindow = new ChatWindow();
                    
                    // Привязка окна к главному окну Revit (чтобы окно не терялось и сворачивалось вместе с Revit)
                    IntPtr revitWindowHandle = commandData.Application.MainWindowHandle;
                    WindowInteropHelper helper = new WindowInteropHelper(_chatWindow);
                    helper.Owner = revitWindowHandle;

                    // Очистка ссылки при закрытии окна пользователем
                    _chatWindow.Closed += (s, e) => _chatWindow = null;
                    
                    // Открытие окна в немодальном режиме
                    _chatWindow.Show();
                }
                else
                {
                    // Если окно уже открыто, просто выводим его на передний план
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