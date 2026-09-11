using RevitAIClient.LLM;
using System.Threading.Tasks;

namespace RevitAIClient.Skills
{
    public interface IRevitSkill
    {
        string Name { get; }
        string Description { get; }
        bool RequiresConfirmation { get; }
        
        ToolSchema GetSchema();
        Task<string> ExecuteAsync(string argumentsJson);
    }
}