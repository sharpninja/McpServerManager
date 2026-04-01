namespace McpServerManager.Core.ViewModels;

public class EditorTab : McpServerManager.UI.Core.ViewModels.EditorTab
{
    public static new EditorTab CreateEditorTab(string todoId, string content)
        => new() { Header = todoId, Content = content, IsMarkdown = false };

    public static new EditorTab CreateCopilotTab(string action, string content)
        => new() { Header = char.ToUpper(action[0]) + action[1..], Content = content, IsMarkdown = true };
}
