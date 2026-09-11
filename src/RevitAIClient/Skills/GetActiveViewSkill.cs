using Autodesk.Revit.DB;
using RevitAIClient.Commands;
using RevitAIClient.LLM;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RevitAIClient.Skills
{
    public class GetActiveViewSkill : IRevitSkill
    {
        public string Name => "get_active_view_info";
        public string Description => "Gets information about the currently active view in Revit.";
        public bool RequiresConfirmation => false;

        public ToolSchema GetSchema()
        {
            return new ToolSchema
            {
                function = new FunctionSchema
                {
                    name = Name,
                    description = Description,
                    parameters = new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() }
                    }
                }
            };
        }

        public async Task<string> ExecuteAsync(string argumentsJson)
        {
            return await MainCommand.TaskHandler.ExecuteAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                var view = app.ActiveUIDocument?.ActiveView;
                
                if (doc == null || view == null) return "No active document or view.";
                
                return $"Active View Name: {view.Name}\nView Type: {view.ViewType}\nScale: 1:{view.Scale}";
            });
        }
    }
}