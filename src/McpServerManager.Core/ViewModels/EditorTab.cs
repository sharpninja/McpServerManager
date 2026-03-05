using CommunityToolkit.Mvvm.ComponentModel;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Represents a tab in the TODO editor area.
/// The first tab is the raw YAML editor; subsequent tabs are Copilot response views.
/// </summary>
public partial class EditorTab : ObservableObject
{
    [ObservableProperty] private string _header = "";
    [ObservableProperty] private string _content = "";
    [ObservableProperty] private bool _isMarkdown;

    /// <summary>Creates the primary editor tab for a TODO item.</summary>
    public static EditorTab CreateEditorTab(string todoId, string content)
        => new() { Header = todoId, Content = content, IsMarkdown = false };

    /// <summary>Creates a Copilot response tab rendered as Markdown.</summary>
    public static EditorTab CreateCopilotTab(string action, string content)
        => new() { Header = char.ToUpper(action[0]) + action[1..], Content = content, IsMarkdown = true };
}
