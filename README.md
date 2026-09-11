# Revit AI Client (MCP-like Assistant)

A lightweight, zero-dependency Revit add-in that integrates AI models (DeepSeek, OpenAI) directly into Autodesk Revit. It acts as an intelligent assistant capable of understanding your Revit model and modifying it through a dynamic "Skills" system.

## 🌟 Key Features

* **Zero Heavy Dependencies**: Built purely on .NET 4.8 and Revit API. No bulky NuGet packages (like Newtonsoft.Json or Roslyn), ensuring clean and fast DLL builds that won't conflict with other plugins.
* **Modern Fluent UI**: A clean, non-modal WPF chat interface that floats over Revit without blocking your workflow.
* **Streaming Responses (SSE)**: Fast, real-time text generation directly in the UI.
* **Function Calling (Skills)**: 
  * **Hard Skills**: Built-in tools for reading parameters, getting active views, and modifying elements.
  * **Soft Skills (Dynamic)**: The AI can write C# code to create *new* skills for itself on the fly, compile them in memory, and use them immediately without restarting Revit!
* **Human-in-the-Loop Security**: Any AI action that modifies the Revit document requires explicit user confirmation before executing the transaction.

## 🛠️ Installation

1. Clone the repository.
2. Open `RevitAIClient.csproj` in Visual Studio (Target framework is .NET 4.8).
3. Check the reference paths for `RevitAPI.dll` and `RevitAPIUI.dll` (default is set to Revit 2023, adjust if necessary).
4. Build the solution.
5. Copy the compiled `RevitAIClient.dll` and `RevitAIClient.addin` to your Revit addins folder:
   `%AppData%\Autodesk\Revit\Addins\202X\`

## 🚀 How to Use

1. Launch Revit and run the **Revit AI Client** from the Add-ins tab.
2. In the UI, click the **⚙ Settings** button.
3. Enter your API Key (e.g., DeepSeek or OpenAI key).
4. Type your request in the chat. For example:
   * *"What elements are currently visible in the active view?"*
   * *"Create a new skill that calculates the total volume of all walls."*

### Dynamic Soft Skills
If you ask the AI to perform a task it doesn't know how to do, it can write a C# script to learn it! 
* Enable **Send Context** in the settings so the AI understands the conversation history.
* Enable **Debug Logs** to see how the AI compiles and registers its new code.
* The newly created skills will appear in the **🛠 Skills** panel and will be saved in `%AppData%\RevitAIClient\SoftSkills`.

## 🏗️ Architecture for Developers

If you want to contribute or create custom AI agents to work with this repo, please read the [REPO_MAP.md](REPO_MAP.md) file first. It contains critical information about Thread Bridging (`IExternalEventHandler`), the Skill architecture, and project constraints.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
