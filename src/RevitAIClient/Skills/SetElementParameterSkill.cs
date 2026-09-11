using Autodesk.Revit.DB;
using RevitAIClient.Commands;
using RevitAIClient.LLM;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RevitAIClient.Skills
{
    public class SetElementParameterSkill : IRevitSkill
    {
        public string Name => "set_element_parameter";
        public string Description => "Sets a parameter value for a specific element by its ID.";
        public bool RequiresConfirmation => true;

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
                                { "elementId", new Dictionary<string, string> { { "type", "integer" }, { "description", "The integer ID of the Revit element." } } },
                                { "parameterName", new Dictionary<string, string> { { "type", "string" }, { "description", "The name of the parameter to change." } } },
                                { "value", new Dictionary<string, string> { { "type", "string" }, { "description", "The new value to set." } } }
                            }
                        },
                        { "required", new[] { "elementId", "parameterName", "value" } }
                    }
                }
            };
        }

        public async Task<string> ExecuteAsync(string argumentsJson)
        {
            // Парсим аргументы
            int elementId = 0;
            string parameterName = string.Empty;
            string value = string.Empty;

            try
            {
                var serializer = new JavaScriptSerializer();
                var args = serializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                if (args.ContainsKey("elementId")) elementId = Convert.ToInt32(args["elementId"]);
                if (args.ContainsKey("parameterName")) parameterName = args["parameterName"].ToString();
                if (args.ContainsKey("value")) value = args["value"].ToString();
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

                Parameter param = elem.LookupParameter(parameterName);
                if (param == null) return $"Error: Parameter '{parameterName}' not found on element.";
                if (param.IsReadOnly) return $"Error: Parameter '{parameterName}' is read-only.";

                try
                {
                    using (Transaction t = new Transaction(doc, $"AI: Set {parameterName}"))
                    {
                        t.Start();
                        
                        bool success = false;
                        switch (param.StorageType)
                        {
                            case StorageType.String:
                                success = param.Set(value);
                                break;
                            case StorageType.Integer:
                                if (int.TryParse(value, out int intVal)) success = param.Set(intVal);
                                else return "Error: Cannot parse value as Integer.";
                                break;
                            case StorageType.Double:
                                if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblVal)) 
                                    success = param.Set(dblVal);
                                else return "Error: Cannot parse value as Double.";
                                break;
                            case StorageType.ElementId:
                                if (int.TryParse(value, out int idVal)) success = param.Set(new ElementId(idVal));
                                else return "Error: Cannot parse value as ElementId.";
                                break;
                        }

                        if (success)
                        {
                            t.Commit();
                            return $"Success: Parameter '{parameterName}' updated to '{value}'.";
                        }
                        else
                        {
                            t.RollBack();
                            return "Error: Failed to set parameter value (type mismatch or invalid data).";
                        }
                    }
                }
                catch (Exception ex)
                {
                    return $"Error during transaction: {ex.Message}";
                }
            });
        }
    }
}