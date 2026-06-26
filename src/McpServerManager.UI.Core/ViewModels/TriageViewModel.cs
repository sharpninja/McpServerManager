using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>ViewModel for the read-only triage dashboard.</summary>
public sealed partial class TriageViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly ILogger<TriageViewModel> _logger;

    public TriageViewModel(
        Dispatcher dispatcher,
        WorkspaceContextViewModel workspaceContext,
        ILogger<TriageViewModel> logger)
    {
        _dispatcher = dispatcher;
        _workspaceContext = workspaceContext;
        _logger = logger;
        workspaceContext.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceContextViewModel.ActiveWorkspacePath))
                _ = Task.Run(() => LoadAsync());
        };
    }

    /// <summary>Logical UI area represented by this ViewModel.</summary>
    public McpArea Area => McpArea.Triage;

    /// <summary>Optional workspace override. Empty means active workspace context, and no active workspace means global.</summary>
    [ObservableProperty]
    private string? _workspacePathFilter;

    /// <summary>Whether data is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Latest error text.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Latest status text.</summary>
    [ObservableProperty]
    private string _statusMessage = "Triage";

    /// <summary>Total group count from the dashboard projection.</summary>
    [ObservableProperty]
    private int _totalGroupCount;

    /// <summary>Total run count from the dashboard projection.</summary>
    [ObservableProperty]
    private int _totalRunCount;

    /// <summary>Total persisted triage-created TODO references before hydration.</summary>
    [ObservableProperty]
    private int _totalCreatedTodoCount;

    /// <summary>Created TODO references hidden because hydration found them completed or missing.</summary>
    [ObservableProperty]
    private int _hiddenCompletedOrMissingTodoCount;

    /// <summary>Created TODO references that failed to hydrate.</summary>
    [ObservableProperty]
    private int _todoHydrationErrorCount;

    /// <summary>Selected triage queue row index.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTriageGroup))]
    private int _selectedTriageQueueIndex = -1;

    /// <summary>Selected report-group queue row index.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReportGroup))]
    private int _selectedReportGroupIndex = -1;

    /// <summary>Selected run history row index.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRun))]
    private int _selectedRunIndex = -1;

    /// <summary>Selected open triage TODO row index.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedOpenTodo))]
    private int _selectedOpenTodoIndex = -1;

    /// <summary>Selected group detail.</summary>
    [ObservableProperty]
    private TriageGroupSnapshot? _selectedGroupDetail;

    /// <summary>Selected run detail.</summary>
    [ObservableProperty]
    private TriageRunSnapshot? _selectedRunDetail;

    /// <summary>Selected report detail.</summary>
    [ObservableProperty]
    private TriageReportSnapshot? _selectedReportDetail;

    /// <summary>Groups still collecting reports or waiting for quiet window.</summary>
    public ObservableCollection<TriageGroupSnapshot> TriageQueue { get; } = [];

    /// <summary>Groups ready for or in report-group processing.</summary>
    public ObservableCollection<TriageGroupSnapshot> ReportGroupQueue { get; } = [];

    /// <summary>AI triage run history.</summary>
    public ObservableCollection<TriageRunSnapshot> RunHistory { get; } = [];

    /// <summary>Hydrated open TODOs created by triage.</summary>
    public ObservableCollection<OpenTriageTodoItem> OpenTriageTodos { get; } = [];

    /// <summary>Currently selected triage queue group.</summary>
    public TriageGroupSnapshot? SelectedTriageGroup =>
        SelectedTriageQueueIndex >= 0 && SelectedTriageQueueIndex < TriageQueue.Count
            ? TriageQueue[SelectedTriageQueueIndex]
            : null;

    /// <summary>Currently selected report group queue group.</summary>
    public TriageGroupSnapshot? SelectedReportGroup =>
        SelectedReportGroupIndex >= 0 && SelectedReportGroupIndex < ReportGroupQueue.Count
            ? ReportGroupQueue[SelectedReportGroupIndex]
            : null;

    /// <summary>Currently selected run.</summary>
    public TriageRunSnapshot? SelectedRun =>
        SelectedRunIndex >= 0 && SelectedRunIndex < RunHistory.Count
            ? RunHistory[SelectedRunIndex]
            : null;

    /// <summary>Currently selected open triage TODO.</summary>
    public OpenTriageTodoItem? SelectedOpenTodo =>
        SelectedOpenTodoIndex >= 0 && SelectedOpenTodoIndex < OpenTriageTodos.Count
            ? OpenTriageTodos[SelectedOpenTodoIndex]
            : null;

    /// <summary>Loads dashboard queues, run history, and open triage TODOs.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading triage dashboard...";

        try
        {
            var workspacePath = ResolveWorkspacePath();
            var dashboardResult = await _dispatcher.QueryAsync(new GetTriageDashboardQuery(workspacePath), ct).ConfigureAwait(true);
            if (!dashboardResult.IsSuccess || dashboardResult.Value is null)
            {
                ErrorMessage = dashboardResult.Error ?? "Failed to load triage dashboard.";
                StatusMessage = "Triage load failed.";
                return;
            }

            var todoResult = await _dispatcher.QueryAsync(new QueryOpenTriageTodosQuery(workspacePath), ct).ConfigureAwait(true);
            if (!todoResult.IsSuccess || todoResult.Value is null)
            {
                ErrorMessage = todoResult.Error ?? "Failed to load triage-created TODOs.";
                StatusMessage = "Triage load failed.";
                return;
            }

            ReplaceCollection(TriageQueue, dashboardResult.Value.TriageQueue);
            ReplaceCollection(ReportGroupQueue, dashboardResult.Value.ReportGroupQueue);
            ReplaceCollection(RunHistory, dashboardResult.Value.RunHistory);
            ReplaceCollection(OpenTriageTodos, todoResult.Value.Items);

            TotalGroupCount = dashboardResult.Value.TotalGroupCount;
            TotalRunCount = dashboardResult.Value.TotalRunCount;
            TotalCreatedTodoCount = todoResult.Value.TotalCreatedCount;
            HiddenCompletedOrMissingTodoCount = todoResult.Value.HiddenCompletedOrMissingCount;
            TodoHydrationErrorCount = todoResult.Value.HydrationErrorCount;

            ClampSelections();
            StatusMessage = BuildLoadedStatus(workspacePath);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            ErrorMessage = ex.Message;
            StatusMessage = "Triage load failed.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Loads full detail for a triage group.</summary>
    public async Task<TriageGroupSnapshot?> LoadGroupDetailAsync(string groupId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return null;

        var result = await _dispatcher.QueryAsync(new GetTriageGroupQuery(groupId), ct).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error ?? "Failed to load triage group.";
            StatusMessage = "Group detail load failed.";
            return null;
        }

        SelectedGroupDetail = result.Value;
        StatusMessage = result.Value is null ? $"Group '{groupId}' was not found." : $"Loaded group '{groupId}'.";
        return result.Value;
    }

    /// <summary>Loads full detail for a triage run.</summary>
    public async Task<TriageRunSnapshot?> LoadRunDetailAsync(string runId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return null;

        var result = await _dispatcher.QueryAsync(new GetTriageRunQuery(runId), ct).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error ?? "Failed to load triage run.";
            StatusMessage = "Run detail load failed.";
            return null;
        }

        SelectedRunDetail = result.Value;
        StatusMessage = result.Value is null ? $"Run '{runId}' was not found." : $"Loaded run '{runId}'.";
        return result.Value;
    }

    /// <summary>Loads full detail for a triage report.</summary>
    public async Task<TriageReportSnapshot?> LoadReportDetailAsync(string reportId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            return null;

        var result = await _dispatcher.QueryAsync(new GetTriageReportQuery(reportId), ct).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error ?? "Failed to load triage report.";
            StatusMessage = "Report detail load failed.";
            return null;
        }

        SelectedReportDetail = result.Value;
        StatusMessage = result.Value is null ? $"Report '{reportId}' was not found." : $"Loaded report '{reportId}'.";
        return result.Value;
    }

    private string? ResolveWorkspacePath()
    {
        if (!string.IsNullOrWhiteSpace(WorkspacePathFilter))
            return WorkspacePathFilter.Trim();

        return string.IsNullOrWhiteSpace(_workspaceContext.ActiveWorkspacePath)
            ? null
            : _workspaceContext.ActiveWorkspacePath.Trim();
    }

    private string BuildLoadedStatus(string? workspacePath)
    {
        var scope = string.IsNullOrWhiteSpace(workspacePath) ? "all workspaces" : workspacePath;
        var hidden = HiddenCompletedOrMissingTodoCount > 0
            ? $", hidden stale/completed: {HiddenCompletedOrMissingTodoCount}"
            : string.Empty;
        var hydration = TodoHydrationErrorCount > 0
            ? $", hydration errors: {TodoHydrationErrorCount}"
            : string.Empty;
        return $"Loaded triage for {scope}: {TriageQueue.Count} collecting, {ReportGroupQueue.Count} grouped, {RunHistory.Count} runs, {OpenTriageTodos.Count} open TODOs{hidden}{hydration}.";
    }

    private void ClampSelections()
    {
        SelectedTriageQueueIndex = ClampIndex(SelectedTriageQueueIndex, TriageQueue.Count);
        SelectedReportGroupIndex = ClampIndex(SelectedReportGroupIndex, ReportGroupQueue.Count);
        SelectedRunIndex = ClampIndex(SelectedRunIndex, RunHistory.Count);
        SelectedOpenTodoIndex = ClampIndex(SelectedOpenTodoIndex, OpenTriageTodos.Count);
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
            return -1;
        return index >= 0 && index < count ? index : 0;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
