using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using McpServerManager.UI.Core.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using UiCoreWorkspaceDetailViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceDetailViewModel;
using UiCoreWorkspaceGlobalPromptViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceGlobalPromptViewModel;
using UiCoreWorkspaceHealthProbeViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceHealthProbeViewModel;
using UiCoreWorkspaceListViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceListViewModel;

namespace McpServerManager.UI.Core.ViewModels;

#pragma warning disable CS1591

public partial class WorkspaceViewModel : ViewModelBase
{
    private readonly ILogger<WorkspaceViewModel> _logger;

    private readonly IClipboardService _clipboardService;
    private readonly ITimerService _timerService;
    private readonly IUiDispatcherService _uiDispatcher;
    private readonly Dispatcher _dispatcher;
    private readonly UiCoreWorkspaceDetailViewModel _detailVm;
    private readonly UiCoreWorkspaceGlobalPromptViewModel _globalPromptVm;
    private readonly UiCoreWorkspaceHealthProbeViewModel _healthVm;
    private readonly List<WorkspaceListEntry> _allEntries = [];
    private string? _editingWorkspaceKey;
    private McpServerManager.UI.Core.Services.ITimerHandle? _healthTimer;
    private bool _isHealthCheckRunning;
    private bool _hasLoadedGlobalPrompt;
    private long _selectionDetailsLoadSequence;

    [ObservableProperty] private ObservableCollection<WorkspaceListEntry> _filteredItems = [];
    [ObservableProperty] private WorkspaceListEntry? _selectedEntry;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _processStatusText = "";
    [ObservableProperty] private string _healthIndicatorBrush = "Gray";
    [ObservableProperty] private string _healthIndicatorTooltip = "Select a workspace";

    [ObservableProperty] private string _editorKey = "";
    [ObservableProperty] private string _editorName = "";
    [ObservableProperty] private string _editorWorkspacePath = "";
    [ObservableProperty] private string _editorTodoPath = "";
    [ObservableProperty] private string _editorDataDirectory = "";
    [ObservableProperty] private string _editorTunnelProvider = "";
    [ObservableProperty] private string _editorRunAs = "";
    [ObservableProperty] private bool _editorIsPrimary;
    [ObservableProperty] private bool _editorIsEnabled = true;
    [ObservableProperty] private string _editorPromptTemplateText = "";
    [ObservableProperty] private string _editorStatusPromptText = "";
    [ObservableProperty] private string _editorImplementPromptText = "";
    [ObservableProperty] private string _editorPlanPromptText = "";

    [ObservableProperty] private string _globalPromptTemplateText = "";
    [ObservableProperty] private string _globalPromptStatusText = "Global prompt not loaded";
    [ObservableProperty] private bool _globalPromptIsDefault;
    [ObservableProperty] private bool _isGlobalPromptLoading;

    public bool IsEditingExisting => !string.IsNullOrWhiteSpace(_editingWorkspaceKey);

    public string EditorModeText => IsEditingExisting
        ? $"Editing workspace: {_editingWorkspaceKey}"
        : "Creating new workspace";

    public event Action<string>? GlobalStatusChanged;
    public event Action<WorkspaceCatalogChangeEvent>? WorkspaceCatalogChanged;

    public Func<string>? GetWorkspacePromptEditorText { get; set; }
    public Func<string>? GetWorkspaceStatusPromptEditorText { get; set; }
    public Func<string>? GetWorkspaceImplementPromptEditorText { get; set; }
    public Func<string>? GetWorkspacePlanPromptEditorText { get; set; }
    public Func<string>? GetGlobalPromptEditorText { get; set; }

