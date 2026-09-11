using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace RevitAIClient.Skills.Dynamic
{
    /// <summary>
    /// Менеджер для сохранения и загрузки динамических навыков (Soft Skills) из AppData.
    /// </summary>
    public class SoftSkillManager
    {
        private readonly string _skillsDirectory;
        private readonly JavaScriptSerializer _serializer;

        public SoftSkillManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _skillsDirectory = Path.Combine(appData, "RevitAIClient", "SoftSkills");
            
            if (!Directory.Exists(_skillsDirectory))
            {
                Directory.CreateDirectory(_skillsDirectory);
            }
            
            _serializer = new JavaScriptSerializer();
        }

        /// <summary>
        /// Сохраняет или обновляет динамический навык в файл JSON.
        /// </summary>
        public void SaveSkill(SoftSkillDefinition skill)
        {
            if (string.IsNullOrWhiteSpace(skill.Name))
            {
                throw new ArgumentException("Skill name cannot be empty.");
            }

            string filePath = Path.Combine(_skillsDirectory, skill.Name + ".json");
            string json = _serializer.Serialize(skill);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Загружает все сохраненные динамические навыки.
        /// </summary>
        public List<SoftSkillDefinition> LoadAllSkills()
        {
            var skills = new List<SoftSkillDefinition>();
            if (Directory.Exists(_skillsDirectory))
            {
                foreach (var file in Directory.GetFiles(_skillsDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var skill = _serializer.Deserialize<SoftSkillDefinition>(json);
                        if (skill != null && !string.IsNullOrWhiteSpace(skill.Name))
                        {
                            skills.Add(skill);
                        }
                    }
                    catch (Exception)
                    {
                        // Игнорируем поврежденные файлы, чтобы не прерывать загрузку остальных навыков
                    }
                }
            }
            return skills;
        }
        
        /// <summary>
        /// Загружает конкретный динамический навык по имени (для редактирования).
        /// </summary>
        public SoftSkillDefinition LoadSkill(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string filePath = Path.Combine(_skillsDirectory, name + ".json");
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    return _serializer.Deserialize<SoftSkillDefinition>(json);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }
    }
}
