using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.Director.Screens;

internal sealed record WorkspaceEditorSnapshot(
    string WorkspacePath,
    string Name,
    string TodoPath,
    string DataDirectory,
    string TunnelProvider,
    string RunAs,
    bool IsPrimary,
    bool IsEnabled,
    string PromptTemplate,
    string StatusPrompt,
    string ImplementPrompt,
    string PlanPrompt)
{
    public static WorkspaceEditorSnapshot FromViewModel(WorkspaceDetailViewModel viewModel)
        => new(
            viewModel.EditorWorkspacePath,
            viewModel.EditorName,
            viewModel.EditorTodoPath,
            viewModel.EditorDataDirectory,
            viewModel.EditorTunnelProvider,
            viewModel.EditorRunAs,
            viewModel.EditorIsPrimary,
            viewModel.EditorIsEnabled,
            viewModel.EditorPromptTemplateText,
            viewModel.EditorStatusPromptText,
            viewModel.EditorImplementPromptText,
            viewModel.EditorPlanPromptText);

    public void ApplyTo(WorkspaceDetailViewModel viewModel)
    {
        viewModel.EditorWorkspacePath = WorkspacePath;
        viewModel.EditorName = Name;
        viewModel.EditorTodoPath = TodoPath;
        viewModel.EditorDataDirectory = DataDirectory;
        viewModel.EditorTunnelProvider = TunnelProvider;
        viewModel.EditorRunAs = RunAs;
        viewModel.EditorIsPrimary = IsPrimary;
        viewModel.EditorIsEnabled = IsEnabled;
        viewModel.EditorPromptTemplateText = PromptTemplate;
        viewModel.EditorStatusPromptText = StatusPrompt;
        viewModel.EditorImplementPromptText = ImplementPrompt;
        viewModel.EditorPlanPromptText = PlanPrompt;
        viewModel.IsDirty = true;
    }
}
