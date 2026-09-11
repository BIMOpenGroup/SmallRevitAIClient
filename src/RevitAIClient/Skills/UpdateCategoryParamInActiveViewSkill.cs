using Autodesk.Revit.DB;
using RevitAIClient.Commands;
using RevitAIClient.LLM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RevitAIClient.Skills
{
    public class UpdateCategoryParamInActiveViewSkill : IRevitSkill
    {
        public string Name => "update_category_param_in_active_view";
        public string Description => "Updates a parameter value for all elements of a specific category in the current active view.";
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
                                { "categoryName", new Dictionary<string, string> { { "type", "string" }, { "description", "The name of the Revit category (e.g., 'Walls', 'Doors', 'Стены')." } } },
                                { "parameterName", new Dictionary<string, string> { { "type", "string" }, { "description", "The name of the parameter to change." } } },
                                { "value", new Dictionary<string, string> { { "type", "string" }, { "description", "The new value to set." } } }
                            }
                        },
                        { "required", new[] { "categoryName", "parameterName", "value" } }
                    }
                }
            };
        }

        public async Task<string> ExecuteAsync(string argumentsJson)
        {
            // Парсим аргументы
            string categoryName = string.Empty;
            string parameterName = string.Empty;
            string value = string.Empty;

            try
            {
                var serializer = new JavaScriptSerializer();
                var args = serializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                if (args.ContainsKey("categoryName")) categoryName = args["categoryName"].ToString();
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
                var view = app.ActiveUIDocument?.ActiveView;

                if (doc == null || view == null) return "Error: No active document or view.";

                // Находим все элементы на активном виде
                var collector = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType();

                // Фильтруем по имени категории (case-insensitive)
                var elementsToUpdate = collector.ToElements()
                    .Where(e => e.Category != null && e.Category.Name.Equals(categoryName, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                if (elementsToUpdate.Count == 0)
                {
                    return $"No elements found for category '{categoryName}' in the active view.";
                }

                int successCount = 0;
                int failCount = 0;
                int notFoundCount = 0;

                try
                {
                    using (Transaction t = new Transaction(doc, $"AI: Update {parameterName} for {categoryName}"))
                    {
                        t.Start();
                        
                        foreach (var elem in elementsToUpdate)
                        {
                            Parameter param = elem.LookupParameter(parameterName);
                            if (param == null || param.IsReadOnly) 
                            { 
                                notFoundCount++; 
                                continue; 
                            }

                            bool success = false;
                            switch (param.StorageType)
                            {
                                case StorageType.String:
                                    success = param.Set(value);
                                    break;
                                case StorageType.Integer:
                                    if (int.TryParse(value, out int intVal)) success = param.Set(intVal);
                                    break;
                                case StorageType.Double:
                                    if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblVal)) 
                                        success = param.Set(dblVal);
                                    break;
                                case StorageType.ElementId:
                                    if (int.TryParse(value, out int idVal)) success = param.Set(new ElementId(idVal));
                                    break;
                            }

                            if (success) successCount++;
                            else failCount++;
                        }

                        if (successCount > 0)
                        {
                            t.Commit();
                            return $"Success! Updated {successCount} elements. Failed to parse value for {failCount} elements. Parameter read-only or not found on {notFoundCount} elements.";
                        }
                        else
                        {
                            t.RollBack();
                            return $"Error: Could not update any elements. Failed to parse value for {failCount} elements. Parameter read-only or not found on {notFoundCount} elements.";
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