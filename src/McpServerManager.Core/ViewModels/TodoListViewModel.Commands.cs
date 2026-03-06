using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class TodoListViewModel
{
    private CqrsRelayCommand<bool>? _loadTodosCommand;
    public CqrsRelayCommand<bool> LoadTodosCommand => _loadTodosCommand ??= CqrsRelayFactory.Create(_dispatcher, LoadTodosAsync);

    private CqrsRelayCommand<bool>? _refreshCommand;
    public CqrsRelayCommand<bool> RefreshCommand => _refreshCommand ??= CqrsRelayFactory.Create(_dispatcher, RefreshAsync);

    private CqrsRelayCommand<bool>? _clearFiltersCommand;
    public CqrsRelayCommand<bool> ClearFiltersCommand => _clearFiltersCommand ??= CqrsRelayFactory.Create(_dispatcher, ClearFilters);

    private CqrsRelayCommand<bool>? _copySelectedIdCommand;
    public CqrsRelayCommand<bool> CopySelectedIdCommand => _copySelectedIdCommand ??= CqrsRelayFactory.Create(_dispatcher, CopySelectedIdAsync);

    private CqrsRelayCommand<bool>? _toggleDoneCommand;
    public CqrsRelayCommand<bool> ToggleDoneCommand => _toggleDoneCommand ??= CqrsRelayFactory.Create(_dispatcher, ToggleDoneAsync);

    private CqrsRelayCommand<bool>? _deleteSelectedCommand;
    public CqrsRelayCommand<bool> DeleteSelectedCommand => _deleteSelectedCommand ??= CqrsRelayFactory.Create(_dispatcher, DeleteSelectedAsync);

    private CqrsRelayCommand<bool>? _analyzeRequirementsCommand;
    public CqrsRelayCommand<bool> AnalyzeRequirementsCommand => _analyzeRequirementsCommand ??= CqrsRelayFactory.Create(_dispatcher, AnalyzeRequirementsAsync);

    private CqrsRelayCommand<bool>? _stopActionCommand;
    public CqrsRelayCommand<bool> StopActionCommand => _stopActionCommand ??= CqrsRelayFactory.Create(_dispatcher, StopAction);

    private CqrsRelayCommand<bool>? _newTodoCommand;
    public CqrsRelayCommand<bool> NewTodoCommand => _newTodoCommand ??= CqrsRelayFactory.Create(_dispatcher, NewTodo);

    private CqrsRelayCommand<bool>? _cancelNewTodoCommand;
    public CqrsRelayCommand<bool> CancelNewTodoCommand => _cancelNewTodoCommand ??= CqrsRelayFactory.Create(_dispatcher, CancelNewTodo);

    private CqrsRelayCommand<bool>? _saveNewTodoCommand;
    public CqrsRelayCommand<bool> SaveNewTodoCommand => _saveNewTodoCommand ??= CqrsRelayFactory.Create(_dispatcher, SaveNewTodoAsync);

    private CqrsRelayCommand<bool>? _openSelectedTodoCommand;
    public CqrsRelayCommand<bool> OpenSelectedTodoCommand => _openSelectedTodoCommand ??= CqrsRelayFactory.Create(_dispatcher, OpenSelectedTodoAsync);

    private CqrsRelayCommand<bool>? _saveEditorCommand;
    public CqrsRelayCommand<bool> SaveEditorCommand => _saveEditorCommand ??= CqrsRelayFactory.Create(_dispatcher, SaveEditorAsync);

    private CqrsRelayCommand<bool>? _clearEditorCommand;
    public CqrsRelayCommand<bool> ClearEditorCommand => _clearEditorCommand ??= CqrsRelayFactory.Create(_dispatcher, ClearEditor);

    private CqrsRelayCommand<bool>? _refreshEditorCommand;
    public CqrsRelayCommand<bool> RefreshEditorCommand => _refreshEditorCommand ??= CqrsRelayFactory.Create(_dispatcher, RefreshEditorAsync);

    private CqrsRelayCommand<bool>? _editorZoomInCommand;
    public CqrsRelayCommand<bool> EditorZoomInCommand => _editorZoomInCommand ??= CqrsRelayFactory.Create(_dispatcher, EditorZoomIn);

    private CqrsRelayCommand<bool>? _editorZoomOutCommand;
    public CqrsRelayCommand<bool> EditorZoomOutCommand => _editorZoomOutCommand ??= CqrsRelayFactory.Create(_dispatcher, EditorZoomOut);

    private CqrsRelayCommand<bool>? _openAiChatCommand;
    public CqrsRelayCommand<bool> OpenAiChatCommand => _openAiChatCommand ??= CqrsRelayFactory.Create(_dispatcher, OpenAiChat);
}
