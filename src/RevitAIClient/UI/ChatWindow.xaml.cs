using RevitAIClient.Commands;
using RevitAIClient.LLM;
using RevitAIClient.Skills;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RevitAIClient.UI
{
    public partial class ChatWindow : Window
    {
        private List<ChatMessage> _history = new List<ChatMessage>();
        private CancellationTokenSource _cts;
        private List<IRevitSkill> _skills = new List<IRevitSkill>();

        public ChatWindow()
        {
            InitializeComponent();
            
            // Системный промпт (пока базовый)
            _history.Add(new ChatMessage { role = "system", content = "You are a helpful Revit AI Assistant. Be concise and precise." });
            
            // Регистрация доступных навыков
            _skills.Add(new GetActiveViewSkill());
            _skills.Add(new SetElementParameterSkill());
            
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

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    // Shift+Enter: разрешаем стандартное поведение (перенос строки)
                    return;
                }
                else
                {
                    // Enter без Shift: отправляем сообщение
                    e.Handled = true; // Предотвращаем добавление новой строки в TextBox
                    if (InputBox.IsEnabled) // Защита от спама энтером
                    {
                        Send_Click(this, new RoutedEventArgs());
                    }
                }
            }
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
                await ProcessLLMRequestAsync(provider, assistantBlock, assistantContent);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    assistantBlock.Text += $"\n[Error: {ex.Message}]";
                });
                LogDebug($"Исключение: {ex.ToString()}");
                _history.RemoveAt(_history.Count - 1); // Удаляем запрос юзера при ошибке
            }
            finally
            {
                provider.OnDebugLog -= LogDebug;
                InputBox.IsEnabled = true;
                InputBox.Focus();
            }
        }

        private async Task ProcessLLMRequestAsync(OpenAIProvider provider, TextBlock assistantBlock, string assistantContent)
        {
            var tools = _skills.ConvertAll(s => s.GetSchema());
            
            var toolCalls = await provider.SendMessageStreamAsync(_history, tools, token =>
            {
                Dispatcher.Invoke(() =>
                {
                    assistantBlock.Text += token;
                    assistantContent += token;
                    ChatScroll.ScrollToBottom();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }, _cts.Token);

            if (toolCalls != null && toolCalls.Count > 0)
            {
                // Добавляем ответ ассистента с вызовом функций
                _history.Add(new ChatMessage { role = "assistant", content = assistantContent, tool_calls = toolCalls });
                
                foreach (var call in toolCalls)
                {
                    var skill = _skills.Find(s => s.Name == call.function.name);
                    string result;
                    if (skill != null)
                    {
                        LogDebug($"[Tool Execute] Name: {call.function.name} Args: {call.function.arguments}");
                        
                        bool userApproved = true;
                        
                        // Если инструмент требует подтверждения - запрашиваем через MessageBox
                        if (skill.RequiresConfirmation)
                        {
                            bool? dialogResult = null;
                            Dispatcher.Invoke(() =>
                            {
                                var msg = $"AI wants to execute: {skill.Name}\n\nArguments:\n{call.function.arguments}\n\nAllow execution?";
                                var resultMsg = MessageBox.Show(msg, "Action Required (Human-in-the-loop)", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                                dialogResult = (resultMsg == MessageBoxResult.Yes);
                            });
                            
                            userApproved = dialogResult ?? false;
                        }

                        if (userApproved)
                        {
                            result = await skill.ExecuteAsync(call.function.arguments);
                        }
                        else
                        {
                            result = "Error: User rejected the execution of this tool.";
                            LogDebug("[Tool Cancelled] User rejected action.");
                        }
                    }
                    else
                    {
                        result = $"Error: Tool {call.function.name} not found.";
                    }
                    
                    LogDebug($"[Tool Result] {result}");
                    
                    // Добавляем результат выполнения в историю
                    _history.Add(new ChatMessage { role = "tool", tool_call_id = call.id, name = call.function.name, content = result });
                    
                    Dispatcher.Invoke(() =>
                    {
                        assistantBlock.Text += $"\n[Executed: {call.function.name}]\n";
                        ChatScroll.ScrollToBottom();
                    });
                }
                
                // Рекурсивно запрашиваем LLM для генерации финального текстового ответа с учетом результатов инструментов
                var newBlock = AddMessageToUI("Assistant (Processing)", "...", Brushes.LightGray);
                newBlock.Text = string.Empty;
                await ProcessLLMRequestAsync(provider, newBlock, string.Empty);
            }
            else
            {
                // Обычный текстовый ответ завершен
                if (string.IsNullOrWhiteSpace(assistantContent))
                {
                    assistantContent = "[Empty Response]";
                    Dispatcher.Invoke(() => assistantBlock.Text = assistantContent);
                }
                _history.Add(new ChatMessage { role = "assistant", content = assistantContent });
                LogDebug("Ответ успешно получен и поток закрыт.");
            }
        }

        private TextBlock AddMessageToUI(string sender, string text, Brush bgBrush)
        {
            // Цветовая схема для современного дизайна
            bool isUser = sender.Equals("User", StringComparison.OrdinalIgnoreCase);
            bool isSystem = sender.StartsWith("System", StringComparison.OrdinalIgnoreCase);
            
            var bubbleColor = isUser ? (Brush)new BrushConverter().ConvertFrom("#DCF8C6") // Светло-зеленый (WhatsApp style)
                                     : (isSystem ? Brushes.LightYellow 
                                                 : (Brush)new BrushConverter().ConvertFrom("#F3F4F6")); // Серый для ассистента

            var border = new Border
            {
                Background = bubbleColor,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = this.Width * 0.85 // Ограничение ширины пузыря
            };

            // Добавляем легкую тень для эстетики
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 1,
                Opacity = 0.1,
                BlurRadius = 4
            };

            var stack = new StackPanel();
            
            if (!isUser) // Для юзера можно не писать "User:", и так понятно по цвету и выравниванию
            {
                stack.Children.Add(new TextBlock 
                { 
                    Text = sender, 
                    FontWeight = FontWeights.Bold, 
                    Foreground = (Brush)new BrushConverter().ConvertFrom("#4B5563"),
                    Margin = new Thickness(0, 0, 0, 4),
                    FontSize = 11
                });
            }
            
            var textBlock = new TextBlock 
            { 
                Text = text, 
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#111827"),
                FontSize = 13
            };
            
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