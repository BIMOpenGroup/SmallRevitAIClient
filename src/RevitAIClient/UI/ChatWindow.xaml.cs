using RevitAIClient.Commands;
using System;
using System.Windows;

namespace RevitAIClient.UI
{
    public partial class ChatWindow : Window
    {
        public ChatWindow()
        {
            InitializeComponent();
        }

        private async void TestApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Запускаем асинхронную задачу в главном потоке Revit
                string docInfo = await MainCommand.TaskHandler.ExecuteAsync(app =>
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null)
                        return "Нет активного документа.";
                        
                    return $"Документ: {doc.Title}\nПуть: {doc.PathName}";
                });

                MessageBox.Show(docInfo, "Revit API Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка вызова Revit API:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}