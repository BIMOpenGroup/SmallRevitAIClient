using System;
using System.CodeDom.Compiler;
using System.Linq;
using Autodesk.Revit.UI;
using Microsoft.CSharp;

namespace RevitAIClient.Skills.Dynamic
{
    /// <summary>
    /// Компилятор динамического кода C# с использованием встроенного CSharpCodeProvider.
    /// </summary>
    public static class DynamicSkillCompiler
    {
        /// <summary>
        /// Компилирует тело метода в делегат Func&lt;UIApplication, string, string&gt;.
        /// Входные параметры:
        /// 1. app (UIApplication) - доступ к текущей сессии Revit
        /// 2. argumentsJson (string) - JSON-строка с аргументами, переданная от LLM
        /// Возвращает: строку результата выполнения
        /// </summary>
        public static Func<UIApplication, string, string> CompileSkill(string code)
        {
            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters();
                
                // Добавляем необходимые зависимости
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                parameters.ReferencedAssemblies.Add("System.Web.Extensions.dll"); // Для JavaScriptSerializer
                parameters.ReferencedAssemblies.Add(typeof(Autodesk.Revit.DB.Document).Assembly.Location); // RevitAPI.dll
                parameters.ReferencedAssemblies.Add(typeof(Autodesk.Revit.UI.UIApplication).Assembly.Location); // RevitAPIUI.dll
                parameters.ReferencedAssemblies.Add(typeof(DynamicSkillCompiler).Assembly.Location); // Текущая сборка плагина

                parameters.GenerateInMemory = true;
                parameters.GenerateExecutable = false;

                // Оборачиваем код пользователя в класс и метод
                string fullCode = $@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DynamicSkills 
{{
    public class SkillRunner 
    {{
        public string Run(UIApplication app, string argumentsJson) 
        {{
            var serializer = new JavaScriptSerializer();
            var args = string.IsNullOrWhiteSpace(argumentsJson) ? new Dictionary<string, object>() : serializer.Deserialize<Dictionary<string, object>>(argumentsJson);
            
            {code}
        }}
    }}
}}";

                var results = provider.CompileAssemblyFromSource(parameters, fullCode);

                if (results.Errors.HasErrors)
                {
                    var errors = string.Join(Environment.NewLine, results.Errors.Cast<CompilerError>().Select(e => $"Line {e.Line}: {e.ErrorText}"));
                    throw new Exception("Compilation failed:\n" + errors);
                }

                var type = results.CompiledAssembly.GetType("DynamicSkills.SkillRunner");
                var instance = Activator.CreateInstance(type);
                var method = type.GetMethod("Run");

                return (app, argsJson) => (string)method.Invoke(instance, new object[] { app, argsJson });
            }
        }
    }
}
