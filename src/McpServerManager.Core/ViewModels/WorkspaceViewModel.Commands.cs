using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class WorkspaceViewModel
{
    private CqrsRelayCommand<bool>? _loadWorkspacesCommand;
    public CqrsRelayCommand<bool> LoadWorkspacesCommand => _loadWorkspacesCommand ??= CqrsRelayFactory.Create(_dispatcher, LoadWorkspacesAsync);

    private CqrsRelayCommand<bool>? _refreshCommand;
    public CqrsRelayCommand<bool> RefreshCommand => _refreshCommand ??= CqrsRelayFactory.Create(_dispatcher, RefreshAsync);

    private CqrsRelayCommand<bool>? _loadGlobalPromptCommand;
    public CqrsRelayCommand<bool> LoadGlobalPromptCommand => _loadGlobalPromptCommand ??= CqrsRelayFactory.Create(_dispatcher, LoadGlobalPromptAsync);

    private CqrsRelayCommand<bool>? _saveGlobalPromptCommand;
    public CqrsRelayCommand<bool> SaveGlobalPromptCommand => _saveGlobalPromptCommand ??= CqrsRelayFactory.Create(_dispatcher, SaveGlobalPromptAsync);

    private CqrsRelayCommand<bool>? _resetGlobalPromptCommand;
    public CqrsRelayCommand<bool> ResetGlobalPromptCommand => _resetGlobalPromptCommand ??= CqrsRelayFactory.Create(_dispatcher, ResetGlobalPromptAsync);

    private CqrsRelayCommand<bool>? _clearFiltersCommand;
    public CqrsRelayCommand<bool> ClearFiltersCommand => _clearFiltersCommand ??= CqrsRelayFactory.Create(_dispatcher, ClearFilters);

    private CqrsRelayCommand<bool>? _newWorkspaceCommand;
    public CqrsRelayCommand<bool> NewWorkspaceCommand => _newWorkspaceCommand ??= CqrsRelayFactory.Create(_dispatcher, NewWorkspace);

    private CqrsRelayCommand<bool>? _openSelectedWorkspaceCommand;
    public CqrsRelayCommand<bool> OpenSelectedWorkspaceCommand => _openSelectedWorkspaceCommand ??= CqrsRelayFactory.Create(_dispatcher, OpenSelectedWorkspaceAsync);

    private CqrsRelayCommand<bool>? _saveEditorCommand;
    public CqrsRelayCommand<bool> SaveEditorCommand => _saveEditorCommand ??= CqrsRelayFactory.Create(_dispatcher, SaveEditorAsync);

    private CqrsRelayCommand<bool>? _deleteSelectedCommand;
    public CqrsRelayCommand<bool> DeleteSelectedCommand => _deleteSelectedCommand ??= CqrsRelayFactory.Create(_dispatcher, DeleteSelectedAsync);

    private CqrsRelayCommand<bool>? _getSelectedStatusCommand;
    public CqrsRelayCommand<bool> GetSelectedStatusCommand => _getSelectedStatusCommand ??= CqrsRelayFactory.Create(_dispatcher, GetSelectedStatusAsync);

    private CqrsRelayCommand<bool>? _checkSelectedWorkspaceHealthCommand;
    public CqrsRelayCommand<bool> CheckSelectedWorkspaceHealthCommand => _checkSelectedWorkspaceHealthCommand ??= CqrsRelayFactory.Create(_dispatcher, CheckSelectedWorkspaceHealthAsync, CanCheckSelectedWorkspaceHealth);

    private CqrsRelayCommand<bool>? _initSelectedWorkspaceCommand;
    public CqrsRelayCommand<bool> InitSelectedWorkspaceCommand => _initSelectedWorkspaceCommand ??= CqrsRelayFactory.Create(_dispatcher, InitSelectedWorkspaceAsync);

    private CqrsRelayCommand<bool>? _startSelectedWorkspaceCommand;
    public CqrsRelayCommand<bool> StartSelectedWorkspaceCommand => _startSelectedWorkspaceCommand ??= CqrsRelayFactory.Create(_dispatcher, StartSelectedWorkspaceAsync);

    private CqrsRelayCommand<bool>? _stopSelectedWorkspaceCommand;
    public CqrsRelayCommand<bool> StopSelectedWorkspaceCommand => _stopSelectedWorkspaceCommand ??= CqrsRelayFactory.Create(_dispatcher, StopSelectedWorkspaceAsync);

    private CqrsRelayCommand<bool>? _copySelectedKeyCommand;
    public CqrsRelayCommand<bool> CopySelectedKeyCommand => _copySelectedKeyCommand ??= CqrsRelayFactory.Create(_dispatcher, CopySelectedKeyAsync);
}
