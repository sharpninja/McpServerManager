using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private readonly Mediator _mediator = new();
    private readonly IClipboardService _clipboardService;
    private readonly List<WorkspaceListEntry> _allEntries = new();
    private string? _editingWorkspaceKey;
    private bool _isBusyHandlerRegistered;

    [ObservableProperty] private ObservableCollection<WorkspaceListEntry> _filteredItems = new();
    [ObservableProperty] private WorkspaceListEntry? _selectedEntry;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _processStatusText = "";

    [ObservableProperty] private string _editorKey = "";
    [ObservableProperty] private string _editorName = "";
    [ObservableProperty] private string _editorWorkspacePath = "";
    [ObservableProperty] private string _editorTodoPath = "";
    [ObservableProperty] private string _editorWorkspacePortText = "";
    [ObservableProperty] private string _editorTunnelProvider = "";
    [ObservableProperty] private string _editorRunAs = "";

    public bool IsEditingExisting => !string.IsNullOrWhiteSpace(_editingWorkspaceKey);
    public string EditorModeText => IsEditingExisting
        ? $"Editing workspace: {_editingWorkspaceKey}"
        : "Creating new workspace";

    public event Action<string>? GlobalStatusChanged;

    public WorkspaceViewModel(IClipboardService clipboardService, string mcpBaseUrl)
    {
        _clipboardService = clipboardService;
        SetMcpBaseUrl(mcpBaseUrl);
        NewWorkspace();
    }

    public WorkspaceViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        SetMcpBaseUrl(AppSettings.ResolveMcpBaseUrl());
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
        _mediator.Register<CreateWorkspaceCommand, McpWorkspaceMutationResult>(new CreateWorkspaceHandler(service));
        _mediator.Register<UpdateWorkspaceCommand, McpWorkspaceMutationResult>(new UpdateWorkspaceHandler(service));
        _mediator.Register<DeleteWorkspaceCommand, McpWorkspaceMutationResult>(new DeleteWorkspaceHandler(service));
        _mediator.Register<InitWorkspaceCommand, McpWorkspaceInitResult>(new InitWorkspaceHandler(service));
        _mediator.Register<StartWorkspaceCommand, McpWorkspaceProcessStatus>(new StartWorkspaceHandler(service));
        _mediator.Register<StopWorkspaceCommand, McpWorkspaceProcessStatus>(new StopWorkspaceHandler(service));
    }

    public void SetMcpBaseUrl(string mcpBaseUrl)
    {
        RegisterCqrsHandlers(new McpWorkspaceService(mcpBaseUrl));
    }

    public Task RefreshForConnectionChangeAsync() => LoadWorkspacesAsync();

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
        if (value?.Item == null) return;
        PopulateEditor(value.Item);
        SetEditingWorkspaceKey(value.Key);
    }

    [RelayCommand]
    private async Task LoadWorkspacesAsync()
    {
        IsLoading = true;
        StatusText = "Loading...";
        GlobalStatusChanged?.Invoke("Loading workspaces...");

        var selectedKey = SelectedEntry?.Key ?? _editingWorkspaceKey;
        try
        {
            var result = await _mediator.QueryAsync<QueryWorkspacesQuery, McpWorkspaceQueryResult>(new QueryWorkspacesQuery());

            _allEntries.Clear();
            _allEntries.AddRange(result.Items.Select(ToEntry)
                .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase));

            ApplyFilters();
            SelectedEntry = null;
            SelectEntryByKey(selectedKey);

            StatusText = $"{result.TotalCount} workspace(s)";
            GlobalStatusChanged?.Invoke(StatusText);
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

    [RelayCommand]
    private async Task RefreshAsync() => await LoadWorkspacesAsync();

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
        ProcessStatusText = "";
        EditorKey = "";
        EditorName = "";
        EditorWorkspacePath = "";
        EditorTodoPath = "";
        EditorWorkspacePortText = "";
        EditorTunnelProvider = "";
        EditorRunAs = "";
        StatusText = "New workspace draft";
    }

    [RelayCommand]
    private async Task OpenSelectedWorkspaceAsync()
    {
        var key = GetKeyForActions();
        if (string.IsNullOrWhiteSpace(key)) return;

        StatusText = $"Loading {key}...";
        try
        {
            var fresh = await _mediator.QueryAsync<GetWorkspaceByIdQuery, McpWorkspaceItem?>(
                new GetWorkspaceByIdQuery(key));
            if (fresh == null)
            {
                StatusText = $"Workspace {key} not found";
                return;
            }

            PopulateEditor(fresh);
            SetEditingWorkspaceKey(ResolveKey(fresh));
            StatusText = $"Loaded {key}";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
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
            StatusText = $"Start requested for {key}";
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

        if (!TryParseWorkspacePort(EditorWorkspacePortText, out var workspacePort, out var parseError))
        {
            StatusText = parseError;
            return;
        }

        var request = new McpWorkspaceCreateRequest
        {
            Name = NullIfWhiteSpace(EditorName),
            WorkspacePath = NullIfWhiteSpace(EditorWorkspacePath),
            TodoPath = NullIfWhiteSpace(EditorTodoPath),
            WorkspacePort = workspacePort,
            TunnelProvider = NullIfWhiteSpace(EditorTunnelProvider),
            RunAs = NullIfWhiteSpace(EditorRunAs)
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

        if (!TryParseWorkspacePort(EditorWorkspacePortText, out var workspacePort, out var parseError))
        {
            StatusText = parseError;
            return;
        }

        var request = new McpWorkspaceUpdateRequest
        {
            Name = NullIfWhiteSpace(EditorName),
            TodoPath = NullIfWhiteSpace(EditorTodoPath),
            WorkspacePort = workspacePort,
            TunnelProvider = NullIfWhiteSpace(EditorTunnelProvider),
            RunAs = NullIfWhiteSpace(EditorRunAs)
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
        var subtitle = string.IsNullOrWhiteSpace(item.WorkspacePath)
            ? $"Port {item.WorkspacePort}"
            : $"{item.WorkspacePath} | Port {item.WorkspacePort}";
        var searchable = string.Join(" ",
            new[]
            {
                key,
                item.Name,
                item.WorkspacePath,
                item.TodoPath,
                item.TunnelProvider,
                item.RunAs,
                item.WorkspacePort.ToString()
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
        EditorWorkspacePortText = item.WorkspacePort > 0 ? item.WorkspacePort.ToString() : "";
        EditorTunnelProvider = item.TunnelProvider ?? "";
        EditorRunAs = item.RunAs ?? "";
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

    private string? GetKeyForActions()
    {
        var key = _editingWorkspaceKey ??
                  NullIfWhiteSpace(SelectedEntry?.Item.WorkspacePath) ??
                  NullIfWhiteSpace(EditorWorkspacePath);
        return key;
    }

    private static bool TryParseWorkspacePort(string? text, out int? port, out string error)
    {
        port = null;
        error = "";
        var raw = (text ?? "").Trim();
        if (raw.Length == 0) return true;

        if (!int.TryParse(raw, out var parsed))
        {
            error = "Workspace Port must be a whole number";
            return false;
        }
        if (parsed < 1 || parsed > 65535)
        {
            error = "Workspace Port must be between 1 and 65535";
            return false;
        }
        port = parsed;
        return true;
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
}

public sealed class WorkspaceListEntry
{
    public McpWorkspaceItem Item { get; init; } = new();
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string SearchText { get; init; } = "";
}
