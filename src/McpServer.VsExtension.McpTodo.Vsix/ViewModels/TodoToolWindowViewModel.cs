using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using McpServer.UI.Core.Services;
using McpServer.VsExtension.McpTodo;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell;
using UiCoreTodoDetailViewModel = McpServer.UI.Core.ViewModels.TodoDetailViewModel;
using UiCoreTodoListEntry = McpServer.UI.Core.ViewModels.TodoListEntry;
using UiCoreTodoListHostViewModel = McpServer.UI.Core.ViewModels.TodoListHostViewModel;
using UiCoreTodoListViewModel = McpServer.UI.Core.ViewModels.TodoListViewModel;
using UiCoreWorkspaceContextViewModel = McpServer.UI.Core.ViewModels.WorkspaceContextViewModel;

namespace McpServer.UI;

internal sealed class TodoToolWindowViewModel : UiCoreTodoListHostViewModel
{
    private readonly TodoEditorService _editorService;
    private CancellationTokenSource? _copilotCts;
    private readonly Action<string>? _openFileInEditor;
    private readonly Action<string, string>? _showCompletionInfoBar;

    public TodoToolWindowViewModel(
        TodoEditorService editorService,
        IClipboardService clipboardService,
        UiCoreTodoListViewModel listVm,
        UiCoreTodoDetailViewModel detailVm,
        UiCoreWorkspaceContextViewModel workspaceContext,
        IServiceProvider serviceProvider,
        ITimerService timerService,
        ILogger<McpServer.UI.Core.ViewModels.TodoListHostViewModel> logger,
        Action<string>? openFileInEditor = null,
        Action<string, string>? showCompletionInfoBar = null)
        : base(clipboardService, listVm, detailVm, workspaceContext, serviceProvider, timerService, logger)
    {
        _editorService = editorService ?? throw new ArgumentNullException(nameof(editorService));
        _openFileInEditor = openFileInEditor;
        _showCompletionInfoBar = showCompletionInfoBar;

        _editorService.TodoSaved += OnTodoSaved;
        PropertyChanged += OnViewModelPropertyChanged;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        NewTodoCommand = new RelayCommand(NewTodo);
        CopyIdCommand = new AsyncRelayCommand(CopySelectedIdAsync);
        StopCommand = new RelayCommand(Stop);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        OpenItemCommand = new AsyncRelayCommand(OpenSelectedTodoAsync);

        SyncFilteredItems();
    }

    public ObservableCollection<UiCoreTodoListEntry> FilteredItems { get; } = [];

    public string FilterPriority
    {
        get => SelectedPriorityIndex switch
        {
            1 => "high",
            2 => "medium",
            3 => "low",
            _ => string.Empty
        };
        set
        {
            var nextValue = value?.Trim().ToLowerInvariant() switch
            {
                "high" => 1,
                "medium" => 2,
                "low" => 3,
                _ => 0
            };

            if (SelectedPriorityIndex != nextValue)
                SelectedPriorityIndex = nextValue;
        }
    }

    public string FilterTextScope
    {
        get => SelectedScopeIndex switch
        {
            1 => "id",
            2 => "all",
            _ => "title"
        };
        set
        {
            var nextValue = value?.Trim().ToLowerInvariant() switch
            {
                "id" => 1,
                "all" => 2,
                _ => 0
            };

            if (SelectedScopeIndex != nextValue)
                SelectedScopeIndex = nextValue;
        }
    }

    public bool IsStopEnabled => IsCopilotRunning;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand NewTodoCommand { get; }
    public IAsyncRelayCommand CopyIdCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand ClearFiltersCommand { get; }
    public IAsyncRelayCommand OpenItemCommand { get; }

    public override Task CopilotStatusAsync()
    {
        if (SelectedEntry?.Item == null)
            return Task.CompletedTask;

        var id = SelectedEntry.Item.Id;
        var prompt = $"Get the current status of TODO {id} from the local MCP server at http://localhost:7147. "
                   + $"Use: curl http://localhost:7147/mcpserver/todo/{id} to retrieve the item. "
                   + "Report the title, priority, section, done status, description, technical details, "
                   + "implementation tasks with completion status, and any blockers or next steps.";
        return InvokeCopilotPromptAsync(id, "Status", prompt);
    }

    public override Task CopilotPlanAsync()
    {
        if (SelectedEntry?.Item == null)
            return Task.CompletedTask;

        var id = SelectedEntry.Item.Id;
        var prompt = $"Create an implementation plan in excruciating detail as a new TODO that TODO {id} depends on. "
                   + $"First retrieve the full details of {id} from the local MCP server using: "
                   + $"curl http://localhost:7147/mcpserver/todo/{id}. "
                   + $"Then create a new TODO via POST http://localhost:7147/mcpserver/todo with the detailed plan. "
                   + $"Finally update {id} via PUT http://localhost:7147/mcpserver/todo/{id} "
                   + "to add the new plan TODO as a dependency.";
        return InvokeCopilotPromptAsync(id, "Plan", prompt);
    }

