using System;
using System.Collections.Generic;

namespace RevitAIClient.Skills.Dynamic
{
    /// <summary>
    /// Описание динамического навыка (Soft Skill).
    /// </summary>
    public class SoftSkillDefinition
    {
        /// <summary>
        /// Имя функции (навыка) для вызова из LLM (должно быть в формате a-zA-Z0-9_-)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Описание навыка для LLM, объясняющее, что он делает и когда его использовать.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// JSON-схема аргументов в формате, ожидаемом OpenAI/DeepSeek API.
        /// (Это строка с JSON-объектом, содержащим properties).
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// Код на C#, реализующий логику навыка. 
        /// Выполняется через CSharpCodeProvider.
        /// </summary>
        public string Code { get; set; }
    }
}
