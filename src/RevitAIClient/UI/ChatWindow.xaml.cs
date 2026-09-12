using RevitAIClient.Commands;
using RevitAIClient.LLM;
using RevitAIClient.Skills;
using RevitAIClient.Skills.Dynamic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private List<ChatMessage> _displayHistory = new List<ChatMessage>();
        private CancellationTokenSource _cts;
        private List<IRevitSkill> _skills = new List<IRevitSkill>();
        private SoftSkillManager _softSkillManager;

        public ChatWindow()
        {
            InitializeComponent();
            
            _softSkillManager = new SoftSkillManager();
            
            // Системный промпт (пока базовый)
            _history.Add(new ChatMessage { role = "system", content = "You are a helpful Revit AI Assistant. Be concise and precise." });
            
            InitializeSkills();
            
            LoadApiKey();
            LoadHistory();
            LoadWindowSettings();
            
            this.Closing += ChatWindow_Closing;
        }

        private void ChatWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
        }

        private void InitializeSkills()
        {
            _skills.Clear();

            // Регистрация встроенных (Hard) навыков
            _skills.Add(new GetActiveViewSkill());
            _skills.Add(new SetElementParameterSkill());
            _skills.Add(new UpdateCategoryParamInActiveViewSkill());
            _skills.Add(new GetElementParametersSkill());
            
            // Регистрация системных навыков для управления динамическими навыками
            var createUpdateSkill = new CreateOrUpdateSoftSkillSkill(_softSkillManager);
            createUpdateSkill.OnSkillUpdated += RefreshSkillsList; // Подписка на событие обновления
            _skills.Add(createUpdateSkill);
            
            _skills.Add(new GetSoftSkillCodeSkill(_softSkillManager));

            // Загрузка динамических (Soft) навыков
            var softSkills = _softSkillManager.LoadAllSkills();
            foreach (var sd in softSkills)
            {
                _skills.Add(new DynamicSkillAdapter(sd));
            }

            UpdateSkillsUI();
        }

        private void RefreshSkillsList()
        {
            Dispatcher.Invoke(() =>
            {
                InitializeSkills();
                LogDebug("Skills list refreshed automatically.");
            });
        }

        private void UpdateSkillsUI()
        {
            SkillsListPanel.Children.Clear();

            // Группировка: сначала Hard Skills, затем Soft Skills
            var hardSkills = _skills.Where(s => !(s is DynamicSkillAdapter)).ToList();
            var softSkills = _skills.Where(s => s is DynamicSkillAdapter).ToList();

            AddSkillGroupToUI("Built-in (Hard)", hardSkills, Brushes.LightGray);
            AddSkillGroupToUI("Dynamic (Soft)", softSkills, Brushes.LightBlue);
        }

        private void AddSkillGroupToUI(string title, List<IRevitSkill> skillsGroup, Brush tagColor)
        {
            if (skillsGroup.Count == 0) return;

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 5, 0, 5)
            };
            SkillsListPanel.Children.Add(titleBlock);

            foreach (var skill in skillsGroup)
            {
                var border = new Border
                {
                    Background = tagColor,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var tb = new TextBlock
                {
                    Text = skill.Name,
                    FontSize = 12,
                    ToolTip = skill.Description,
                    TextWrapping = TextWrapping.Wrap
                };

                border.Child = tb;
                SkillsListPanel.Children.Add(border);
            }
        }

        private void SettingsToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = SettingsToggleBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SkillsToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            SkillsPanel.Visibility = SkillsToggleBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetWindowSettingsPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "RevitAIClient");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "window_settings.json");
        }

        private void SaveWindowSettings()
        {
            try
            {
                var settings = new Dictionary<string, double>
                {
                    { "Width", this.ActualWidth },
                    { "Height", this.ActualHeight },
                    { "Top", this.Top },
                    { "Left", this.Left }
                };
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                File.WriteAllText(GetWindowSettingsPath(), serializer.Serialize(settings));
            }
            catch (Exception ex)
            {
                LogDebug($"Error saving window settings: {ex.Message}");
            }
        }

        private void LoadWindowSettings()
        {
            try
            {
                var path = GetWindowSettingsPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var settings = serializer.Deserialize<Dictionary<string, double>>(json);
                    if (settings != null)
                    {
                        if (settings.ContainsKey("Width") && settings["Width"] > 100) this.Width = settings["Width"];
                        if (settings.ContainsKey("Height") && settings["Height"] > 100) this.Height = settings["Height"];
                        
                        if (settings.ContainsKey("Top") && settings.ContainsKey("Left"))
                        {
                            double top = settings["Top"];
                            double left = settings["Left"];
                            double w = settings.ContainsKey("Width") ? settings["Width"] : this.Width;
                            double h = settings.ContainsKey("Height") ? settings["Height"] : this.Height;
                            
                            // Проверка, что окно будет видно на экране (защита от сохранения координат с отключенного монитора)
                            if (left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                                top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight &&
                                left + w > SystemParameters.VirtualScreenLeft &&
                                top + h > SystemParameters.VirtualScreenTop)
                            {
                                this.Top = top;
                                this.Left = left;
                                this.WindowStartupLocation = WindowStartupLocation.Manual;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading window settings: {ex.Message}");
            }
        }

        private string GetConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "RevitAIClient");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "apikey.txt");
        }

        private string GetHistoryPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "RevitAIClient");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "history.json");
        }

        private void LoadHistory()
        {
            try
            {
                var path = GetHistoryPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    _displayHistory = serializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
                    
                    foreach (var msg in _displayHistory)
                    {
                        if (msg.role == "user")
                            AddMessageToUI("User", msg.content, null);
                        else if (msg.role == "assistant" && !string.IsNullOrEmpty(msg.content))
                            AddMessageToUI("Assistant", msg.content, null);
                        else if (msg.role == "tool")
                            AddMessageToUI("System [Tool Result]", msg.content, null);
                    }
                    ChatScroll.ScrollToBottom();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading history: {ex.Message}");
            }
        }

        private void SaveDisplayHistory()
        {
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                File.WriteAllText(GetHistoryPath(), serializer.Serialize(_displayHistory));
            }
            catch { }
        }

        private void RecordMessage(ChatMessage msg)
        {
            _history.Add(msg);
            _displayHistory.Add(msg);
            SaveDisplayHistory();
        }

        private void UndoLastMessage()
        {
            if (_history.Count > 0) _history.RemoveAt(_history.Count - 1);
            if (_displayHistory.Count > 0) _displayHistory.RemoveAt(_displayHistory.Count - 1);
            SaveDisplayHistory();
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
            Dispatcher.Invoke(() =>
            {
                if (DebugModeCheck.IsChecked == true)
                {
                    AddMessageToUI("System [DEBUG]", message, Brushes.LightYellow);
                }
            });
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

            RecordMessage(new ChatMessage { role = "user", content = userText });
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
                UndoLastMessage(); // Удаляем запрос юзера при ошибке
            }
            finally
            {
                provider.OnDebugLog -= LogDebug;
                InputBox.IsEnabled = true;
                InputBox.Focus();
            }
        }

        private List<ChatMessage> GetMessagesToSend()
        {
            bool sendContext = false;
            Dispatcher.Invoke(() => sendContext = SendContextCheck.IsChecked == true);

            if (sendContext)
            {
                return _history;
            }
            else
            {
                var list = new List<ChatMessage>();
                if (_history.Count > 0)
                    list.Add(_history[0]); // System prompt
                
                int lastUserIdx = _history.FindLastIndex(m => m.role == "user");
                if (lastUserIdx > 0)
                {
                    for (int i = lastUserIdx; i < _history.Count; i++)
                    {
                        list.Add(_history[i]);
                    }
                }
                return list;
            }
        }

        private async Task ProcessLLMRequestAsync(OpenAIProvider provider, TextBlock assistantBlock, string assistantContent)
        {
            var tools = _skills.ConvertAll(s => s.GetSchema());
            var messagesToSend = GetMessagesToSend();
            
            var toolCalls = await provider.SendMessageStreamAsync(messagesToSend, tools, token =>
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
                RecordMessage(new ChatMessage { role = "assistant", content = assistantContent, tool_calls = toolCalls });
                
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
                    RecordMessage(new ChatMessage { role = "tool", tool_call_id = call.id, name = call.function.name, content = result });
                    
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
                RecordMessage(new ChatMessage { role = "assistant", content = assistantContent });
                LogDebug("Ответ успешно получен и поток закрыт.");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ChatHistoryPanel.Children.Clear();
            _displayHistory.Clear();
            _history.Clear();
            _history.Add(new ChatMessage { role = "system", content = "You are a helpful Revit AI Assistant. Be concise and precise." });
            
            try
            {
                var path = GetHistoryPath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
            
            InputBox.Focus();
        }

        private TextBlock AddMessageToUI(string sender, string text, Brush bgBrush = null)
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
    }
}