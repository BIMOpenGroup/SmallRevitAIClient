using Autodesk.Revit.DB;
using RevitAIClient.Commands;
using RevitAIClient.LLM;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RevitAIClient.Skills
{
    public class GetElementParametersSkill : IRevitSkill
    {
        public string Name => "get_element_parameters";
        public string Description => "Gets all parameters and their current values for a specific element by its ID.";
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
                        { "properties", new Dictionary<string, object>
                            {
                                { "elementId", new Dictionary<string, string> { { "type", "integer" }, { "description", "The integer ID of the Revit element." } } }
                            }
                        },
                        { "required", new[] { "elementId" } }
                    }
                }
            };
        }

        public async Task<string> ExecuteAsync(string argumentsJson)
        {
            int elementId = 0;
            try
            {
                var serializer = new JavaScriptSerializer();
                var args = serializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                if (args.ContainsKey("elementId")) elementId = Convert.ToInt32(args["elementId"]);
            }
            catch (Exception ex)
            {
                return $"Error parsing arguments: {ex.Message}";
            }

            return await MainCommand.TaskHandler.ExecuteAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) return "Error: No active document.";

                ElementId id = new ElementId(elementId);
                Element elem = doc.GetElement(id);
                if (elem == null) return $"Error: Element with ID {elementId} not found.";

                var sb = new StringBuilder();
                sb.AppendLine($"Parameters for Element {elementId} ({elem.Name}):");
                
                // Проходимся по всем параметрам элемента
                foreach (Parameter param in elem.Parameters)
                {
                    if (param == null) continue;
                    
                    string val = param.AsValueString();
                    if (string.IsNullOrEmpty(val))
                    {
                        switch (param.StorageType)
                        {
                            case StorageType.String: val = param.AsString(); break;
                            case StorageType.Integer: val = param.AsInteger().ToString(); break;
                            case StorageType.Double: val = param.AsDouble().ToString(); break;
                            case StorageType.ElementId: val = param.AsElementId().IntegerValue.ToString(); break;
                            case StorageType.None: val = "None"; break;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(val)) val = "Empty";
                    
                    // Помечаем параметры только для чтения, чтобы LLM понимала, что их нельзя менять
                    string readOnly = param.IsReadOnly ? "[Read-Only]" : "[Writable]";
                    
                    sb.AppendLine($"- {param.Definition.Name}: {val} {readOnly}");
                }

                return sb.ToString();
            });
        }
    }
}