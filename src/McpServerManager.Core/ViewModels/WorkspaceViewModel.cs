using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Core.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("WorkspaceViewModel");
    private readonly Mediator _mediator = new();
    private readonly IClipboardService _clipboardService;
    private readonly UiCoreViewModelEvaluator _uiCoreEvaluator;
    private readonly List<WorkspaceListEntry> _allEntries = new();
    private string? _editingWorkspaceKey;
    private bool _isBusyHandlerRegistered;
    private bool _hasRunUiCoreListEvaluation;
    private Timer? _healthTimer;
    private bool _isHealthCheckRunning;
    private bool _hasLoadedGlobalPrompt;
    private long _selectionDetailsLoadSequence;

    [ObservableProperty] private ObservableCollection<WorkspaceListEntry> _filteredItems = new();
    [ObservableProperty] private WorkspaceListEntry? _selectedEntry;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _processStatusText = "";
    [ObservableProperty] private IBrush _healthIndicatorBrush = Brushes.Gray;
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

    /// <summary>Set by the view code-behind to read current per-workspace prompt editor content.</summary>
    public Func<string>? GetWorkspacePromptEditorText { get; set; }

    /// <summary>Set by the view code-behind to read current workspace status prompt editor content.</summary>
    public Func<string>? GetWorkspaceStatusPromptEditorText { get; set; }

    /// <summary>Set by the view code-behind to read current workspace implement prompt editor content.</summary>
    public Func<string>? GetWorkspaceImplementPromptEditorText { get; set; }

    /// <summary>Set by the view code-behind to read current workspace plan prompt editor content.</summary>
    public Func<string>? GetWorkspacePlanPromptEditorText { get; set; }

    /// <summary>Set by the desktop view code-behind to read current global prompt editor content.</summary>
    public Func<string>? GetGlobalPromptEditorText { get; set; }

    public WorkspaceViewModel(IClipboardService clipboardService, McpWorkspaceService service)
    {
        _clipboardService = clipboardService;
        _uiCoreEvaluator = new UiCoreViewModelEvaluator(workspaceService: service);
        RegisterCqrsHandlers(service);
        NewWorkspace();
    }

    public WorkspaceViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        // Design-time / standalone fallback — creates its own client.
        var baseUrl = AppSettings.ResolveMcpBaseUrl();
        var normalizedUrl = McpServerRestClientFactory.NormalizeBaseUrl(baseUrl);
        var client = McpServerRestClientFactory.Create(baseUrl, TimeSpan.FromSeconds(5));
        var workspaceService = new McpWorkspaceService(client, new Uri(normalizedUrl, UriKind.Absolute));
        _uiCoreEvaluator = new UiCoreViewModelEvaluator(workspaceService: workspaceService);
        RegisterCqrsHandlers(workspaceService);
        NewWorkspace();
    }

    private void RegisterCqrsHandlers(McpWorkspaceService service)
    {
        if (!_isBusyHandlerRegistered)
        {
            _mediator.IsBusyChanged += busy =>
            {
                Dispatcher.UIThread.Post(() => IsLoading = busy);
            };
            _isBusyHandlerRegistered = true;
        }

        _mediator.RegisterQuery(new QueryWorkspacesHandler(service));
        _mediator.RegisterQuery(new GetWorkspaceByIdHandler(service));
        _mediator.RegisterQuery(new GetWorkspaceStatusHandler(service));
        _mediator.RegisterQuery(new GetWorkspaceHealthHandler(service));
        _mediator.RegisterQuery(new GetWorkspaceGlobalPromptHandler(service));
        _mediator.Register<CreateWorkspaceCommand, McpWorkspaceMutationResult>(new CreateWorkspaceHandler(service));
        _mediator.Register<UpdateWorkspaceCommand, McpWorkspaceMutationResult>(new UpdateWorkspaceHandler(service));
        _mediator.Register<DeleteWorkspaceCommand, McpWorkspaceMutationResult>(new DeleteWorkspaceHandler(service));
        _mediator.Register<InitWorkspaceCommand, McpWorkspaceInitResult>(new InitWorkspaceHandler(service));
        _mediator.Register<StartWorkspaceCommand, McpWorkspaceProcessStatus>(new StartWorkspaceHandler(service));
        _mediator.Register<StopWorkspaceCommand, McpWorkspaceProcessStatus>(new StopWorkspaceHandler(service));
        _mediator.Register<UpdateWorkspaceGlobalPromptCommand, McpWorkspaceGlobalPromptResult>(
            new UpdateWorkspaceGlobalPromptHandler(service));
    }

    public Task RefreshForConnectionChangeAsync() => LoadWorkspacesCoreAsync(forceEditorReload: true);

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
        CheckSelectedWorkspaceHealthCommand.NotifyCanExecuteChanged();
        if (value == null)
        {
            _selectionDetailsLoadSequence++;
            StopHealthTimer();
            UpdateHealthIndicator(null, "Select a workspace");
            return;
        }

        var loadSequence = ++_selectionDetailsLoadSequence;
        SetEditingWorkspaceKey(value.Key);
        _ = LoadSelectedWorkspaceDetailsAsync(value.Key, loadSequence);
        StartHealthTimer();
        _ = CheckWorkspaceHealthForSelectionAsync(updateStatusText: false);
    }

    [RelayCommand]
    private async Task LoadWorkspacesAsync() => await LoadWorkspacesCoreAsync(forceEditorReload: false);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadWorkspacesCoreAsync(forceEditorReload: true);

    [RelayCommand]
    private async Task LoadGlobalPromptAsync()
    {
        await LoadGlobalPromptCoreAsync(updateStatusBar: true);
    }

    [RelayCommand]
    private async Task SaveGlobalPromptAsync()
    {
        var template = GetGlobalPromptEditorText?.Invoke() ?? GlobalPromptTemplateText;
        if (IsGlobalPromptLoading)
            return;

        IsGlobalPromptLoading = true;
        GlobalPromptStatusText = "Saving global prompt...";
        try
        {
            var result = await _mediator.SendAsync<UpdateWorkspaceGlobalPromptCommand, McpWorkspaceGlobalPromptResult>(
                new UpdateWorkspaceGlobalPromptCommand(BlankToNullPreserveContent(template)));

            GlobalPromptTemplateText = result.Template ?? "";
            GlobalPromptIsDefault = result.IsDefault;
            _hasLoadedGlobalPrompt = true;
            GlobalPromptStatusText = result.IsDefault
                ? "Saved global prompt (using built-in default)"
                : "Saved global prompt";
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

    [RelayCommand]
    private async Task ResetGlobalPromptAsync()
    {
        if (IsGlobalPromptLoading)
            return;

        // Empty/null tells the server to revert to the built-in template.
        GlobalPromptTemplateText = "";
        await SaveGlobalPromptAsync();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterText = "";
        ApplyFilters();
    }

    [RelayCommand]
    private void NewWorkspace()
    {
        SelectedEntry = null;
        SetEditingWorkspaceKey(null);
        CheckSelectedWorkspaceHealthCommand.NotifyCanExecuteChanged();
        StopHealthTimer();
        UpdateHealthIndicator(null, "Select a workspace");
        ProcessStatusText = "";
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

    [RelayCommand]
    private async Task OpenSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;
        await TryReloadWorkspaceEditorByKeyAsync(key, updateStatus: true);
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        if (IsEditingExisting)
            await UpdateExistingWorkspaceAsync();
        else
            await CreateWorkspaceAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            var result = await _mediator.SendAsync<DeleteWorkspaceCommand, McpWorkspaceMutationResult>(
                new DeleteWorkspaceCommand(key));
            if (!result.Success)
            {
                StatusText = $"Delete failed: {result.Error}";
                return;
            }

            StatusText = $"Deleted {key}";
            if (string.Equals(_editingWorkspaceKey, key, StringComparison.OrdinalIgnoreCase))
                NewWorkspace();
            await LoadWorkspacesAsync();
            RaiseWorkspaceCatalogChanged(WorkspaceCatalogChangeKind.Deleted, key, result.Workspace);
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task GetSelectedStatusAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            var status = await _mediator.QueryAsync<GetWorkspaceStatusQuery, McpWorkspaceProcessStatus>(
                new GetWorkspaceStatusQuery(key));
            ProcessStatusText = FormatProcessStatus(status);
            StatusText = $"Status loaded for {key}";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckSelectedWorkspaceHealth))]
    private async Task CheckSelectedWorkspaceHealthAsync()
    {
        await CheckWorkspaceHealthForSelectionAsync(updateStatusText: true);
    }

    [RelayCommand]
    private async Task InitSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            var result = await _mediator.SendAsync<InitWorkspaceCommand, McpWorkspaceInitResult>(
                new InitWorkspaceCommand(key));
            if (!result.Success)
            {
                StatusText = $"Init failed: {result.Error}";
                return;
            }

            var fileCount = result.FilesCreated?.Count ?? 0;
            StatusText = $"Initialized {key} ({fileCount} files)";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task StartSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            var status = await _mediator.SendAsync<StartWorkspaceCommand, McpWorkspaceProcessStatus>(
                new StartWorkspaceCommand(key));
            ProcessStatusText = FormatProcessStatus(status);
            if (!string.IsNullOrWhiteSpace(status.Error))
            {
                StatusText = $"Start failed for {key}: {status.Error}";
            }
            else if (status.IsRunning)
            {
                StatusText = $"Started {key}";
            }
            else
            {
                StatusText = $"Start failed for {key}: workspace did not report a running process";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task StopSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            var status = await _mediator.SendAsync<StopWorkspaceCommand, McpWorkspaceProcessStatus>(
                new StopWorkspaceCommand(key));
            ProcessStatusText = FormatProcessStatus(status);
            StatusText = $"Stop requested for {key}";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task CopySelectedKeyAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;
        await _clipboardService.SetTextAsync(key);
        StatusText = $"Copied {key}";
    }

    private async Task CreateWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(EditorWorkspacePath))
        {
            StatusText = "Workspace Path is required";
            return;
        }

        var request = new McpWorkspaceCreateRequest
        {
            Name = NullIfWhiteSpace(EditorName),
            WorkspacePath = NullIfWhiteSpace(EditorWorkspacePath),
            TodoPath = NullIfWhiteSpace(EditorTodoPath),
            DataDirectory = NullIfWhiteSpace(EditorDataDirectory),
            TunnelProvider = NullIfWhiteSpace(EditorTunnelProvider),
            RunAs = NullIfWhiteSpace(EditorRunAs),
            IsPrimary = EditorIsPrimary,
            IsEnabled = EditorIsEnabled,
            PromptTemplate = BlankToNullPreserveContent(GetWorkspacePromptEditorText?.Invoke() ?? EditorPromptTemplateText),
            StatusPrompt = BlankToNullPreserveContent(GetWorkspaceStatusPromptEditorText?.Invoke() ?? EditorStatusPromptText),
            ImplementPrompt = BlankToNullPreserveContent(GetWorkspaceImplementPromptEditorText?.Invoke() ?? EditorImplementPromptText),
            PlanPrompt = BlankToNullPreserveContent(GetWorkspacePlanPromptEditorText?.Invoke() ?? EditorPlanPromptText)
        };

        try
        {
            var result = await _mediator.SendAsync<CreateWorkspaceCommand, McpWorkspaceMutationResult>(
                new CreateWorkspaceCommand(request));
            if (!result.Success)
            {
                StatusText = $"Create failed: {result.Error}";
                return;
            }

            var key = ResolveKey(result.Workspace) ??
                      NullIfWhiteSpace(EditorWorkspacePath) ??
                      "";
            EditorKey = key;
            SetEditingWorkspaceKey(key);
            StatusText = $"Created {key}";
            await LoadWorkspacesAsync();
            SelectEntryByKey(key);
            RaiseWorkspaceCatalogChanged(WorkspaceCatalogChangeKind.Created, key, result.Workspace);
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    private async Task UpdateExistingWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key))
            return;

        var request = new McpWorkspaceUpdateRequest
        {
            Name = NullIfWhiteSpace(EditorName),
            TodoPath = NullIfWhiteSpace(EditorTodoPath),
            DataDirectory = NullIfWhiteSpace(EditorDataDirectory),
            TunnelProvider = NullIfWhiteSpace(EditorTunnelProvider),
            RunAs = NullIfWhiteSpace(EditorRunAs),
            IsPrimary = EditorIsPrimary,
            IsEnabled = EditorIsEnabled,
            PromptTemplate = BlankToNullPreserveContent(GetWorkspacePromptEditorText?.Invoke() ?? EditorPromptTemplateText),
            StatusPrompt = BlankToNullPreserveContent(GetWorkspaceStatusPromptEditorText?.Invoke() ?? EditorStatusPromptText),
            ImplementPrompt = BlankToNullPreserveContent(GetWorkspaceImplementPromptEditorText?.Invoke() ?? EditorImplementPromptText),
            PlanPrompt = BlankToNullPreserveContent(GetWorkspacePlanPromptEditorText?.Invoke() ?? EditorPlanPromptText)
        };

        try
        {
            var result = await _mediator.SendAsync<UpdateWorkspaceCommand, McpWorkspaceMutationResult>(
                new UpdateWorkspaceCommand(key, request));
            if (!result.Success)
            {
                StatusText = $"Save failed: {result.Error}";
                return;
            }

            StatusText = $"Saved {key}";
            await LoadWorkspacesAsync();
            SelectEntryByKey(key);
            RaiseWorkspaceCatalogChanged(WorkspaceCatalogChangeKind.Updated, key, result.Workspace);
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
    }

    private static WorkspaceListEntry ToEntry(McpWorkspaceItem item)
    {
        var key = ResolveKey(item) ?? "";
        var title = string.IsNullOrWhiteSpace(item.Name) ? key : item.Name.Trim();
        var flags = new List<string>();
        if (item.IsPrimary == true)
            flags.Add("Primary");
        if (item.IsEnabled == false)
            flags.Add("Disabled");
        var flagsText = flags.Count == 0 ? "" : $" | {string.Join(", ", flags)}";
        var subtitle = string.IsNullOrWhiteSpace(item.WorkspacePath)
            ? flagsText.TrimStart(' ', '|').Trim()
            : $"{item.WorkspacePath}{flagsText}";
        var searchable = string.Join(" ",
            new[]
            {
                key,
                item.Name,
                item.WorkspacePath,
                item.TodoPath,
                item.DataDirectory,
                item.TunnelProvider,
                item.RunAs,
                item.IsPrimary == true ? "primary" : null,
                item.IsEnabled == false ? "disabled" : "enabled"
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new WorkspaceListEntry
        {
            Item = item,
            Key = key,
            Title = title,
            Subtitle = subtitle,
            SearchText = searchable
        };
    }

    private static string? ResolveKey(McpWorkspaceItem? item)
    {
        if (item == null) return null;
        if (!string.IsNullOrWhiteSpace(item.WorkspacePath))
            return item.WorkspacePath.Trim();
        return null;
    }

    private void ApplyFilters()
    {
        IEnumerable<WorkspaceListEntry> source = _allEntries;
        var text = (FilterText ?? "").Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var matcher = BooleanSearchParser.Parse(text);
            source = source.Where(e => matcher(e.SearchText));
        }

        FilteredItems = new ObservableCollection<WorkspaceListEntry>(
            source
                .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase));
    }

    private void PopulateEditor(McpWorkspaceItem item)
    {
        EditorName = item.Name ?? "";
        EditorWorkspacePath = item.WorkspacePath ?? "";
        EditorTodoPath = item.TodoPath ?? "";
        EditorDataDirectory = item.DataDirectory ?? "";
        EditorTunnelProvider = item.TunnelProvider ?? "";
        EditorRunAs = item.RunAs ?? "";
        EditorIsPrimary = item.IsPrimary ?? false;
        EditorIsEnabled = item.IsEnabled ?? true;
        EditorPromptTemplateText = item.PromptTemplate ?? "";
        EditorStatusPromptText = item.StatusPrompt ?? "";
        EditorImplementPromptText = item.ImplementPrompt ?? "";
        EditorPlanPromptText = item.PlanPrompt ?? "";
    }

    private async Task LoadWorkspacesCoreAsync(bool forceEditorReload)
    {
        IsLoading = true;
        StatusText = forceEditorReload ? "Refreshing..." : "Loading...";
        GlobalStatusChanged?.Invoke(forceEditorReload ? "Refreshing workspaces..." : "Loading workspaces...");

        var selectedKey = SelectedEntry?.Key ?? _editingWorkspaceKey;
        try
        {
            var result = await _mediator.QueryAsync<QueryWorkspacesQuery, McpWorkspaceQueryResult>(new QueryWorkspacesQuery());
            await EvaluateUiCoreWorkspaceListParityAsync(result);

            _allEntries.Clear();
            _allEntries.AddRange(result.Items.Select(ToEntry)
                .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase));

            ApplyFilters();
            SelectedEntry = null;
            SelectEntryByKey(selectedKey);

            var refreshNote = "";
            if (forceEditorReload &&
                !string.IsNullOrWhiteSpace(selectedKey) &&
                !string.Equals(SelectedEntry?.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
            {
                var refreshed = await TryReloadWorkspaceEditorByKeyAsync(selectedKey, updateStatus: false);
                refreshNote = refreshed ? " • editor refreshed" : " • editor not found";
            }

            if (!_hasLoadedGlobalPrompt || forceEditorReload)
                await LoadGlobalPromptCoreAsync(updateStatusBar: false);

            StatusText = $"{result.TotalCount} workspace(s){refreshNote}";
            GlobalStatusChanged?.Invoke(forceEditorReload
                ? $"Refreshed {result.TotalCount} workspace(s)."
                : $"Loaded {result.TotalCount} workspace(s).");
        }
        catch (Exception ex)
        {
            _allEntries.Clear();
            ApplyFilters();
            StatusText = "Error: " + ex.Message;
            GlobalStatusChanged?.Invoke($"Workspace load failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EvaluateUiCoreWorkspaceListParityAsync(McpWorkspaceQueryResult currentResult)
    {
        if (_hasRunUiCoreListEvaluation)
            return;

        _hasRunUiCoreListEvaluation = true;
        var evaluation = await _uiCoreEvaluator
            .EvaluateWorkspaceListAsync(currentResult)
            .ConfigureAwait(true);

        if (!evaluation.Success)
        {
            _logger.LogWarning(
                "UI.Core WorkspaceListViewModel evaluation failed: {Error}",
                evaluation.Error ?? "unknown error");
            return;
        }

        if (evaluation.IsMatch)
        {
            _logger.LogInformation(
                "UI.Core WorkspaceListViewModel parity check passed ({Count} items).",
                evaluation.CurrentCount);
            return;
        }

        _logger.LogWarning(
            "UI.Core WorkspaceListViewModel parity mismatch. Current={CurrentCount}, UiCore={UiCoreCount}, MissingInUiCore=[{MissingInUiCore}], MissingInCurrent=[{MissingInCurrent}]",
            evaluation.CurrentCount,
            evaluation.UiCoreCount,
            string.Join(", ", evaluation.MissingInUiCore),
            string.Join(", ", evaluation.MissingInCurrent));
    }

    private async Task<bool> TryReloadWorkspaceEditorByKeyAsync(string key, bool updateStatus)
    {
        if (updateStatus)
            StatusText = $"Loading {key}...";

        try
        {
            var fresh = await _mediator.QueryAsync<GetWorkspaceByIdQuery, McpWorkspaceItem?>(
                new GetWorkspaceByIdQuery(key));
            if (fresh == null)
            {
                if (updateStatus)
                    StatusText = $"Workspace {key} not found";
                return false;
            }

            PopulateEditor(fresh);
            var resolvedKey = ResolveKey(fresh) ?? key;
            SetEditingWorkspaceKey(resolvedKey);
            SelectEntryByKey(resolvedKey);
            if (updateStatus)
                StatusText = $"Loaded {resolvedKey}";
            return true;
        }
        catch (Exception ex)
        {
            if (updateStatus)
                StatusText = "Error: " + ex.Message;
            return false;
        }
    }

    private async Task LoadSelectedWorkspaceDetailsAsync(string key, long loadSequence)
    {
        try
        {
            var fresh = await _mediator.QueryAsync<GetWorkspaceByIdQuery, McpWorkspaceItem?>(
                new GetWorkspaceByIdQuery(key));

            if (loadSequence != _selectionDetailsLoadSequence)
                return;

            if (!string.Equals(SelectedEntry?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            if (fresh == null)
            {
                StatusText = $"Workspace {key} not found";
                return;
            }

            PopulateEditor(fresh);
            var resolvedKey = ResolveKey(fresh) ?? key;
            SetEditingWorkspaceKey(resolvedKey);

            if (!string.Equals(resolvedKey, key, StringComparison.OrdinalIgnoreCase))
                SelectEntryByKey(resolvedKey);
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
        if (string.IsNullOrWhiteSpace(key)) return;
        var entry = FilteredItems.FirstOrDefault(e =>
            string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
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

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BlankToNullPreserveContent(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task LoadGlobalPromptCoreAsync(bool updateStatusBar)
    {
        if (IsGlobalPromptLoading)
            return;

        IsGlobalPromptLoading = true;
        GlobalPromptStatusText = "Loading global prompt...";

        try
        {
            var result = await _mediator.QueryAsync<GetWorkspaceGlobalPromptQuery, McpWorkspaceGlobalPromptResult>(
                new GetWorkspaceGlobalPromptQuery());
            GlobalPromptTemplateText = result.Template ?? "";
            GlobalPromptIsDefault = result.IsDefault;
            _hasLoadedGlobalPrompt = true;
            GlobalPromptStatusText = result.IsDefault
                ? "Loaded global prompt (built-in default)"
                : "Loaded global prompt";

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

    private void RaiseWorkspaceCatalogChanged(
        WorkspaceCatalogChangeKind changeKind,
        string? fallbackKey,
        McpWorkspaceItem? workspace)
    {
        var key = ResolveKey(workspace) ?? NullIfWhiteSpace(fallbackKey);
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
        var key = _editingWorkspaceKey ??
                  NullIfWhiteSpace(SelectedEntry?.Item.WorkspacePath) ??
                  NullIfWhiteSpace(EditorWorkspacePath);
        return key;
    }

    private async Task CheckWorkspaceHealthForSelectionAsync(bool updateStatusText)
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
            var health = await _mediator.QueryAsync<GetWorkspaceHealthQuery, McpWorkspaceHealthResult>(
                new GetWorkspaceHealthQuery(key));

            if (!string.Equals(SelectedEntry?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            ProcessStatusText = FormatHealthStatus(health);
            UpdateHealthIndicator(health.Success, health.Success
                ? $"Healthy: {key}"
                : $"Unhealthy: {key}");

            if (updateStatusText)
                StatusText = health.Success
                    ? $"Health OK for {key}"
                    : $"Health failed for {key}";
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
        _healthTimer = new Timer(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _ = CheckWorkspaceHealthForSelectionAsync(updateStatusText: false);
            });
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
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
            true => Brushes.LimeGreen,
            false => Brushes.IndianRed,
            _ => Brushes.Gray
        };
        HealthIndicatorTooltip = tooltip;
    }

    private static string FormatProcessStatus(McpWorkspaceProcessStatus status)
    {
        var state = status.IsRunning ? "Running" : "Stopped";
        var pid = status.Pid.HasValue ? $"PID {status.Pid.Value}" : "PID n/a";
        var port = status.Port.HasValue ? $"Port {status.Port.Value}" : "Port n/a";
        var uptime = string.IsNullOrWhiteSpace(status.Uptime) ? "" : $", Uptime {status.Uptime}";
        var error = string.IsNullOrWhiteSpace(status.Error) ? "" : $" ({status.Error})";
        return $"{state}, {pid}, {port}{uptime}{error}";
    }

    private bool CanCheckSelectedWorkspaceHealth() => SelectedEntry?.Item != null;

    private static string FormatHealthStatus(McpWorkspaceHealthResult health)
    {
        var status = health.StatusCode > 0 ? $"HTTP {health.StatusCode}" : "HTTP n/a";
        var url = string.IsNullOrWhiteSpace(health.Url) ? "" : $" @ {health.Url}";
        var body = string.IsNullOrWhiteSpace(health.Body)
            ? ""
            : $" | {TruncateSingleLine(health.Body, 180)}";
        var error = string.IsNullOrWhiteSpace(health.Error) ? "" : $" ({health.Error})";
        return $"{(health.Success ? "Healthy" : "Unhealthy")} {status}{url}{body}{error}";
    }

    private static string TruncateSingleLine(string text, int maxLength)
    {
        var singleLine = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (singleLine.Length <= maxLength) return singleLine;
        return singleLine.Substring(0, Math.Max(0, maxLength - 3)) + "...";
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