    public override Task CopilotImplementAsync()
    {
        if (SelectedEntry?.Item == null)
            return Task.CompletedTask;

        var id = SelectedEntry.Item.Id;
        var prompt = $@"Implement TODO {id}. Follow this procedure:

1. RETRIEVE: Fetch the full TODO from the local MCP server:
   curl http://localhost:7147/mcpserver/todo/{id}
   Note the implementationTasks array — each entry has {{ task, done }}.

2. IMPLEMENT TASKS: Work through each implementationTask that has done=false.
   After completing each task, immediately update the TODO via PUT to mark
   that specific task done. Send the FULL implementationTasks array with the
   completed task's done field set to true:
   curl -X PUT http://localhost:7147/mcpserver/todo/{id} \
     -H ""Content-Type: application/json"" \
     -d '{{""implementationTasks"": [ ...full array with updated done flags... ]}}'
   This makes progress visible in the tree view in real time.

3. UPDATE DEPENDENTS: After all tasks are complete, query all TODOs:
   curl http://localhost:7147/mcpserver/todo
   Find any TODO whose dependsOn array contains ""{id}"". For each dependent:
   - Update its technicalDetails or note to reflect that {id} is now complete.
   - If all of the dependent's own dependencies are satisfied, update its
     remaining estimate and note accordingly.

4. MARK DONE: When all implementationTasks are done, mark the TODO itself done:
   curl -X PUT http://localhost:7147/mcpserver/todo/{id} \
     -H ""Content-Type: application/json"" \
     -d '{{""done"": true}}'

5. Update the session log throughout. Run to completion, do not wait for user.
   The project is at E:\github\FunWasHad.";
        return InvokeCopilotPromptAsync(id, "Implement", prompt);
    }

    protected override void StopAction()
    {
        _copilotCts?.Cancel();
        StatusText = "Stopped";
    }

    protected override void NewTodo()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _editorService.OpenNewTodo();
        StatusText = "Opened new TODO template.";
    }

    protected override async Task OpenSelectedTodoAsync()
    {
        if (SelectedEntry?.Item == null)
            return;

        await _editorService.OpenTodoAsync(SelectedEntry.Item.Id).ConfigureAwait(true);
        StatusText = $"Opened {SelectedEntry.Item.Id}";
    }

    private void Stop() => StopAction();

    private void OnTodoSaved() => RefreshCommand.Execute(null);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GroupedItems):
                SyncFilteredItems();
                break;
            case nameof(SelectedPriorityIndex):
                OnPropertyChanged(nameof(FilterPriority));
                break;
            case nameof(SelectedScopeIndex):
                OnPropertyChanged(nameof(FilterTextScope));
                break;
            case nameof(IsCopilotRunning):
                OnPropertyChanged(nameof(IsStopEnabled));
                break;
        }
    }

    private void SyncFilteredItems()
    {
        FilteredItems.Clear();
        foreach (var entry in GroupedItems.SelectMany(static group => group.Items))
            FilteredItems.Add(entry);
    }

    private async Task InvokeCopilotPromptAsync(string id, string action, string prompt)
    {
        StatusText = $"{action} {id}…";
        _copilotCts?.Dispose();
        _copilotCts = new CancellationTokenSource();
        IsCopilotRunning = true;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "McpServer-McpTodo");
            Directory.CreateDirectory(tempDir);
            var mdPath = Path.Combine(tempDir, $"{action}-{id}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            File.WriteAllText(mdPath, $"# {action}: {id}\n\n_Running…_\n");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _openFileInEditor?.Invoke(mdPath);

            var firstLine = true;
            void OnLine(string line)
            {
                if (firstLine)
                {
                    File.WriteAllText(mdPath, $"# {action}: {id}\n\n{line}\n");
                    firstLine = false;
                    return;
                }

                File.AppendAllText(mdPath, line + Environment.NewLine);
            }

            var result = await CopilotCliHelper.InvokeAsync(prompt, OnLine, _copilotCts.Token).ConfigureAwait(true);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (result.State == "cancelled")
            {
                StatusText = $"{action} {id} stopped";
            }
            else if (result.State == "success")
            {
                StatusText = $"{action} {id} complete";
                _showCompletionInfoBar?.Invoke($"{action} {id} complete", mdPath);
            }
            else
            {
                StatusText = $"{action} {id}: {result.State}";
                CopilotOutputPane.Log($"Copilot CLI returned {result.State} for {action} {id}: {result.Stderr ?? result.Body}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = $"{action} {id} stopped";
        }
        catch (InvalidOperationException ex)
        {
            CopilotOutputPane.Log($"Copilot CLI failed ({action} {id}): {ex.Message}");
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            StatusText = $"Copilot unavailable for {action} {id}";
        }
        finally
        {
            IsCopilotRunning = false;
            _copilotCts?.Dispose();
            _copilotCts = null;
        }
    }
}
