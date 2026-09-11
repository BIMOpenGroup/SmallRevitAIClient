using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RevitAIClient.LLM
{
    public class OpenAIProvider : ILLMProvider
    {
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly bool _useThinking;
        private readonly string _reasoningEffort;
        
        // Событие для дебаг-логирования
        public event Action<string> OnDebugLog;

        // Переиспользуем HttpClient по рекомендации Microsoft
        private static readonly HttpClient _httpClient = new HttpClient();

        public OpenAIProvider(string apiKey, string endpoint, string model, bool useThinking = false, string reasoningEffort = "high")
        {
            _apiKey = apiKey?.Trim();
            _endpoint = string.IsNullOrWhiteSpace(endpoint) ? "https://api.deepseek.com/chat/completions" : endpoint.Trim();
            
            // Если пользователь ввел только базовый URL (например, https://api.deepseek.com), добавляем нужный путь
            if (_endpoint.EndsWith("/")) _endpoint = _endpoint.TrimEnd('/');
            if (!_endpoint.EndsWith("/chat/completions") && !_endpoint.EndsWith("/completions"))
            {
                _endpoint += "/chat/completions";
            }

            _model = string.IsNullOrWhiteSpace(model) ? "deepseek-flash" : model.Trim();
            _useThinking = useThinking;
            _reasoningEffort = reasoningEffort;
        }

        public async Task<List<ToolCall>> SendMessageStreamAsync(List<ChatMessage> history, List<ToolSchema> tools, Action<string> onTokenReceived, CancellationToken cancellationToken)
        {
            var serializer = new JavaScriptSerializer();
            
            var payload = new Dictionary<string, object>
            {
                { "model", _model },
                { "messages", history },
                { "stream", true }
            };

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = tools;
            }

            if (_useThinking)
            {
                payload["thinking"] = new Dictionary<string, string> { { "type", "enabled" } };
                payload["reasoning_effort"] = _reasoningEffort;
            }

            var json = serializer.Serialize(payload);
            OnDebugLog?.Invoke($"Payload size: {json.Length} chars. Endpoint: {_endpoint}");

            var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var toolCallsMap = new Dictionary<int, ToolCall>();

            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    OnDebugLog?.Invoke($"HTTP Error {(int)response.StatusCode}: {errorContent}");
                    throw new Exception($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\nResponse: {errorContent}");
                }
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        if (line.StartsWith("data: "))
                        {
                            var data = line.Substring(6).Trim();
                            if (data == "[DONE]") break;

                            try
                            {
                                var parsed = serializer.Deserialize<Dictionary<string, object>>(data);
                                if (parsed != null && parsed.TryGetValue("choices", out var choicesObj) && choicesObj is System.Collections.ArrayList choices && choices.Count > 0)
                                {
                                    if (choices[0] is Dictionary<string, object> choice && 
                                        choice.TryGetValue("delta", out var deltaObj) && deltaObj is Dictionary<string, object> delta)
                                    {
                                        if (delta.TryGetValue("content", out var contentObj) && contentObj is string content)
                                        {
                                            onTokenReceived?.Invoke(content);
                                        }
                                        else if (delta.TryGetValue("reasoning_content", out var reasoningObj) && reasoningObj is string reasoningContent)
                                        {
                                            // DeepSeek использует reasoning_content для процесса "Thinking".
                                        }
                                        
                                        // Парсинг вызова функций (Tool Calls) из SSE-потока
                                        if (delta.TryGetValue("tool_calls", out var toolCallsObj) && toolCallsObj is System.Collections.ArrayList tcArray)
                                        {
                                            foreach (Dictionary<string, object> tcDelta in tcArray)
                                            {
                                                if (tcDelta.TryGetValue("index", out var indexObj) && indexObj is int index)
                                                {
                                                    if (!toolCallsMap.ContainsKey(index))
                                                    {
                                                        toolCallsMap[index] = new ToolCall();
                                                    }
                                                    
                                                    if (tcDelta.TryGetValue("id", out var idObj) && idObj is string id)
                                                        toolCallsMap[index].id = id;
                                                        
                                                    if (tcDelta.TryGetValue("function", out var funcObj) && funcObj is Dictionary<string, object> funcDelta)
                                                    {
                                                        if (funcDelta.TryGetValue("name", out var nameObj) && nameObj is string name)
                                                            toolCallsMap[index].function.name = name;
                                                        if (funcDelta.TryGetValue("arguments", out var argsObj) && argsObj is string args)
                                                            toolCallsMap[index].function.arguments += args;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            { 
                                OnDebugLog?.Invoke($"Ошибка парсинга чанка: {ex.Message}. Чанк: {data}");
                            }
                        }
                    }
                }
            }
            
            return new List<ToolCall>(toolCallsMap.Values);
        }
    }
}