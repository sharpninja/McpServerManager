using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RequestTracker.Core.Commands;
using RequestTracker.Core.Cqrs;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;

namespace RequestTracker.Core.ViewModels;

public partial class TodoListViewModel : ViewModelBase
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("TodoListViewModel");
    private readonly Mediator _mediator = new();
    private readonly IClipboardService _clipboardService;
    private List<TodoListEntry> _allEntries = new();
    private CancellationTokenSource? _activeCts;

    // ── Observable properties ───────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TodoListGroup> _groupedItems = new();
    [ObservableProperty] private TodoListEntry? _selectedEntry;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isLoading;

    // Filters
    [ObservableProperty] private int _selectedPriorityIndex;
    [ObservableProperty] private int _selectedScopeIndex;
    [ObservableProperty] private string _filterText = "";

    // New-todo form
    [ObservableProperty] private bool _isCreatingNew;
    [ObservableProperty] private string _newTodoTitle = "";
    [ObservableProperty] private int _newTodoPriorityIndex = 1; // 0=High, 1=Medium, 2=Low

    // Editor
    [ObservableProperty] private string _editorText = "";
    [ObservableProperty] private string _editorTitle = "";
    [ObservableProperty] private bool _isEditorVisible;
    [ObservableProperty] private double _editorFontSize = 13;
    [ObservableProperty] private bool _isCopilotRunning;

    /// <summary>Set by the view code-behind to read current TextEditor content.</summary>
    public Func<string>? GetEditorText { get; set; }

    public static IReadOnlyList<string> PriorityOptions { get; } = new[] { "All", "High", "Medium", "Low" };
    public static IReadOnlyList<string> ScopeOptions { get; } = new[] { "Title", "ID", "All Fields" };
    public static IReadOnlyList<string> NewPriorityOptions { get; } = new[] { "High", "Medium", "Low" };

    // ── Constructor ─────────────────────────────────────────────────────────

    public TodoListViewModel(IClipboardService clipboardService, string mcpBaseUrl)
    {
        _clipboardService = clipboardService;
        RegisterCqrsHandlers(new McpTodoService(mcpBaseUrl));
    }

    public TodoListViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        RegisterCqrsHandlers(new McpTodoService(AppSettings.ResolveMcpBaseUrl()));
    }

    private void RegisterCqrsHandlers(McpTodoService service)
    {
        _mediator.IsBusyChanged += busy =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = busy);
        };
        _mediator.RegisterQuery(new QueryTodosHandler(service));
        _mediator.RegisterQuery(new GetTodoByIdHandler(service));
        _mediator.Register<CreateTodoCommand, McpTodoMutationResult>(new CreateTodoHandler(service));
        _mediator.Register<UpdateTodoCommand, McpTodoMutationResult>(new UpdateTodoHandler(service));
        _mediator.Register<DeleteTodoCommand, McpTodoMutationResult>(new DeleteTodoHandler(service));
        _mediator.Register<AnalyzeTodoRequirementsCommand, McpRequirementsAnalysisResult>(new AnalyzeTodoRequirementsHandler(service));
    }

    // ── Filter change triggers ──────────────────────────────────────────────

    partial void OnSelectedPriorityIndexChanged(int value) => ApplyFilters();
    partial void OnSelectedScopeIndexChanged(int value) => ApplyFilters();
    partial void OnFilterTextChanged(string value) => ApplyFilters();

    // ── Commands ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadTodosAsync()
    {
        IsLoading = true;
        StatusText = "Loading…";
        try
        {
            var result = await _mediator.QueryAsync<QueryTodosQuery, McpTodoQueryResult>(
                new QueryTodosQuery { Done = false });

            _allEntries = BuildEntries(result.Items);
            ApplyFilters();
            StatusText = $"{result.TotalCount} item(s)";
        }
        catch (Exception ex)
        {
            _allEntries = new List<TodoListEntry>();
            ApplyFilters();
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadTodosAsync();

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedPriorityIndex = 0;
        SelectedScopeIndex = 0;
        FilterText = "";
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
        try
        {
            var result = await _mediator.SendAsync<AnalyzeTodoRequirementsCommand, McpRequirementsAnalysisResult>(
                new AnalyzeTodoRequirementsCommand(item.Id));
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
        try
        {
            var fresh = await _mediator.QueryAsync<GetTodoByIdQuery, McpTodoFlatItem?>(
                new GetTodoByIdQuery(item.Id));
            if (fresh != null)
            {
                EditorText = TodoMarkdown.ToMarkdown(fresh);
                EditorTitle = fresh.Id;
            }
            else
            {
                StatusText = $"Todo {item.Id} not found";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening: {ex.Message}";
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
                    EditorTitle = newId;
                    await LoadTodosAsync();
                }
                else
                {
                    StatusText = $"Create failed: {result.Error}";
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
                    await LoadTodosAsync();
                }
                else
                {
                    StatusText = $"Save failed: {result.Error}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearEditor()
    {
        EditorText = "";
        EditorTitle = "";
    }

    [RelayCommand]
    private async Task RefreshEditorAsync()
    {
        if (string.IsNullOrEmpty(EditorTitle)) return;
        try
        {
            var fresh = await _mediator.QueryAsync<GetTodoByIdQuery, McpTodoFlatItem?>(
                new GetTodoByIdQuery(EditorTitle));
            if (fresh != null)
            {
                EditorText = TodoMarkdown.ToMarkdown(fresh);
                StatusText = $"Refreshed {EditorTitle}";
            }
            else
            {
                StatusText = $"Todo {EditorTitle} not found";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error refreshing: {ex.Message}";
        }
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
        await RunCopilotCommandAsync("status", item,
            $"Analyze the current status of this todo item. Summarize what has been done, what remains, and any blockers.\n\n{TodoMarkdown.ToMarkdown(item)}");
    }

    [RelayCommand]
    private async Task CopilotPlanAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        await RunCopilotCommandAsync("plan", item,
            $"Create a detailed implementation plan for this todo item. Break it down into concrete steps with technical details.\n\n{TodoMarkdown.ToMarkdown(item)}");
    }

    [RelayCommand]
    private async Task CopilotImplementAsync()
    {
        if (SelectedEntry?.Item is not { } item) return;
        await RunCopilotCommandAsync("implement", item,
            $"Implement this todo item. Make the necessary code changes.\n\n{TodoMarkdown.ToMarkdown(item)}");
    }

    private async Task RunCopilotCommandAsync(string action, McpTodoFlatItem item, string prompt)
    {
        _activeCts?.Cancel();
        _activeCts = new CancellationTokenSource();
        var ct = _activeCts.Token;

        IsCopilotRunning = true;
        EditorTitle = $"{item.Id} — copilot {action}";
        EditorText = $"⏳ Running copilot {action} for {item.Id}…\n";
        StatusText = $"Copilot {action}: {item.Id}…";

        var sb = new StringBuilder();
        sb.AppendLine($"⏳ Running copilot {action} for {item.Id}…");
        sb.AppendLine();

        try
        {
            var result = await CopilotCliService.InvokeAsync(
                prompt,
                workingDirectory: null,
                onStdoutLine: line =>
                {
                    sb.AppendLine(line);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorText = sb.ToString());
                },
                cancellationToken: ct);

            sb.AppendLine();
            sb.AppendLine(result.State == "success"
                ? $"✅ Copilot {action} completed."
                : $"⚠️ Copilot {action} finished with state: {result.State}");

            if (!string.IsNullOrWhiteSpace(result.Stderr))
            {
                sb.AppendLine();
                sb.AppendLine("--- stderr ---");
                sb.AppendLine(result.Stderr.Trim());
            }

            EditorText = sb.ToString();
            StatusText = result.State == "success"
                ? $"Copilot {action} done: {item.Id}"
                : $"Copilot {action} {result.State}: {item.Id}";
        }
        catch (OperationCanceledException)
        {
            sb.AppendLine();
            sb.AppendLine("🛑 Cancelled.");
            EditorText = sb.ToString();
            StatusText = $"Copilot {action} cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot {Action} failed for {Id}", action, item.Id);
            sb.AppendLine();
            sb.AppendLine($"❌ Error: {ex.Message}");
            EditorText = sb.ToString();
            StatusText = $"Copilot error: {ex.Message}";
        }
        finally
        {
            IsCopilotRunning = false;
        }
    }

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
            source = source.Where(e => MatchesTextFilter(e.Item, text, scopeTag));
        }

        var filtered = source.ToList();

        // Group by priority
        var groups = filtered
            .GroupBy(e => e.PriorityGroup)
            .OrderBy(g => PrioritySortKey(g.First().Item?.Priority))
            .Select(g => new TodoListGroup(g.Key, new ObservableCollection<TodoListEntry>(g)))
            .ToList();

        GroupedItems = new ObservableCollection<TodoListGroup>(groups);
    }

    private static bool MatchesTextFilter(McpTodoFlatItem? item, string filterText, string scope)
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
        return searchable.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
