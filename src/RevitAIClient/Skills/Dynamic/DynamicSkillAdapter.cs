using System;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using RevitAIClient.Commands;
using RevitAIClient.LLM;

namespace RevitAIClient.Skills.Dynamic
{
    /// <summary>
    /// Адаптер, превращающий SoftSkillDefinition в полноценный IRevitSkill.
    /// Выполняет ленивую компиляцию кода при первом вызове.
    /// </summary>
    public class DynamicSkillAdapter : IRevitSkill
    {
        private readonly SoftSkillDefinition _definition;
        private readonly JavaScriptSerializer _serializer;
        private Func<Autodesk.Revit.UI.UIApplication, string, string> _compiledAction;

        public DynamicSkillAdapter(SoftSkillDefinition definition)
        {
            _definition = definition;
            _serializer = new JavaScriptSerializer();
        }

        public string Name => _definition.Name;
        public string Description => _definition.Description;
        
        // ВСЕ динамические скрипты считаются небезопасными (могут содержать транзакции)
        public bool RequiresConfirmation => true; 

        public ToolSchema GetSchema()
        {
            if (string.IsNullOrWhiteSpace(_definition.Schema))
            {
                return new ToolSchema
                {
                    Type = "object",
                    Properties = new System.Collections.Generic.Dictionary<string, object>()
                };
            }

            try
            {
                return _serializer.Deserialize<ToolSchema>(_definition.Schema);
            }
            catch (Exception ex)
            {
                // Если LLM написала невалидный JSON для схемы
                System.Diagnostics.Debug.WriteLine($"Failed to parse schema for {Name}: {ex.Message}");
                return new ToolSchema
                {
                    Type = "object",
                    Properties = new System.Collections.Generic.Dictionary<string, object>()
                };
            }
        }

        public async Task<string> ExecuteAsync(string argumentsJson)
        {
            try
            {
                // Ленивая компиляция при первом запуске
                if (_compiledAction == null)
                {
                    _compiledAction = DynamicSkillCompiler.CompileSkill(_definition.Code);
                }

                // Передаем в UI-поток Revit
                return await MainCommand.TaskHandler.ExecuteAsync(app =>
                {
                    return _compiledAction(app, argumentsJson);
                });
            }
            catch (Exception ex)
            {
                // Возвращаем ошибку в LLM, чтобы она могла понять, что пошло не так
                return $"Error executing dynamic skill '{Name}': {ex.Message}\n{ex.InnerException?.Message}";
            }
        }
    }
}