    public WorkspaceViewModel(
        IClipboardService clipboardService,
        UiCoreWorkspaceDetailViewModel detailVm,
        UiCoreWorkspaceGlobalPromptViewModel globalPromptVm,
        UiCoreWorkspaceHealthProbeViewModel healthVm,
        ITimerService timerService,
        IUiDispatcherService uiDispatcher,
        Dispatcher dispatcher,
        ILogger<WorkspaceViewModel>? logger = null)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _detailVm = detailVm ?? throw new ArgumentNullException(nameof(detailVm));
        _globalPromptVm = globalPromptVm ?? throw new ArgumentNullException(nameof(globalPromptVm));
        _healthVm = healthVm ?? throw new ArgumentNullException(nameof(healthVm));
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? NullLogger<WorkspaceViewModel>.Instance;
        NewWorkspace();
    }

    public Task RefreshForConnectionChangeAsync() => RefreshAsync();

    partial void OnFilterTextChanged(string value) => ApplyFilters();

    partial void OnEditorWorkspacePathChanged(string value)
    {
        if (IsEditingExisting && !string.IsNullOrWhiteSpace(_editingWorkspaceKey))
        {
            EditorKey = _editingWorkspaceKey;
            return;
        }

        EditorKey = (value ?? "").Trim();
    }

    partial void OnSelectedEntryChanged(WorkspaceListEntry? value)
    {
        NotifyCheckSelectedWorkspaceHealthCanExecuteChanged();
        if (value == null)
        {
            _selectionDetailsLoadSequence++;
            StopHealthTimer();
            UpdateHealthIndicator(null, "Select a workspace");
            ProcessStatusText = "";
            return;
        }

        var loadSequence = ++_selectionDetailsLoadSequence;
        SetEditingWorkspaceKey(value.Key);
        _ = LoadSelectedWorkspaceDetailsAsync(value.Key, loadSequence);
        StartHealthTimer();
        _ = CheckWorkspaceHealthForSelectionAsync(updateStatusText: false);
    }

    protected virtual void NotifyCheckSelectedWorkspaceHealthCanExecuteChanged()
    {
    }

    protected internal async Task LoadWorkspacesAsync()
    {
        // Thin entry: state + dispatch + apply only. Logic moved to Apply + supporting.
        IsLoading = true;
        StatusText = "Loading...";
        var res = await _dispatcher.QueryAsync(new ListWorkspacesQuery(), default).ConfigureAwait(true);
        ApplyLoadWorkspacesResult(res);
        IsLoading = false;
    }

    private void ApplyLoadWorkspacesResult(Result<ListWorkspacesResult> res)
    {
        var selectedKey = SelectedEntry?.Key ?? _editingWorkspaceKey;
        if (!res.IsSuccess || res.Value is null)
        {
            _allEntries.Clear();
            ApplyFilters();
            var err = res.Error ?? "Unknown error loading workspaces.";
            StatusText = "Error: " + err;
            GlobalStatusChanged?.Invoke($"Workspace load failed: {err}");
            return;
        }

        _allEntries.Clear();
        _allEntries.AddRange(
            res.Value.Items
                .Select(ToEntry)
                .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase));

        ApplyFilters();
        SelectedEntry = null;
        SelectEntryByKey(selectedKey);

        if (!_hasLoadedGlobalPrompt)
            _ = LoadGlobalPromptCoreAsync(updateStatusBar: false);

        StatusText = $"{res.Value.TotalCount} workspace(s)";
        GlobalStatusChanged?.Invoke($"Loaded {res.Value.TotalCount} workspace(s).");
    }

    protected internal async Task RefreshAsync()
    {
        // Thin entry per CQRS remediation: direct observable + 1 dispatch + apply.
        IsLoading = true;
        StatusText = "Refreshing...";
        var res = await _dispatcher.QueryAsync(new ListWorkspacesQuery(), default).ConfigureAwait(true);
        ApplyRefreshResult(res);
        IsLoading = false;
    }

    private void ApplyRefreshResult(Result<ListWorkspacesResult> res)
    {
        var selectedKey = SelectedEntry?.Key ?? _editingWorkspaceKey;
        if (!res.IsSuccess || res.Value is null)
        {
            _allEntries.Clear();
            ApplyFilters();
            var err = res.Error ?? "Unknown error loading workspaces.";
            StatusText = "Error: " + err;
            GlobalStatusChanged?.Invoke($"Workspace load failed: {err}");
            return;
        }

        _allEntries.Clear();
        _allEntries.AddRange(
            res.Value.Items
                .Select(ToEntry)
                .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase));

        ApplyFilters();
        SelectedEntry = null;
        SelectEntryByKey(selectedKey);

        var refreshNote = "";
        if (!string.IsNullOrWhiteSpace(selectedKey) &&
            !string.Equals(SelectedEntry?.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
        {
            _ = TryReloadWorkspaceEditorByKeyAsync(selectedKey, updateStatus: false);
            refreshNote = " • editor refresh queued";
        }

        if (!_hasLoadedGlobalPrompt)
            _ = LoadGlobalPromptCoreAsync(updateStatusBar: false);

        StatusText = $"{res.Value.TotalCount} workspace(s){refreshNote}";
        GlobalStatusChanged?.Invoke($"Refreshed {res.Value.TotalCount} workspace(s).");
    }

    protected async Task LoadGlobalPromptAsync() => await LoadGlobalPromptCoreAsync(updateStatusBar: true);

    protected async Task SaveGlobalPromptAsync()
    {
        if (IsGlobalPromptLoading)
            return;

        IsGlobalPromptLoading = true;
        GlobalPromptStatusText = "Saving global prompt...";
        try
        {
            _globalPromptVm.TemplateText = GetGlobalPromptEditorText?.Invoke() ?? GlobalPromptTemplateText;
            await _globalPromptVm.SaveAsync();
            SyncGlobalPromptFromVm(defaultStatus: "Saved global prompt");
            _hasLoadedGlobalPrompt = true;
            GlobalStatusChanged?.Invoke(GlobalPromptStatusText);
        }
        catch (Exception ex)
        {
            GlobalPromptStatusText = "Global prompt save failed: " + ex.Message;
            GlobalStatusChanged?.Invoke(GlobalPromptStatusText);
        }
        finally
        {
            IsGlobalPromptLoading = false;
        }
    }

    protected async Task ResetGlobalPromptAsync()
    {
        if (IsGlobalPromptLoading)
            return;

        IsGlobalPromptLoading = true;
        try
        {
            await _globalPromptVm.ResetAsync();
            SyncGlobalPromptFromVm(defaultStatus: "Saved global prompt (using built-in default)");
            _hasLoadedGlobalPrompt = true;
            GlobalStatusChanged?.Invoke(GlobalPromptStatusText);
        }
        catch (Exception ex)
        {
            GlobalPromptStatusText = "Global prompt save failed: " + ex.Message;
            GlobalStatusChanged?.Invoke(GlobalPromptStatusText);
        }
        finally
        {
            IsGlobalPromptLoading = false;
        }
    }

    protected void ClearFilters()
    {
        FilterText = "";
        ApplyFilters();
    }

    protected void NewWorkspace()
    {
        SelectedEntry = null;
        SetEditingWorkspaceKey(null);
        StopHealthTimer();
        UpdateHealthIndicator(null, "Select a workspace");
        ProcessStatusText = "";
        _detailVm.BeginNewDraft();

        EditorKey = "";
        EditorName = "";
        EditorWorkspacePath = "";
        EditorTodoPath = "";
        EditorDataDirectory = "";
        EditorTunnelProvider = "";
        EditorRunAs = "";
        EditorIsPrimary = false;
        EditorIsEnabled = true;
        EditorPromptTemplateText = "";
        EditorStatusPromptText = "";
        EditorImplementPromptText = "";
        EditorPlanPromptText = "";
        StatusText = "New workspace draft";
    }

    protected async Task OpenSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key))
            return;

        await TryReloadWorkspaceEditorByKeyAsync(key, updateStatus: true);
    }

    protected async Task SaveEditorAsync()
    {
        // Thin entry: prep from state, dispatch, apply. Branching/mutation logic in Build + Apply.
        var cmd = BuildSaveEditorCommand();
        var res = await _dispatcher.SendAsync(cmd, default).ConfigureAwait(true);
        ApplySaveEditorResult(res);
    }

    private ICommand<WorkspaceMutationOutcome> BuildSaveEditorCommand()
    {
        if (IsEditingExisting)
        {
            var key = GetKeyForActions();
            return new UpdateWorkspaceCommand
            {
                WorkspacePath = key,
                Name = EditorName,
                TodoPath = EditorTodoPath,
                DataDirectory = EditorDataDirectory,
                TunnelProvider = EditorTunnelProvider,
                RunAs = EditorRunAs,
                IsPrimary = EditorIsPrimary,
                IsEnabled = EditorIsEnabled,
                PromptTemplate = EditorPromptTemplateText,
                StatusPrompt = EditorStatusPromptText,
                ImplementPrompt = EditorImplementPromptText,
                PlanPrompt = EditorPlanPromptText,
            };
        }
        else
        {
            return new CreateWorkspaceCommand
            {
                WorkspacePath = EditorWorkspacePath,
                Name = EditorName,
                TodoPath = EditorTodoPath,
                DataDirectory = EditorDataDirectory,
                TunnelProvider = EditorTunnelProvider,
                RunAs = EditorRunAs,
                IsPrimary = EditorIsPrimary,
                IsEnabled = EditorIsEnabled,
                PromptTemplate = EditorPromptTemplateText,
                StatusPrompt = EditorStatusPromptText,
                ImplementPrompt = EditorImplementPromptText,
                PlanPrompt = EditorPlanPromptText,
            };
        }
    }

    private void ApplySaveEditorResult(Result<WorkspaceMutationOutcome> res)
    {
        if (!res.IsSuccess || res.Value == null || !res.Value.Success)
        {
            StatusText = "Save failed: " + (res.Value?.Error ?? res.Error ?? "unknown");
            return;
        }
        var key = res.Value.Item?.WorkspacePath ?? (IsEditingExisting ? GetKeyForActions() : EditorWorkspacePath);
        if (res.Value.Item != null) PopulateEditor(res.Value.Item);
        SetEditingWorkspaceKey(key);
        StatusText = (IsEditingExisting ? "Saved " : "Created ") + key;
        _ = LoadWorkspacesAsync();
        SelectEntryByKey(key);
        var kind = IsEditingExisting ? WorkspaceCatalogChangeKind.Updated : WorkspaceCatalogChangeKind.Created;
        RaiseWorkspaceCatalogChanged(kind, key, res.Value.Item);
    }

    protected internal async Task DeleteSelectedAsync()
    {
        var key = GetKeyForActions();
        var res = await _dispatcher.SendAsync(new DeleteWorkspaceCommand(key));
        ApplyDeleteResult(key, res);
    }

    private void ApplyDeleteResult(string key, Result<WorkspaceMutationOutcome> res)
    {
        if (!res.IsSuccess || res.Value == null || !res.Value.Success)
        {
            StatusText = "Delete failed: " + (res.Value?.Error ?? res.Error ?? "unknown");
            return;
        }
        StatusText = $"Deleted {key}";
        if (string.Equals(_editingWorkspaceKey, key, StringComparison.OrdinalIgnoreCase))
            NewWorkspace();
        _ = LoadWorkspacesAsync();
        RaiseWorkspaceCatalogChanged(WorkspaceCatalogChangeKind.Deleted, key, null);
    }

    protected async Task GetSelectedStatusAsync()
    {
        var key = GetKeyForActions();
        var res = await _dispatcher.QueryAsync(new GetWorkspaceStatusQuery(key));
        ApplyStatusResult(key, res);
    }

    private void ApplyStatusResult(string key, Result<WorkspaceProcessState> res)
    {
        if (!res.IsSuccess || res.Value == null)
        {
            StatusText = "Error: " + (res.Error ?? "unknown");
            return;
        }
        ProcessStatusText = res.Value.IsRunning ? $"Running pid {res.Value.Pid}" : "Stopped";
        StatusText = $"Status loaded for {key}";
    }

    protected async Task CheckSelectedWorkspaceHealthAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;
        try
        {
            var result = await _dispatcher.QueryAsync(new CheckWorkspaceHealthQuery(key), default).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value == null)
            {
                StatusText = "Error: " + (result.Error ?? "unknown");
                return;
            }
            UpdateHealthIndicator(result.Value.Success, null);
            StatusText = $"Health checked for {key}";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    protected bool CanCheckSelectedWorkspaceHealth() => !string.IsNullOrWhiteSpace(SelectedEntry?.Key);

    protected async Task InitSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key))
            return;

        try
        {
            var result = await _dispatcher.SendAsync(new InitWorkspaceCommand(key), default).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value == null)
            {
                StatusText = "Init failed: " + (result.Error ?? "unknown");
                return;
            }

            var fileCount = result.Value.SeededDefinitions ?? 0;
            StatusText = $"Initialized {key} ({fileCount} files)";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    protected async Task StartSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key))
            return;

        try
        {
            var result = await _dispatcher.SendAsync(new StartWorkspaceCommand(key), default).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value == null)
            {
                StatusText = $"Start failed for {key}: {result.Error ?? "unknown"}";
                return;
            }

            ProcessStatusText = result.Value.IsRunning ? $"Running pid {result.Value.Pid}" : "";
            var state = result.Value;
            if (!string.IsNullOrWhiteSpace(state?.Error))
                StatusText = $"Start failed for {key}: {state.Error}";
            else if (state?.IsRunning == true)
                StatusText = $"Started {key}";
            else
                StatusText = $"Start failed for {key}: workspace did not report a running process";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    protected async Task StopSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key))
            return;

        try
        {
            var result = await _dispatcher.SendAsync(new StopWorkspaceCommand(key), default).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value == null)
            {
                StatusText = $"Stop failed for {key}: {result.Error ?? "unknown"}";
                return;
            }

            ProcessStatusText = result.Value.IsRunning ? $"Running pid {result.Value.Pid}" : "";
            StatusText = $"Stop requested for {key}";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    protected async Task CopySelectedKeyAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key))
            return;

        await _clipboardService.SetTextAsync(key).ConfigureAwait(true);
        StatusText = $"Copied {key}";
    }

    internal async Task CreateWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(EditorWorkspacePath))
        {
            StatusText = "Workspace Path is required";
            return;
        }

        // Thinned: dispatch only. Orchestration (detail, create, load, select, raise) extracted to handler.
        var res = await _dispatcher.SendAsync(new CreateWorkspaceCommand { WorkspacePath = EditorWorkspacePath });
    }



    private static WorkspaceListEntry ToEntry(WorkspaceSummary item)
    {
        var key = item.WorkspacePath;
        var title = string.IsNullOrWhiteSpace(item.Name) ? key : item.Name.Trim();
        var flags = new List<string>();
        if (item.IsPrimary)
            flags.Add("Primary");
        if (!item.IsEnabled)
            flags.Add("Disabled");

        var flagsText = flags.Count == 0 ? "" : $" | {string.Join(", ", flags)}";
        return new WorkspaceListEntry
        {
            Item = new McpWorkspaceItem
            {
                WorkspacePath = item.WorkspacePath,
                Name = item.Name,
                IsPrimary = item.IsPrimary,
                IsEnabled = item.IsEnabled
            },
            Key = key,
            Title = title,
            Subtitle = flagsText.TrimStart(' ', '|').Trim(),
            SearchText = string.Join(
                " ",
                new[]
                {
                    key,
                    item.Name,
                    item.IsPrimary ? "primary" : null,
                    item.IsEnabled ? "enabled" : "disabled"
                }.Where(static value => !string.IsNullOrWhiteSpace(value)))
        };
    }

    private void ApplyFilters()
    {
        IEnumerable<WorkspaceListEntry> source = _allEntries;
        var text = (FilterText ?? "").Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var matcher = BooleanSearchParser.Parse(text);
            source = source.Where(entry => matcher(entry.SearchText));
        }

        FilteredItems = new ObservableCollection<WorkspaceListEntry>(
            source.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase));
    }

    private void PopulateEditor(WorkspaceDetail detail)
    {
        EditorKey = detail.WorkspacePath;
        EditorName = detail.Name;
        EditorWorkspacePath = detail.WorkspacePath;
        EditorTodoPath = detail.TodoPath;
        EditorDataDirectory = detail.DataDirectory ?? "";
        EditorTunnelProvider = detail.TunnelProvider ?? "";
        EditorRunAs = detail.RunAs ?? "";
        EditorIsPrimary = detail.IsPrimary;
        EditorIsEnabled = detail.IsEnabled;
        EditorPromptTemplateText = detail.PromptTemplate ?? "";
        EditorStatusPromptText = detail.StatusPrompt;
        EditorImplementPromptText = detail.ImplementPrompt;
        EditorPlanPromptText = detail.PlanPrompt;
    }



    private async Task<bool> TryReloadWorkspaceEditorByKeyAsync(string key, bool updateStatus)
    {
        if (updateStatus)
            StatusText = $"Loading {key}...";

        try
        {
            _detailVm.WorkspacePath = key;
            await _detailVm.LoadAsync();
            if (!string.IsNullOrWhiteSpace(_detailVm.ErrorMessage))
            {
                if (updateStatus)
                    StatusText = "Error: " + _detailVm.ErrorMessage;
                return false;
            }

            if (_detailVm.Detail is null)
            {
                if (updateStatus)
                    StatusText = $"Workspace {key} not found";
                return false;
            }

            PopulateEditor(_detailVm.Detail);
            SetEditingWorkspaceKey(_detailVm.Detail.WorkspacePath);
            SelectEntryByKey(_detailVm.Detail.WorkspacePath);
            if (updateStatus)
                StatusText = $"Loaded {_detailVm.Detail.WorkspacePath}";
            return true;
        }
        catch (Exception ex)
        {
            if (updateStatus)
                StatusText = "Error: " + ex.Message;
            return false;
        }
    }

    internal async Task LoadSelectedWorkspaceDetailsAsync(string key, long loadSequence)
    {
        try
        {
            _detailVm.WorkspacePath = key;
            await _detailVm.LoadAsync();

            if (loadSequence != _selectionDetailsLoadSequence)
                return;

            if (!string.Equals(SelectedEntry?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrWhiteSpace(_detailVm.ErrorMessage))
            {
                StatusText = "Error: " + _detailVm.ErrorMessage;
                return;
            }

            if (_detailVm.Detail is null)
            {
                StatusText = $"Workspace {key} not found";
                return;
            }

            PopulateEditor(_detailVm.Detail);
            SetEditingWorkspaceKey(_detailVm.Detail.WorkspacePath);
            if (!string.Equals(_detailVm.Detail.WorkspacePath, key, StringComparison.OrdinalIgnoreCase))
                SelectEntryByKey(_detailVm.Detail.WorkspacePath);
        }
        catch (Exception ex)
        {
            if (loadSequence != _selectionDetailsLoadSequence)
                return;

            if (!string.Equals(SelectedEntry?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            StatusText = "Error: " + ex.Message;
        }
    }

    private void SelectEntryByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var entry = FilteredItems.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
            SelectedEntry = entry;
    }

    private void SetEditingWorkspaceKey(string? workspaceKey)
    {
        _editingWorkspaceKey = NullIfWhiteSpace(workspaceKey);
        EditorKey = _editingWorkspaceKey ?? "";
        OnPropertyChanged(nameof(IsEditingExisting));
        OnPropertyChanged(nameof(EditorModeText));
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BlankToNullPreserveContent(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    internal async Task LoadGlobalPromptCoreAsync(bool updateStatusBar)
    {
        if (IsGlobalPromptLoading)
            return;

        IsGlobalPromptLoading = true;
        GlobalPromptStatusText = "Loading global prompt...";
        try
        {
            await _globalPromptVm.LoadAsync();
            SyncGlobalPromptFromVm(defaultStatus: "Loaded global prompt");
            _hasLoadedGlobalPrompt = true;
            if (updateStatusBar)
                GlobalStatusChanged?.Invoke(GlobalPromptStatusText);
        }
        catch (Exception ex)
        {
            GlobalPromptStatusText = "Global prompt unavailable: " + ex.Message;
            if (updateStatusBar)
                GlobalStatusChanged?.Invoke(GlobalPromptStatusText);
        }
        finally
        {
            IsGlobalPromptLoading = false;
        }
    }

    private void SyncGlobalPromptFromVm(string defaultStatus)
    {
        GlobalPromptTemplateText = _globalPromptVm.TemplateText;
        GlobalPromptIsDefault = _globalPromptVm.IsDefault;
        GlobalPromptStatusText = _globalPromptVm.StatusMessage ?? defaultStatus;
    }

    private void RaiseWorkspaceCatalogChanged(
        WorkspaceCatalogChangeKind changeKind,
        string? fallbackKey,
        WorkspaceDetail? workspace)
    {
        var key = NullIfWhiteSpace(workspace?.WorkspacePath) ?? NullIfWhiteSpace(fallbackKey);
        if (string.IsNullOrWhiteSpace(key))
            return;

        WorkspaceCatalogChanged?.Invoke(new WorkspaceCatalogChangeEvent
        {
            ChangeKind = changeKind,
            WorkspaceKey = key,
            IsPrimary = workspace?.IsPrimary,
            IsEnabled = workspace?.IsEnabled
        });
    }

    private string? GetKeyForActions()
    {
        return _editingWorkspaceKey ??
               NullIfWhiteSpace(SelectedEntry?.Item.WorkspacePath) ??
               NullIfWhiteSpace(EditorWorkspacePath);
    }

    internal async Task CheckWorkspaceHealthForSelectionAsync(bool updateStatusText)
    {
        var key = SelectedEntry?.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            UpdateHealthIndicator(null, "Select a workspace");
            return;
        }

        if (_isHealthCheckRunning)
            return;

        _isHealthCheckRunning = true;
        if (updateStatusText)
            StatusText = $"Checking health for {key}...";

        try
        {
            _healthVm.WorkspacePath = key;
            await _healthVm.CheckHealthAsync();

            if (!string.Equals(SelectedEntry?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrWhiteSpace(_healthVm.ErrorMessage))
            {
                UpdateHealthIndicator(false, $"Health error: {_healthVm.ErrorMessage}");
                if (updateStatusText)
                    StatusText = "Error: " + _healthVm.ErrorMessage;
                return;
            }

            var health = _healthVm.LastHealthState;
            ProcessStatusText = _healthVm.HealthStatusText;
            UpdateHealthIndicator(health?.Success, health?.Success == true
                ? $"Healthy: {key}"
                : $"Unhealthy: {key}");

            if (updateStatusText)
            {
                StatusText = health?.Success == true
                    ? $"Health OK for {key}"
                    : $"Health failed for {key}";
            }
        }
        catch (Exception ex)
        {
            if (!string.Equals(SelectedEntry?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            UpdateHealthIndicator(false, $"Health error: {ex.Message}");
            if (updateStatusText)
                StatusText = "Error: " + ex.Message;
        }
        finally
        {
            _isHealthCheckRunning = false;
        }
    }

    private void StartHealthTimer()
    {
        StopHealthTimer();
        _healthTimer = _timerService.CreateRecurring(TimeSpan.FromMinutes(1), ct =>
        {
            _uiDispatcher.Post(() => _ = CheckWorkspaceHealthForSelectionAsync(updateStatusText: false));
            return Task.CompletedTask;
        });
    }

    private void StopHealthTimer()
    {
        _healthTimer?.Dispose();
        _healthTimer = null;
    }

    private void UpdateHealthIndicator(bool? isHealthy, string tooltip)
    {
        HealthIndicatorBrush = isHealthy switch
        {
            true => "LimeGreen",
            false => "OrangeRed",
            _ => "Gray"
        };
        HealthIndicatorTooltip = tooltip;
    }

    private void ApplyEditorToDetailVm(bool forCreate)
    {
        if (forCreate)
            _detailVm.BeginNewDraft(EditorWorkspacePath);

        _detailVm.WorkspacePath = _editingWorkspaceKey ?? EditorWorkspacePath;
        _detailVm.EditorWorkspacePath = EditorWorkspacePath.Trim();
        _detailVm.EditorName = NullIfWhiteSpace(EditorName) ?? string.Empty;
        _detailVm.EditorTodoPath = NullIfWhiteSpace(EditorTodoPath) ?? string.Empty;
        _detailVm.EditorDataDirectory = NullIfWhiteSpace(EditorDataDirectory) ?? string.Empty;
        _detailVm.EditorTunnelProvider = NullIfWhiteSpace(EditorTunnelProvider) ?? string.Empty;
        _detailVm.EditorRunAs = NullIfWhiteSpace(EditorRunAs) ?? string.Empty;
        _detailVm.EditorIsPrimary = EditorIsPrimary;
        _detailVm.EditorIsEnabled = EditorIsEnabled;
        _detailVm.EditorPromptTemplateText = BlankToNullPreserveContent(GetWorkspacePromptEditorText?.Invoke() ?? EditorPromptTemplateText) ?? string.Empty;
        _detailVm.EditorStatusPromptText = BlankToNullPreserveContent(GetWorkspaceStatusPromptEditorText?.Invoke() ?? EditorStatusPromptText) ?? string.Empty;
        _detailVm.EditorImplementPromptText = BlankToNullPreserveContent(GetWorkspaceImplementPromptEditorText?.Invoke() ?? EditorImplementPromptText) ?? string.Empty;
        _detailVm.EditorPlanPromptText = BlankToNullPreserveContent(GetWorkspacePlanPromptEditorText?.Invoke() ?? EditorPlanPromptText) ?? string.Empty;
    }
}

public sealed class WorkspaceListEntry
{
    public McpWorkspaceItem Item { get; init; } = new();
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string SearchText { get; init; } = "";
}

public enum WorkspaceCatalogChangeKind
{
    Created,
    Updated,
    Deleted
}

public sealed class WorkspaceCatalogChangeEvent
{
    public WorkspaceCatalogChangeKind ChangeKind { get; init; }
    public string WorkspaceKey { get; init; } = "";
    public bool? IsPrimary { get; init; }
    public bool? IsEnabled { get; init; }
}

#pragma warning restore CS1591
