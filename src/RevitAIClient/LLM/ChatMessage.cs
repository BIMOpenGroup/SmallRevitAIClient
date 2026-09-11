using System.Collections.Generic;

namespace RevitAIClient.LLM
{
    public class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
        
        // Для ответа ассистента с вызовом функций
        public List<ToolCall> tool_calls { get; set; }
        
        // Для ответа от функции (роль: tool)
        public string tool_call_id { get; set; }
        public string name { get; set; }
    }
}