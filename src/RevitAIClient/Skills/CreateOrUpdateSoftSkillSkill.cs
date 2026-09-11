using System;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using RevitAIClient.LLM;
using RevitAIClient.Skills.Dynamic;

namespace RevitAIClient.Skills
{
    /// <summary>
    /// Системный (Hard) навык для создания или обновления динамических (Soft) навыков.
    /// </summary>
    public class CreateOrUpdateSoftSkillSkill : IRevitSkill
    {
        private readonly SoftSkillManager _manager;

        // Событие, которое срабатывает при успешном создании/обновлении навыка
        public event Action OnSkillUpdated;

        public CreateOrUpdateSoftSkillSkill(SoftSkillManager manager)
        {
            _manager = manager;
        }

        public string Name => "CreateOrUpdateSoftSkill";

        public string Description => "Creates or updates a dynamic C# skill for Revit. Use this to teach yourself new abilities based on user requests. Provide the C# code for the 'Run' method body, taking 'UIApplication app' and 'string argumentsJson' as inputs.";

        // Мы не требуем подтверждения на СОХРАНЕНИЕ навыка (это просто запись в файл).
        // Подтверждение потребуется при его ВЫПОЛНЕНИИ.
        public bool RequiresConfirmation => false;

        public ToolSchema GetSchema()
        {
            return new ToolSchema
            {
                type = "function",
                function = new FunctionSchema
                {
                    name = Name,
                    description = Description,
                    parameters = new
                    {
                        type = "object",
                        properties = new System.Collections.Generic.Dictionary<string, object>
                        {
                            {
                                "skillName", new
                                {
                                    type = "string",
                                    description = "Name of the skill (a-zA-Z0-9_-), e.g., 'DrawWallSkill'."
                                }
                            },
                            {
                                "description", new
                                {
                                    type = "string",
                                    description = "What the skill does and when to use it."
                                }
                            },
                            {
                                "parametersSchemaJson", new
                                {
                                    type = "string",
                                    description = "JSON schema for the arguments this skill expects (as a JSON string)."
                                }
                            },
                            {
                                "csharpCode", new
                                {
                                    type = "string",
                                    description = "The C# code body. MUST be valid C# 5.0 syntax. Do not use string interpolation ($). You have access to 'app' (UIApplication) and 'argumentsJson' (string). Return a string result."
                                }
                            }
                        },
                        required = new[] { "skillName", "description", "parametersSchemaJson", "csharpCode" }
                    }
                }
            };
        }

        public Task<string> ExecuteAsync(string argumentsJson)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var args = serializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(argumentsJson);

                if (args == null || !args.ContainsKey("skillName") || !args.ContainsKey("csharpCode"))
                {
                    return Task.FromResult("Error: Missing required arguments (skillName, csharpCode).");
                }

                string skillName = args["skillName"];
                string description = args.ContainsKey("description") ? args["description"] : "";
                string schema = args.ContainsKey("parametersSchemaJson") ? args["parametersSchemaJson"] : "{\"type\":\"object\",\"properties\":{}}";
                string code = args["csharpCode"];

                // Шаг 1: Попытка скомпилировать код, чтобы сразу отловить ошибки синтаксиса
                try
                {
                    DynamicSkillCompiler.CompileSkill(code);
                }
                catch (Exception ex)
                {
                    // Возвращаем ошибку компиляции обратно LLM, чтобы она исправила код
                    return Task.FromResult($"Compilation failed. Please fix the code and try again.\nDetails: {ex.Message}");
                }

                // Шаг 2: Если компиляция успешна, сохраняем навык
                var definition = new SoftSkillDefinition
                {
                    Name = skillName,
                    Description = description,
                    Schema = schema,
                    Code = code
                };

                _manager.SaveSkill(definition);

                // Вызываем событие для обновления UI
                OnSkillUpdated?.Invoke();
                
                return Task.FromResult($"Skill '{skillName}' created/updated successfully. You can now use it in our conversation.");
            }
            catch (Exception ex)

            {
                return Task.FromResult($"Error creating skill: {ex.Message}");
            }
        }
    }
}
