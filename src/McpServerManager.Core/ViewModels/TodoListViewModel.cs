using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.ViewModels;

public partial class TodoListViewModel : ViewModelBase
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("TodoListViewModel");
    private readonly Mediator _mediator = new();
    private readonly IClipboardService _clipboardService;
    private List<TodoListEntry> _allEntries = new();
    private CancellationTokenSource? _activeCts;
    private bool _isBusyHandlerRegistered;

    // ── Observable properties ───────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TodoListGroup> _groupedItems = new();
    [ObservableProperty] private TodoListEntry? _selectedEntry;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isLoading;

    // Filters
    [ObservableProperty] private int _selectedPriorityIndex;
    [ObservableProperty] private int _selectedScopeIndex;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _includeCompleted;

    // New-todo form
    [ObservableProperty] private bool _isCreatingNew;
    [ObservableProperty] private string _newTodoTitle = "";
    [ObservableProperty] private int _newTodoPriorityIndex = 1; // 0=High, 1=Medium, 2=Low

    // Editor
    [ObservableProperty] private string _editorText = "";
    [ObservableProperty] private string _editorTitle = "";
    [ObservableProperty] private double _editorFontSize = 13;
    [ObservableProperty] private bool _isCopilotRunning;
    [ObservableProperty] private McpTodoFlatItem? _currentTodoDetail;

    /// <summary>Set by the view code-behind to read current TextEditor content.</summary>
    public Func<string>? GetEditorText { get; set; }

    /// <summary>Raised when a status message should appear on the global status bar.</summary>
    public event Action<string>? GlobalStatusChanged;

    public static IReadOnlyList<string> PriorityOptions { get; } = new[] { "All", "High", "Medium", "Low" };
    public static IReadOnlyList<string> ScopeOptions { get; } = new[] { "Title", "ID", "All Fields" };
    public static IReadOnlyList<string> NewPriorityOptions { get; } = new[] { "High", "Medium", "Low" };

    // ── Constructor ─────────────────────────────────────────────────────────

    public TodoListViewModel(IClipboardService clipboardService, string mcpBaseUrl, string? mcpApiKey = null)
    {
        _clipboardService = clipboardService;
        SetMcpBaseUrl(mcpBaseUrl, mcpApiKey);
    }

    public TodoListViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        SetMcpBaseUrl(AppSettings.ResolveMcpBaseUrl());
    }

    private void RegisterCqrsHandlers(McpTodoService service)
    {
        if (!_isBusyHandlerRegistered)
        {
            _mediator.IsBusyChanged += busy =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = busy);
            };
            _isBusyHandlerRegistered = true;
        }

        _mediator.RegisterQuery(new QueryTodosHandler(service));
        _mediator.RegisterQuery(new GetTodoByIdHandler(service));
        _mediator.Register<CreateTodoCommand, McpTodoMutationResult>(new CreateTodoHandler(service));
        _mediator.Register<UpdateTodoCommand, McpTodoMutationResult>(new UpdateTodoHandler(service));
        _mediator.Register<DeleteTodoCommand, McpTodoMutationResult>(new DeleteTodoHandler(service));
        _mediator.Register<AnalyzeTodoRequirementsCommand, McpRequirementsAnalysisResult>(new AnalyzeTodoRequirementsHandler(service));
        _mediator.Register<StreamTodoPromptCommand, IAsyncEnumerable<string>>(new StreamTodoPromptHandler(service));
    }

    public void SetMcpBaseUrl(string mcpBaseUrl, string? mcpApiKey = null, string? workspaceRootPath = null)
    {
        RegisterCqrsHandlers(new McpTodoService(mcpBaseUrl, mcpApiKey, workspaceRootPath));
    }

    public Task RefreshForConnectionChangeAsync() => LoadTodosAsync();

    // ── Filter change triggers ──────────────────────────────────────────────

    partial void OnSelectedPriorityIndexChanged(int value) => ApplyFilters();
    partial void OnSelectedScopeIndexChanged(int value) => ApplyFilters();
    partial void OnFilterTextChanged(string value) => ApplyFilters();
    partial void OnIncludeCompletedChanged(bool value) => _ = LoadTodosAsync();

    // ── Commands ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadTodosAsync() => await LoadTodosCoreAsync(forceEditorReload: false);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadTodosCoreAsync(forceEditorReload: true);

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedPriorityIndex = 0;
        SelectedScopeIndex = 0;
        FilterText = "";
        IncludeCompleted = false;
    }

    [RelayCommand]
    private async Task CopySelectedIdAsync()
    {
        if (SelectedEntry?.Item is { } item)
        {
            await _clipboardService.SetTextAsync(item.Id);
            StatusText = "Copied " + item.Id;
        }
    }

    [RelayCommand]
    private async Task ToggleDoneAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        try
        {
            var result = await _mediator.SendAsync<UpdateTodoCommand, McpTodoMutationResult>(
                new UpdateTodoCommand(item.Id, new McpTodoUpdateRequest { Done = !item.Done }));
            if (result.Success)
            {
                StatusText = item.Done ? $"Reopened {item.Id}" : $"Completed {item.Id}";
                await LoadTodosAsync();
                if (string.Equals(CurrentTodoDetail?.Id, item.Id, StringComparison.OrdinalIgnoreCase))
                    await TryRefreshEditorByIdAsync(item.Id, updateStatus: false);
            }
            else
            {
                StatusText = $"Failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        try
        {
            var result = await _mediator.SendAsync<DeleteTodoCommand, McpTodoMutationResult>(
                new DeleteTodoCommand(item.Id));
            if (result.Success)
            {
                StatusText = $"Deleted {item.Id}";
                if (string.Equals(CurrentTodoDetail?.Id, item.Id, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentTodoDetail = null;
                    EditorText = "";
                    EditorTitle = "";
                }
                await LoadTodosAsync();
            }
            else
            {
                StatusText = $"Delete failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AnalyzeRequirementsAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        StatusText = $"Analyzing {item.Id}…";
        _activeCts?.Cancel();
        _activeCts = new CancellationTokenSource();
        var ct = _activeCts.Token;
        try
        {
            var result = await _mediator.SendAsync<AnalyzeTodoRequirementsCommand, McpRequirementsAnalysisResult>(
                new AnalyzeTodoRequirementsCommand(item.Id), ct);
            if (result.Success)
            {
                StatusText = $"Requirements for {item.Id}: {(result.FunctionalRequirements?.Count ?? 0)} functional, {(result.TechnicalRequirements?.Count ?? 0)} technical";
            }
            else
            {
                StatusText = $"Analysis failed: {result.Error}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Canceled analysis for {item.Id}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopAction()
    {
        _activeCts?.Cancel();
        StatusText = "Stopped";
    }

    [RelayCommand]
    private void NewTodo()
    {
        EditorText = TodoMarkdown.BlankTemplate();
        EditorTitle = "NEW-TODO";
    }

    [RelayCommand]
    private void CancelNewTodo()
    {
        IsCreatingNew = false;
        NewTodoTitle = "";
    }

    [RelayCommand]
    private async Task SaveNewTodoAsync()
    {
        var title = (NewTodoTitle ?? "").Trim();
        if (string.IsNullOrEmpty(title))
        {
            StatusText = "Title is required";
            return;
        }

        var priority = NewPriorityOptions[NewTodoPriorityIndex];
        var id = $"TODO-{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            var result = await _mediator.SendAsync<CreateTodoCommand, McpTodoMutationResult>(
                new CreateTodoCommand(new McpTodoCreateRequest
                {
                    Id = id,
                    Title = title,
                    Priority = priority.ToLowerInvariant(),
                    Section = "general"
                }));

            if (result.Success)
            {
                StatusText = $"Created {id}";
                IsCreatingNew = false;
                NewTodoTitle = "";
                await LoadTodosAsync();
            }
            else
            {
                StatusText = $"Create failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenSelectedTodoAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        GlobalStatusChanged?.Invoke($"Opening {item.Id}…");
        try
        {
            var fresh = await _mediator.QueryAsync<GetTodoByIdQuery, McpTodoFlatItem?>(
                new GetTodoByIdQuery(item.Id));
            if (fresh != null)
            {
                CurrentTodoDetail = fresh;
                EditorText = TodoMarkdown.ToMarkdown(fresh);
                EditorTitle = fresh.Id;
                GlobalStatusChanged?.Invoke($"Opened {fresh.Id}.");
            }
            else
            {
                CurrentTodoDetail = null;
                StatusText = $"Todo {item.Id} not found";
                GlobalStatusChanged?.Invoke($"Todo {item.Id} not found.");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening: {ex.Message}";
            GlobalStatusChanged?.Invoke($"Error opening todo: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        var text = GetEditorText?.Invoke() ?? EditorText;
        if (string.IsNullOrWhiteSpace(text)) return;

        var id = TodoMarkdown.ExtractId(text);
        var isNew = string.IsNullOrEmpty(id)
                    || string.Equals(id, "NEW-TODO", StringComparison.OrdinalIgnoreCase);

        GlobalStatusChanged?.Invoke(isNew ? "Creating todo…" : $"Saving {id}…");
        try
        {
            if (isNew)
            {
                // New todo: generate a real ID and create via MCP
                var newId = $"TODO-{DateTime.UtcNow:yyyyMMddHHmmss}";
                var updateReq = TodoMarkdown.FromMarkdown(text);
                var createReq = new McpTodoCreateRequest
                {
                    Id = newId,
                    Title = updateReq.Title ?? "Untitled",
                    Priority = updateReq.Priority ?? "medium",
                    Section = updateReq.Section ?? "general",
                    Description = updateReq.Description,
                    TechnicalDetails = updateReq.TechnicalDetails,
                    ImplementationTasks = updateReq.ImplementationTasks,
                    DependsOn = updateReq.DependsOn,
                    Estimate = updateReq.Estimate
                };

                var result = await _mediator.SendAsync<CreateTodoCommand, McpTodoMutationResult>(
                    new CreateTodoCommand(createReq));
                if (result.Success)
                {
                    StatusText = $"Created {newId}";
                    GlobalStatusChanged?.Invoke($"Created todo {newId}.");
                    EditorTitle = newId;
                    await LoadTodosAsync();
                    await TryRefreshEditorByIdAsync(newId, updateStatus: false);
                }
                else
                {
                    StatusText = $"Create failed: {result.Error}";
                    GlobalStatusChanged?.Invoke($"Create failed: {result.Error}");
                }
            }
            else
            {
                var updateReq = TodoMarkdown.FromMarkdown(text);
                var result = await _mediator.SendAsync<UpdateTodoCommand, McpTodoMutationResult>(
                    new UpdateTodoCommand(id!, updateReq));
                if (result.Success)
                {
                    StatusText = $"Saved {id}";
                    GlobalStatusChanged?.Invoke($"Saved todo {id}.");
                    await LoadTodosAsync();
                    await TryRefreshEditorByIdAsync(id!, updateStatus: false);
                }
                else
                {
                    StatusText = $"Save failed: {result.Error}";
                    GlobalStatusChanged?.Invoke($"Save failed: {result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
            GlobalStatusChanged?.Invoke($"Error saving todo: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearEditor()
    {
        EditorText = "";
        EditorTitle = "";
        CurrentTodoDetail = null;
    }

    [RelayCommand]
    private async Task RefreshEditorAsync()
    {
        var editorTodoId = GetCurrentEditorTodoId();
        if (string.IsNullOrWhiteSpace(editorTodoId)) return;
        await TryRefreshEditorByIdAsync(editorTodoId, updateStatus: true);
    }

    [RelayCommand]
    private void EditorZoomIn() => EditorFontSize = Math.Min(EditorFontSize + 2, 48);

    [RelayCommand]
    private void EditorZoomOut() => EditorFontSize = Math.Max(EditorFontSize - 2, 8);

    // ── AI Chat ─────────────────────────────────────────────────────────────

    /// <summary>Raised when the user clicks the AI button; platform code-behind opens the chat window.</summary>
    public event EventHandler? OpenAiChatRequested;

    [RelayCommand]
    private void OpenAiChat() => OpenAiChatRequested?.Invoke(this, EventArgs.Empty);

    // ── Copilot CLI Commands ────────────────────────────────────────────────

    [RelayCommand]
    private async Task CopilotStatusAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        await RunTodoPromptCommandAsync("status", item, TodoPromptActionKind.Status);
    }

    [RelayCommand]
    private async Task CopilotPlanAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        await RunTodoPromptCommandAsync("plan", item, TodoPromptActionKind.Plan);
    }

    [RelayCommand]
    private async Task CopilotImplementAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        await RunTodoPromptCommandAsync("implement", item, TodoPromptActionKind.Implement);
    }

    private async Task RunTodoPromptCommandAsync(string action, McpTodoFlatItem item, TodoPromptActionKind promptAction)
    {
        _activeCts?.Cancel();
        _activeCts = new CancellationTokenSource();
        var ct = _activeCts.Token;

        IsCopilotRunning = true;
        EditorTitle = $"{item.Id} — {action} prompt";
        EditorText = $"⏳ Requesting {action} prompt for {item.Id} from MCP server…\n";
        StatusText = $"{Capitalize(action)} prompt: {item.Id}…";

        var sb = new StringBuilder();
        sb.AppendLine($"⏳ Requesting {action} prompt for {item.Id} from MCP server…");
        sb.AppendLine();

        try
        {
            var stream = await _mediator.SendAsync<StreamTodoPromptCommand, IAsyncEnumerable<string>>(
                new StreamTodoPromptCommand(item.Id, promptAction), ct);

            var receivedAnyLine = false;
            var receivedErrorLine = false;
            var uiFlushStopwatch = Stopwatch.StartNew();
            var linesSinceUiFlush = 0;
            await foreach (var line in stream.WithCancellation(ct))
            {
                receivedAnyLine = true;
                if (!string.IsNullOrWhiteSpace(line) &&
                    line.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                {
                    receivedErrorLine = true;
                }

                sb.AppendLine(line);
                linesSinceUiFlush++;

                // Flush to the UI thread periodically to show real-time streaming.
                // The await foreach runs on a threadpool thread (SSE reader uses ConfigureAwait(false)),
                // so we must dispatch EditorText updates explicitly.
                if (linesSinceUiFlush >= 4 || uiFlushStopwatch.ElapsedMilliseconds >= 80)
                {
                    var snapshot = sb.ToString();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorText = snapshot);
                    linesSinceUiFlush = 0;
                    uiFlushStopwatch.Restart();
                }
            }

            if (!receivedAnyLine)
            {
                sb.AppendLine("❌ Error: MCP server returned no prompt output.");
                sb.AppendLine("The server likely failed before emitting any SSE data.");
                var text = sb.ToString();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    EditorText = text;
                    StatusText = $"{Capitalize(action)} prompt error: no output from MCP server";
                });
            }
            else if (receivedErrorLine)
            {
                sb.AppendLine();
                sb.AppendLine($"❌ {Capitalize(action)} prompt failed.");
                var text = sb.ToString();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    EditorText = text;
                    StatusText = $"{Capitalize(action)} prompt error: {item.Id}";
                });
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine($"✅ {Capitalize(action)} prompt completed.");
                var text = sb.ToString();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    EditorText = text;
                    StatusText = $"{Capitalize(action)} prompt done: {item.Id}";
                });
            }
        }
        catch (OperationCanceledException)
        {
            sb.AppendLine();
            sb.AppendLine("🛑 Cancelled.");
            var text = sb.ToString();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                EditorText = text;
                StatusText = $"{Capitalize(action)} prompt cancelled";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TODO prompt {Action} failed for {Id}", action, item.Id);
            sb.AppendLine();
            sb.AppendLine($"❌ Error: {ex.Message}");
            var text = sb.ToString();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                EditorText = text;
                StatusText = $"{Capitalize(action)} prompt error: {ex.Message}";
            });
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsCopilotRunning = false);
        }
    }

    private static string Capitalize(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

    /// <summary>Builds a context string with current todo data for the AI assistant.</summary>
    public string GetTodoContextForAgent()
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- Todo context ---");

        // Current editor content (the open todo)
        var editorText = GetEditorText?.Invoke() ?? EditorText;
        if (!string.IsNullOrWhiteSpace(editorText))
        {
            sb.AppendLine();
            sb.AppendLine("## Currently open todo (YAML):");
            sb.AppendLine(editorText.Trim());
        }

        // Full list summary
        if (_allEntries.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Todo list summary:");
            foreach (var entry in _allEntries)
            {
                var item = entry.Item;
                if (item == null) continue;
                var doneTag = item.Done ? " [DONE]" : "";
                sb.AppendLine($"- {item.Id} | {item.Priority} | {item.Title}{doneTag}");
                if (item.Description is { Count: > 0 })
                {
                    foreach (var d in item.Description)
                        sb.AppendLine($"    {d}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("--- End todo context ---");
        return sb.ToString();
    }

    // ── Filtering & grouping ────────────────────────────────────────────────

    private void ApplyFilters()
    {
        IEnumerable<TodoListEntry> source = _allEntries;

        // Priority filter
        var priorityTag = SelectedPriorityIndex switch
        {
            1 => "high",
            2 => "medium",
            3 => "low",
            _ => ""
        };
        if (!string.IsNullOrEmpty(priorityTag))
            source = source.Where(e => string.Equals(e.Item?.Priority, priorityTag, StringComparison.OrdinalIgnoreCase));

        // Text filter with boolean expression support
        var text = (FilterText ?? "").Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var scopeTag = SelectedScopeIndex switch
            {
                1 => "id",
                2 => "all",
                _ => "title"
            };
            var matcher = Services.BooleanSearchParser.Parse(text);
            source = source.Where(e => MatchesTextFilter(e.Item, matcher, scopeTag));
        }

        var filtered = source.ToList();

        // Group by priority, sort items within each group by ID
        var groups = filtered
            .GroupBy(e => e.PriorityGroup)
            .OrderBy(g => PrioritySortKey(g.First().Item?.Priority))
            .Select(g => new TodoListGroup(g.Key, new ObservableCollection<TodoListEntry>(
                g.OrderBy(e => e.Item?.Id, StringComparer.OrdinalIgnoreCase))))
            .ToList();

        GroupedItems = new ObservableCollection<TodoListGroup>(groups);
    }

    private static bool MatchesTextFilter(McpTodoFlatItem? item, Func<string, bool> matcher, string scope)
    {
        if (item == null) return false;
        var searchable = scope switch
        {
            "id" => item.Id ?? "",
            "title" => item.Title ?? "",
            _ => string.Join(" ",
                new[] { item.Id, item.Title, item.Section, item.Priority, item.Note, item.Estimate, item.Remaining }
                    .Concat(item.Description ?? Enumerable.Empty<string>())
                    .Concat(item.TechnicalDetails ?? Enumerable.Empty<string>())
                    .Where(s => !string.IsNullOrEmpty(s)))
        };
        return matcher(searchable);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task LoadTodosCoreAsync(bool forceEditorReload)
    {
        var previouslySelectedId = SelectedEntry?.Item?.Id;
        var editorTodoId = forceEditorReload ? GetCurrentEditorTodoId() : null;

        IsLoading = true;
        StatusText = forceEditorReload ? "Refreshing…" : "Loading…";
        GlobalStatusChanged?.Invoke(forceEditorReload ? "Refreshing todos…" : "Loading todos…");
        try
        {
            var result = await _mediator.QueryAsync<QueryTodosQuery, McpTodoQueryResult>(
                new QueryTodosQuery { Done = IncludeCompleted ? null : false });

            _allEntries = BuildEntries(result.Items);
            ApplyFilters();
            RestoreSelectionById(previouslySelectedId);

            var refreshNote = "";
            if (forceEditorReload && !string.IsNullOrWhiteSpace(editorTodoId))
            {
                var refreshed = await TryRefreshEditorByIdAsync(editorTodoId, updateStatus: false);
                refreshNote = refreshed ? " • editor refreshed" : " • editor not found";
            }

            StatusText = $"{result.TotalCount} item(s){refreshNote}";
            GlobalStatusChanged?.Invoke(forceEditorReload
                ? $"Refreshed {result.TotalCount} todo(s)."
                : $"Loaded {result.TotalCount} todo(s).");
        }
        catch (Exception ex)
        {
            _allEntries = new List<TodoListEntry>();
            ApplyFilters();
            SelectedEntry = null;
            StatusText = "Error: " + ex.Message;
            GlobalStatusChanged?.Invoke($"Todo load failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RestoreSelectionById(string? todoId)
    {
        if (string.IsNullOrWhiteSpace(todoId))
        {
            SelectedEntry = null;
            return;
        }

        SelectedEntry = _allEntries.FirstOrDefault(e =>
            string.Equals(e.Item?.Id, todoId, StringComparison.OrdinalIgnoreCase));
    }

    private string? GetCurrentEditorTodoId()
    {
        var editorContent = GetEditorText?.Invoke() ?? EditorText;
        var markdownId = TodoMarkdown.ExtractId(editorContent);
        if (!string.IsNullOrWhiteSpace(markdownId) &&
            !string.Equals(markdownId, "NEW-TODO", StringComparison.OrdinalIgnoreCase))
            return markdownId;

        if (string.IsNullOrWhiteSpace(EditorTitle) ||
            string.Equals(EditorTitle, "NEW-TODO", StringComparison.OrdinalIgnoreCase) ||
            EditorTitle.Contains(" — ", StringComparison.Ordinal))
            return null;

        return EditorTitle.Trim();
    }

    private async Task<bool> TryRefreshEditorByIdAsync(string todoId, bool updateStatus)
    {
        try
        {
            var fresh = await _mediator.QueryAsync<GetTodoByIdQuery, McpTodoFlatItem?>(
                new GetTodoByIdQuery(todoId));
            if (fresh == null)
            {
                CurrentTodoDetail = null;
                if (string.Equals(EditorTitle, todoId, StringComparison.OrdinalIgnoreCase))
                {
                    EditorText = "";
                    EditorTitle = "";
                }
                if (updateStatus)
                    StatusText = $"Todo {todoId} not found";
                return false;
            }

            CurrentTodoDetail = fresh;
            EditorText = TodoMarkdown.ToMarkdown(fresh);
            EditorTitle = fresh.Id;
            RestoreSelectionById(fresh.Id);
            if (updateStatus)
                StatusText = $"Refreshed {fresh.Id}";
            return true;
        }
        catch (Exception ex)
        {
            if (updateStatus)
                StatusText = $"Error refreshing: {ex.Message}";
            return false;
        }
    }

    private static List<TodoListEntry> BuildEntries(List<McpTodoFlatItem>? items)
    {
        if (items == null || items.Count == 0) return new();
        return items
            .Select(i => new TodoListEntry
            {
                PriorityGroup = "Priority: " + FormatPriority(i.Priority),
                DisplayLine = $"{i.Id} · {i.Priority} · {i.Title}",
                Item = i
            })
            .OrderBy(e => PrioritySortKey(e.Item?.Priority))
            .ThenBy(e => e.Item?.Id)
            .ToList();
    }

    private static string FormatPriority(string? p)
    {
        if (string.IsNullOrWhiteSpace(p)) return "Other";
        return char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant();
    }

    private static int PrioritySortKey(string? p) => (p?.Trim().ToUpperInvariant()) switch
    {
        "HIGH" => 0,
        "MEDIUM" => 1,
        "LOW" => 2,
        _ => 3
    };

    /// <summary>Calculates implementation progress as "done/total".</summary>
    public static string GetTaskProgress(McpTodoFlatItem? item)
    {
        if (item?.ImplementationTasks is not { Count: > 0 } tasks) return "";
        var done = tasks.Count(t => t.Done);
        return $"{done}/{tasks.Count}";
    }
}

// ── Display models ──────────────────────────────────────────────────────────

public sealed class TodoListEntry
{
    public string PriorityGroup { get; set; } = "";
    public string DisplayLine { get; set; } = "";
    public McpTodoFlatItem? Item { get; set; }
}

public sealed class TodoListGroup
{
    public string Name { get; }
    public ObservableCollection<TodoListEntry> Items { get; }
    public TodoListGroup(string name, ObservableCollection<TodoListEntry> items)
    {
        Name = name;
        Items = items;
    }
}
