using System.Collections.Generic;

namespace RevitAIClient.LLM
{
    public class ToolCall
    {
        public string id { get; set; }
        public string type { get; set; } = "function";
        public FunctionCall function { get; set; } = new FunctionCall();
    }

    public class FunctionCall
    {
        public string name { get; set; }
        public string arguments { get; set; } = string.Empty;
    }

    public class ToolSchema
    {
        public string type { get; set; } = "function";
        public FunctionSchema function { get; set; }
    }

    public class FunctionSchema
    {
        public string name { get; set; }
        public string description { get; set; }
        public object parameters { get; set; }
    }
}