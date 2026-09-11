using System;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using RevitAIClient.LLM;
using RevitAIClient.Skills.Dynamic;

namespace RevitAIClient.Skills
{
    /// <summary>
    /// Системный (Hard) навык для чтения исходного кода существующих динамических навыков.
    /// Позволяет LLM анализировать и изменять ранее написанный код.
    /// </summary>
    public class GetSoftSkillCodeSkill : IRevitSkill
    {
        private readonly SoftSkillManager _manager;

        public GetSoftSkillCodeSkill(SoftSkillManager manager)
        {
            _manager = manager;
        }

        public string Name => "GetSoftSkillCode";

        public string Description => "Reads the source code and schema of an existing dynamic soft skill. Use this when the user asks you to modify, fix, or review a skill you previously created.";

        // Это только чтение локального файла, не требует подтверждения
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
                                    description = "The exact name of the skill to read (e.g., 'DrawWallSkill')."
                                }
                            }
                        },
                        required = new[] { "skillName" }
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

                if (args == null || !args.ContainsKey("skillName"))
                {
                    return Task.FromResult("Error: Missing required argument 'skillName'.");
                }

                string skillName = args["skillName"];
                
                var skillDefinition = _manager.LoadSkill(skillName);

                if (skillDefinition == null)
                {
                    return Task.FromResult($"Error: Skill '{skillName}' not found.");
                }

                // Формируем красивый JSON-ответ с кодом и текущей схемой
                var responseObj = new
                {
                    Name = skillDefinition.Name,
                    Description = skillDefinition.Description,
                    ParametersSchemaJson = skillDefinition.Schema,
                    CSharpCode = skillDefinition.Code
                };

                return Task.FromResult(serializer.Serialize(responseObj));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error reading skill code: {ex.Message}");
            }
        }
    }
}
