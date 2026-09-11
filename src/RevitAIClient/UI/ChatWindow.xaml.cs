using RevitAIClient.Commands;
using RevitAIClient.LLM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitAIClient.UI
{
    public partial class ChatWindow : Window
    {
        private List<ChatMessage> _history = new List<ChatMessage>();
        private CancellationTokenSource _cts;

        public ChatWindow()
        {
            InitializeComponent();
            
            // Системный промпт (пока базовый)
            _history.Add(new ChatMessage { role = "system", content = "You are a helpful Revit AI Assistant. Be concise and precise." });
            
            LoadApiKey();
        }

        private string GetConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "RevitAIClient");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "apikey.txt");
        }

        private void LoadApiKey()
        {
            try
            {
                var path = GetConfigPath();
                if (File.Exists(path))
                {
                    ApiKeyBox.Password = File.ReadAllText(path).Trim();
                }
            }
            catch { }
        }

        private void SaveApiKey(string key)
        {
            try
            {
                File.WriteAllText(GetConfigPath(), key);
            }
            catch { }
        }

        private void LogDebug(string message)
        {
#if DEBUG
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI("System [DEBUG]", message, Brushes.LightYellow);
            });
#endif
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string userText = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            string apiKey = ApiKeyBox.Password;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter an API Key first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Сохраняем ключ при успешной отправке
            SaveApiKey(apiKey);

            _history.Add(new ChatMessage { role = "user", content = userText });
            AddMessageToUI("User", userText, Brushes.LightBlue);
            
            InputBox.Text = string.Empty;
            InputBox.IsEnabled = false;

            LogDebug($"Отправка запроса на {EndpointBox.Text} с моделью {ModelBox.Text}...");

            var assistantBlock = AddMessageToUI("Assistant", "...", Brushes.LightGray);
            var assistantContent = string.Empty;

            bool useThinking = UseThinkingCheck.IsChecked ?? false;
            string effort = (EffortCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "high";

            _cts = new CancellationTokenSource();
            var provider = new OpenAIProvider(apiKey, EndpointBox.Text, ModelBox.Text, useThinking, effort);
            
            // Подписываемся на дебаг-события провайдера
            provider.OnDebugLog += LogDebug;

            try
            {
                assistantBlock.Text = string.Empty;
                
                await provider.SendMessageStreamAsync(_history, token =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        assistantBlock.Text += token;
                        assistantContent += token;
                        ChatScroll.ScrollToBottom();
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }, _cts.Token);

                _history.Add(new ChatMessage { role = "assistant", content = assistantContent });
                LogDebug("Ответ успешно получен и поток закрыт.");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    assistantBlock.Text += $"\n[Error: {ex.Message}]";
                });
                LogDebug($"Исключение: {ex.ToString()}");
            }
            finally
            {
                provider.OnDebugLog -= LogDebug;
                InputBox.IsEnabled = true;
                InputBox.Focus();
            }
        }

        private TextBlock AddMessageToUI(string sender, string text, Brush bgBrush)
        {
            var border = new Border
            {
                Background = bgBrush,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = sender + ":", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,5) });
            
            var textBlock = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(textBlock);
            
            border.Child = stack;
            ChatHistoryPanel.Children.Add(border);
            ChatScroll.ScrollToBottom();

            return textBlock;
        }

        private async void TestApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string docInfo = await MainCommand.TaskHandler.ExecuteAsync(app =>
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null) return "Нет активного документа.";
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