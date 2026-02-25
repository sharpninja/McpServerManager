using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Converters;
using McpServerManager.Core.Models;
using McpServerManager.Core.Models.Json;
using McpServerManager.Core.Services;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Commands;

namespace McpServerManager.Core.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string AgentsReadmeFileName = "AGENTS-README-FIRST.yaml";

    private static readonly JsonSerializerOptions CopilotJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new WorkspaceInfoConverter() }
    };

    /// <summary>
    /// Target path resolved for the current OS (Windows path on Windows, /mnt/... on Linux).
    /// </summary>
    private static string GetResolvedTargetPath() => PathConverter.ToDisplayPath(AppSettings.ResolveSessionsRootPath());

    private static string GetHtmlCacheDir() => AppSettings.ResolveHtmlCacheDirectory();
    private static string GetMcpBaseUrl() => AppSettings.ResolveMcpBaseUrl();
    private const string McpSessionPrefix = "MCP_SESSION://";
    private const string McpAgentPrefix = "MCP_AGENT://";

    private readonly IClipboardService _clipboardService;
    private string _defaultMcpBaseUrl = "";
    private string? _defaultMcpApiKey;
    private Uri _defaultMcpBaseUri = new("http://localhost");
    private string _activeMcpBaseUrl = "";
    private string? _activeMcpApiKey;
    internal McpSessionLogService McpSessionService = null!;
    private McpTodoService _mcpTodoService = null!;
    private McpWorkspaceService _mcpWorkspaceService = null!;
    private McpWorkspaceService _workspaceCatalogService = null!;
    private bool _suppressWorkspaceSelectionChanged;
    private bool _hasRegisteredCqrsHandlers;
    internal readonly Mediator _mediator = new();
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("ViewModel");

    /// <summary>ViewModel for the Todo tab. Created lazily on first access.</summary>
    public TodoListViewModel TodoViewModel => _todoViewModel ??= CreateTodoViewModel();
    private TodoListViewModel? _todoViewModel;

    private TodoListViewModel CreateTodoViewModel()
    {
        var vm = new TodoListViewModel(_clipboardService, _activeMcpBaseUrl, _activeMcpApiKey);
        vm.GlobalStatusChanged += msg => DispatchToUi(() => StatusMessage = msg);
        return vm;
    }

    /// <summary>ViewModel for the Workspace tab. Created lazily on first access.</summary>
    public WorkspaceViewModel WorkspaceViewModel => _workspaceViewModel ??= CreateWorkspaceViewModel();
    private WorkspaceViewModel? _workspaceViewModel;

    private WorkspaceViewModel CreateWorkspaceViewModel()
    {
        var vm = new WorkspaceViewModel(_clipboardService, _defaultMcpBaseUrl, _defaultMcpApiKey);
        vm.GlobalStatusChanged += msg => DispatchToUi(() => StatusMessage = msg);
        vm.WorkspaceCatalogChanged += change => _ = RefreshWorkspacePickerAfterCatalogChangeAsync(change);
        return vm;
    }

    /// <summary>ViewModel for the Log tab. Created lazily on first access.</summary>
    public LogViewModel LogViewModel => _logViewModel ??= new LogViewModel(_clipboardService);
    private LogViewModel? _logViewModel;
    internal List<UnifiedSessionLog> _mcpSessions = new();
    internal Dictionary<string, UnifiedSessionLog> _mcpSessionsByPath = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Cached sessions from ReloadFromMcpAsyncInternal, consumed once by the auto-triggered ALL_JSON load.</summary>
    private IReadOnlyList<UnifiedSessionLog>? _cachedSessionsForAutoLoad;
    private Timer? _mcpAutoRefreshTimer;
    private bool _isRefreshing;
    private Timer? _workspaceHealthTimer;
    private bool _isWorkspaceHealthCheckRunning;
    private bool _pendingWorkspaceHealthRefresh;
    private FileSystemWatcher? _agentsReadmeWatcher;
    private string? _agentsReadmeWatchedFilePath;
    private int _agentsReadmeReloadVersion;

    [ObservableProperty]
    private ObservableCollection<FileNode> _nodes = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedNodePathDisplay))]
    private FileNode? _selectedNode;

    /// <summary>Null-safe path for binding (avoids 'Value is null' when SelectedNode is null).</summary>
    public string SelectedNodePathDisplay => SelectedNode?.Path ?? "";

    [ObservableProperty]
    private ObservableCollection<string> _changeLog = new();

    [ObservableProperty]
    private bool _isMarkdownVisible = true;

    [ObservableProperty]
    private bool _isJsonVisible = false;

    [ObservableProperty]
    private bool _isRequestDetailsVisible = false;

    // Phone navigation section visibility (for NavigationView-based phone UI)
    [ObservableProperty]
    private bool _isPhoneSessionsVisible = true;

    [ObservableProperty]
    private bool _isPhoneViewerVisible = false;

    [ObservableProperty]
    private bool _isPhoneHistoryVisible = false;

    [RelayCommand]
    private void PhoneNavigateSection(string? sectionKey)
        => _mediator.SendAsync(new Commands.PhoneNavigateSectionCommand(this, sectionKey));

    internal void PhoneNavigateSectionInternal(string? sectionKey)
    {
        IsPhoneSessionsVisible = sectionKey == "Sessions";
        IsPhoneViewerVisible = sectionKey == "Viewer";
        IsPhoneHistoryVisible = sectionKey == "History";
    }

    [ObservableProperty]
    private UnifiedRequestEntry? _selectedUnifiedRequest;

    [ObservableProperty]
    private ObservableCollection<JsonTreeNode> _jsonTree = new();

    [ObservableProperty]
    private JsonLogSummary _jsonLogSummary = new();

    [ObservableProperty]
    private ObservableCollection<SearchableEntry> _searchableEntries = new();

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _requestIdFilter = "";

    [ObservableProperty]
    private string _displayFilter = "";

    [ObservableProperty]
    private string _modelFilter = "";

    [ObservableProperty]
    private string _timestampFilter = "";

    [ObservableProperty]
    private string _agentFilter = "";

    /// <summary>Distinct values for filter ComboBoxes (includes "" for "All").</summary>
    [ObservableProperty]
    private ObservableCollection<string> _distinctRequestIds = new() { "" };

    [ObservableProperty]
    private ObservableCollection<string> _distinctDisplayTexts = new() { "" };

    [ObservableProperty]
    private ObservableCollection<string> _distinctModels = new() { "" };

    [ObservableProperty]
    private ObservableCollection<string> _distinctAgents = new() { "" };

    [ObservableProperty]
    private ObservableCollection<string> _distinctTimestamps = new() { "" };

    [ObservableProperty]
    private ObservableCollection<SearchableEntry> _filteredSearchEntries = new();

    [ObservableProperty]
    private SearchableEntry? _selectedSearchEntry;

    [ObservableProperty]
    private JsonTreeNode? _selectedJsonNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppVersionDisplay))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _appVersion = ResolveAppVersion();

    public string AppVersionDisplay => $"v{AppVersion}";

    /// <summary>Window title including the full SemVer from GitVersion.</summary>
    public string WindowTitle => $"McpServerManager {AppVersionDisplay}";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<WorkspaceConnectionOption> _workspaceConnections = new();

    [ObservableProperty]
    private WorkspaceConnectionOption? _selectedWorkspaceConnection;

    [ObservableProperty]
    private IBrush _workspaceHealthIndicatorBrush = Brushes.Gray;

    [ObservableProperty]
    private string _workspaceHealthIndicatorTooltip = "Select a workspace";

    [ObservableProperty]
    private bool _isSwitchingWorkspace;

    [ObservableProperty]
    private string _agentsReadmeFilePath = "";

    [ObservableProperty]
    private string _agentsReadmeFileTimestampText = "";

    [ObservableProperty]
    private string _agentsReadmeLoadedAtLocalText = "";

    [ObservableProperty]
    private string _agentsReadmeStatusText = "Waiting for workspace selection...";

    [ObservableProperty]
    private string _agentsReadmeContent = "";

    /// <summary>True while an async operation (MCP refresh, JSON load, etc.) is in progress.</summary>
    [ObservableProperty]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        _logger.LogDebug($"[ViewModel] IsBusy changed to {value}, mediator.IsBusy={_mediator.IsBusy}");
    }

    /// <summary>True when markdown preview was opened in the system browser.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMarkdownLoadingPlaceholder))]
    private bool _isPreviewOpenedInBrowser;

    /// <summary>True when markdown is selected but preview not yet loaded (show loading message).</summary>
    public bool ShowMarkdownLoadingPlaceholder => !IsPreviewOpenedInBrowser;

    /// <summary>Path to the current preview HTML file (for "Open in browser" button).</summary>
    [ObservableProperty]
    private string? _currentPreviewHtmlPath;

    /// <summary>When true, show markdown as raw text instead of rendered (use if a doc does not display).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRenderedMarkdown))]
    private bool _showMarkdownAsRawText;

    /// <summary>True when the rendered markdown viewer should be visible (inverse of ShowMarkdownAsRawText).</summary>
    public bool ShowRenderedMarkdown => !ShowMarkdownAsRawText;

    /// <summary>Raw markdown of the selected file when preview is opened externally.</summary>
    [ObservableProperty]
    private string _currentPreviewMarkdownText = "";

    private string? _currentMarkdownPath;

    /// <summary>Path we last navigated to; used to avoid reloading markdown when selection is restored to same path (e.g. after tree rebuild).</summary>
    private string? _lastNavigatedPath;

    private CancellationTokenSource? _markdownPreviewCts;

    /// <summary>Cancels any in-flight markdown preview task (e.g. pandoc generation).</summary>
    public void CancelMarkdownPreview()
    {
        _markdownPreviewCts?.Cancel();
        _markdownPreviewCts?.Dispose();
        _markdownPreviewCts = null;
    }

    // Navigation History
    private readonly Stack<FileNode> _backStack = new();
    private readonly Stack<FileNode> _forwardStack = new();
    private bool _isNavigatingHistory;

    public MainWindowViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        InitializeMcpEndpoint(GetMcpBaseUrl());
        RegisterCqrsHandlers();
    }

    public MainWindowViewModel(IClipboardService clipboardService, string mcpBaseUrl)
    {
        _clipboardService = clipboardService;
        InitializeMcpEndpoint(mcpBaseUrl);
        RegisterCqrsHandlers();
    }

    private void InitializeMcpEndpoint(string mcpBaseUrl)
    {
        _defaultMcpBaseUrl = NormalizeMcpBaseUrl(mcpBaseUrl);
        _defaultMcpApiKey = McpServerRestClientFactory.TryResolveApiKey(_defaultMcpBaseUrl);
        _defaultMcpBaseUri = new Uri(_defaultMcpBaseUrl, UriKind.Absolute);
        _workspaceCatalogService = new McpWorkspaceService(_defaultMcpBaseUrl, _defaultMcpApiKey);
        ApplyActiveMcpBaseUrl(_defaultMcpBaseUrl, _defaultMcpApiKey);
        ApplyWorkspaceConnectionOptions(
            new[]
            {
                WorkspaceConnectionOption.CreateDefault(_defaultMcpBaseUri, primaryWorkspaceRootPath: null, apiKey: _defaultMcpApiKey)
            },
            preferredSelection: null,
            preferredBaseUrl: _defaultMcpBaseUrl);
    }

    private static string NormalizeMcpBaseUrl(string mcpBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(mcpBaseUrl))
            throw new InvalidOperationException("MCP base URL is required.");

        var trimmed = mcpBaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("MCP base URL must be an absolute URI.");

        return uri.ToString().TrimEnd('/');
    }

    private void ApplyActiveMcpBaseUrl(string mcpBaseUrl, string? mcpApiKey = null)
    {
        _activeMcpBaseUrl = NormalizeMcpBaseUrl(mcpBaseUrl);
        _activeMcpApiKey = string.IsNullOrWhiteSpace(mcpApiKey)
            ? McpServerRestClientFactory.TryResolveApiKey(_activeMcpBaseUrl)
            : mcpApiKey?.Trim();

        McpSessionService = new McpSessionLogService(_activeMcpBaseUrl, _activeMcpApiKey);
        _mcpTodoService = new McpTodoService(_activeMcpBaseUrl, _activeMcpApiKey);
        // Workspace endpoints always target the connection server, not the active workspace
        _mcpWorkspaceService = new McpWorkspaceService(_defaultMcpBaseUrl, _defaultMcpApiKey);

        if (_hasRegisteredCqrsHandlers)
            RegisterMcpServiceHandlers();

        _todoViewModel?.SetMcpBaseUrl(_activeMcpBaseUrl, _activeMcpApiKey);
        _workspaceViewModel?.SetMcpBaseUrl(_defaultMcpBaseUrl, _defaultMcpApiKey);
    }

    private async Task<string?> ResolveActiveConnectionApiKeyAsync(WorkspaceConnectionOption option, string baseUrl)
    {
        // Workspace default API keys are per-workspace and rotate on server restart.
        // Prefer fetching the current default key from /api-key for the selected connection.
        var fetchedDefaultKey = await McpServerRestClientFactory
            .TryFetchDefaultApiKeyAsync(baseUrl)
            .ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(fetchedDefaultKey))
            return fetchedDefaultKey;

        // Fall back to any key resolved from the local marker file (Desktop/dev scenarios).
        if (!string.IsNullOrWhiteSpace(option.ApiKey))
            return option.ApiKey;

        if (!string.IsNullOrWhiteSpace(option.WorkspaceRootPath))
        {
            var markerKey = McpServerRestClientFactory.TryResolveApiKeyForWorkspaceRoot(option.WorkspaceRootPath, baseUrl);
            if (!string.IsNullOrWhiteSpace(markerKey))
                return markerKey;
        }

        return null;
    }

    private void RegisterCqrsHandlers()
    {
        _mediator.IsBusyChanged += _ => DispatchToUi(() => IsBusy = _mediator.IsBusy);
        // Async operations (data loading)
        _mediator.Register(new Commands.InitializeFromMcpHandler());
        _mediator.Register(new Commands.RefreshAndLoadAllJsonHandler());
        _mediator.Register(new Commands.RefreshAndLoadAgentJsonHandler());
        _mediator.Register(new Commands.RefreshAndLoadSessionHandler());
        _mediator.Register(new Commands.LoadJsonFileHandler());
        _mediator.Register(new Commands.NavigateToNodeHandler());
        _mediator.Register(new Commands.LoadMarkdownFileHandler());
        _mediator.Register(new Commands.LoadSourceFileHandler());

        // Navigation
        _mediator.Register(new Commands.NavigateBackHandler());
        _mediator.Register(new Commands.NavigateForwardHandler());
        _mediator.Register(new Commands.RefreshViewHandler());
        _mediator.Register(new Commands.PhoneNavigateSectionHandler());
        _mediator.Register(new Commands.TreeItemTappedHandler());

        // Request details
        _mediator.Register(new Commands.ShowRequestDetailsHandler());
        _mediator.Register(new Commands.CloseRequestDetailsHandler());
        _mediator.Register(new Commands.NavigateToPreviousRequestHandler());
        _mediator.Register(new Commands.NavigateToNextRequestHandler());

        // Selection & interaction
        _mediator.Register(new Commands.SelectSearchEntryHandler());
        _mediator.Register(new Commands.JsonNodeDoubleTappedHandler());
        _mediator.Register(new Commands.SearchRowTappedHandler());
        _mediator.Register(new Commands.SearchRowDoubleTappedHandler());

        // Clipboard
        _mediator.Register(new Commands.CopyTextHandler());
        _mediator.Register(new Commands.CopyOriginalJsonHandler());

        // Preview/Markdown
        _mediator.Register(new Commands.OpenPreviewInBrowserHandler());
        _mediator.Register(new Commands.ToggleShowRawMarkdownHandler());

        // Archive
        _mediator.Register(new Commands.ArchiveCurrentHandler());
        _mediator.Register(new Commands.ArchiveTreeItemHandler());

        // Tree & config
        _mediator.Register(new Commands.OpenTreeItemHandler());
        _mediator.Register(new Commands.OpenAgentConfigHandler());
        _mediator.Register(new Commands.OpenPromptTemplatesHandler());

        // Refresh
        _mediator.Register(new Commands.RefreshHandler());

        _hasRegisteredCqrsHandlers = true;
        RegisterMcpServiceHandlers();
    }

    private void RegisterMcpServiceHandlers()
    {
        // Todo CQRS
        var todoService = _mcpTodoService;
        _mediator.RegisterQuery(new Commands.QueryTodosHandler(todoService));
        _mediator.RegisterQuery(new Commands.GetTodoByIdHandler(todoService));
        _mediator.Register<Commands.CreateTodoCommand, McpTodoMutationResult>(new Commands.CreateTodoHandler(todoService));
        _mediator.Register<Commands.UpdateTodoCommand, McpTodoMutationResult>(new Commands.UpdateTodoHandler(todoService));
        _mediator.Register<Commands.DeleteTodoCommand, McpTodoMutationResult>(new Commands.DeleteTodoHandler(todoService));
        _mediator.Register<Commands.AnalyzeTodoRequirementsCommand, McpRequirementsAnalysisResult>(new Commands.AnalyzeTodoRequirementsHandler(todoService));

        // Workspace CQRS
        var workspaceService = _mcpWorkspaceService;
        _mediator.RegisterQuery(new Commands.QueryWorkspacesHandler(workspaceService));
        _mediator.RegisterQuery(new Commands.GetWorkspaceByIdHandler(workspaceService));
        _mediator.RegisterQuery(new Commands.GetWorkspaceStatusHandler(workspaceService));
        _mediator.RegisterQuery(new Commands.GetWorkspaceHealthHandler(workspaceService));
        _mediator.Register<Commands.CreateWorkspaceCommand, McpWorkspaceMutationResult>(new Commands.CreateWorkspaceHandler(workspaceService));
        _mediator.Register<Commands.UpdateWorkspaceCommand, McpWorkspaceMutationResult>(new Commands.UpdateWorkspaceHandler(workspaceService));
        _mediator.Register<Commands.DeleteWorkspaceCommand, McpWorkspaceMutationResult>(new Commands.DeleteWorkspaceHandler(workspaceService));
        _mediator.Register<Commands.InitWorkspaceCommand, McpWorkspaceInitResult>(new Commands.InitWorkspaceHandler(workspaceService));
        _mediator.Register<Commands.StartWorkspaceCommand, McpWorkspaceProcessStatus>(new Commands.StartWorkspaceHandler(workspaceService));
        _mediator.Register<Commands.StopWorkspaceCommand, McpWorkspaceProcessStatus>(new Commands.StopWorkspaceHandler(workspaceService));
    }

    /// <summary>Command for tree item tap (handles directory expand/collapse and MCP node refresh).</summary>
    [RelayCommand]
    private void TreeItemTapped(FileNode? node)
        => _mediator.SendAsync(new Commands.TreeItemTappedCommand(this, node));

    internal void TreeItemTappedInternal(FileNode? node)
    {
        if (node == null) return;
        if (IsMcpVirtualNode(node) && SelectedNode == node)
        {
            RefreshCurrentMcpNode();
            return;
        }
        if (node.IsDirectory)
        {
            // Root "All JSON" node: select it to load/refresh aggregated JSON
            if (Nodes.Contains(node))
            {
                if (SelectedNode == node)
                    GenerateAndNavigateInternal(node);
                else
                    SelectedNode = node;
                return;
            }

            bool expanding = !node.IsExpanded;
            node.IsExpanded = expanding;

            // Accordion: collapse siblings when expanding a directory
            if (expanding)
            {
                foreach (var topNode in Nodes)
                {
                    foreach (var sibling in topNode.Children)
                    {
                        if (sibling != node && sibling.IsDirectory)
                            sibling.IsExpanded = false;
                    }
                }
            }
            return;
        }
        SelectedNode = node;
    }

    /// <summary>Command for JSON tree node double-tap (navigate to details or copy text).</summary>
    [RelayCommand]
    private void JsonNodeDoubleTapped(JsonTreeNode? node)
        => _mediator.SendAsync(new Commands.JsonNodeDoubleTappedCommand(this, node));

    internal void JsonNodeDoubleTappedInternal(JsonTreeNode? node)
    {
        if (node == null) return;
        if (!string.IsNullOrEmpty(node.SourcePath))
            TryNavigateToDetailsForSourcePath(node.SourcePath);
    }

    /// <summary>Command for search row single-tap (select entry).</summary>
    [RelayCommand]
    private void SearchRowTapped(SearchableEntry? entry)
        => _mediator.SendAsync(new Commands.SearchRowTappedCommand(this, entry));

    internal void SearchRowTappedInternal(SearchableEntry? entry)
    {
        if (entry != null)
            SelectedSearchEntry = entry;
    }

    /// <summary>Command for search row double-tap (open request details).</summary>
    [RelayCommand]
    private void SearchRowDoubleTapped(SearchableEntry? entry)
        => _mediator.SendAsync(new Commands.SearchRowDoubleTappedCommand(this, entry));

    internal void SearchRowDoubleTappedInternal(SearchableEntry? entry)
    {
        if (entry?.UnifiedEntry != null)
            ShowRequestDetailsInternal(entry);
    }

    private static string ResolveAppVersion()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString()
            : informationalVersion;

        if (string.IsNullOrWhiteSpace(version))
            return "unknown";

        var markerIndex = version.IndexOf(".Sha", StringComparison.OrdinalIgnoreCase);
        return markerIndex > 0 ? version[..markerIndex] : version;
    }

    /// <summary>Called by MainWindow when it has opened. Builds the file tree off the UI thread and applies on UI; starts the watcher.</summary>
    public void InitializeAfterWindowShown()
    {
        _ = _mediator.SendAsync(new Commands.InitializeFromMcpCommand(this));
        _ = LoadWorkspaceConnectionsAsync();
    }

    partial void OnSelectedWorkspaceConnectionChanged(WorkspaceConnectionOption? value)
    {
        if (value == null)
        {
            UpdateAgentsReadmeWatcherForSelection(null);
            StopWorkspaceHealthRefresh();
            UpdateWorkspaceHealthIndicator(null, "Select a workspace");
            return;
        }

        UpdateAgentsReadmeWatcherForSelection(value);

        StartWorkspaceHealthRefresh();
        _ = RefreshSelectedWorkspaceHealthAsync();

        if (_suppressWorkspaceSelectionChanged)
            return;

        _ = SwitchWorkspaceConnectionAsync(value);
    }

    private async Task RefreshSelectedWorkspaceHealthAsync()
    {
        var selected = SelectedWorkspaceConnection;
        if (selected == null)
        {
            UpdateWorkspaceHealthIndicator(null, "Select a workspace");
            return;
        }

        if (_isWorkspaceHealthCheckRunning)
        {
            _pendingWorkspaceHealthRefresh = true;
            return;
        }

        _pendingWorkspaceHealthRefresh = false;
        _isWorkspaceHealthCheckRunning = true;
        var baseUrl = NormalizeMcpBaseUrl(selected.BaseUrl);
        var displayName = selected.DisplayName;

        try
        {
            var service = new McpWorkspaceService(baseUrl);
            var health = await service.GetHealthAsync().ConfigureAwait(true);

            var current = SelectedWorkspaceConnection;
            if (current == null ||
                !string.Equals(NormalizeMcpBaseUrl(current.BaseUrl), baseUrl, StringComparison.OrdinalIgnoreCase))
                return;

            UpdateWorkspaceHealthIndicator(health.Success, FormatWorkspaceHealthTooltip(displayName, health));
        }
        catch (Exception ex)
        {
            var current = SelectedWorkspaceConnection;
            if (current == null ||
                !string.Equals(NormalizeMcpBaseUrl(current.BaseUrl), baseUrl, StringComparison.OrdinalIgnoreCase))
                return;

            UpdateWorkspaceHealthIndicator(false, $"Unhealthy: {displayName} ({ex.Message})");
        }
        finally
        {
            _isWorkspaceHealthCheckRunning = false;
            if (_pendingWorkspaceHealthRefresh)
            {
                _pendingWorkspaceHealthRefresh = false;
                DispatchToUi(() => _ = RefreshSelectedWorkspaceHealthAsync());
            }
        }
    }

    private void StartWorkspaceHealthRefresh()
    {
        StopWorkspaceHealthRefresh();
        var interval = TimeSpan.FromMinutes(1);
        _workspaceHealthTimer = new Timer(_ =>
        {
            DispatchToUi(() => _ = RefreshSelectedWorkspaceHealthAsync());
        }, null, interval, interval);
    }

    private void StopWorkspaceHealthRefresh()
    {
        _workspaceHealthTimer?.Dispose();
        _workspaceHealthTimer = null;
    }

    private void UpdateWorkspaceHealthIndicator(bool? isHealthy, string tooltip)
    {
        WorkspaceHealthIndicatorBrush = isHealthy switch
        {
            true => Brushes.LimeGreen,
            false => Brushes.IndianRed,
            _ => Brushes.Gray
        };
        WorkspaceHealthIndicatorTooltip = tooltip;
    }

    private void UpdateAgentsReadmeWatcherForSelection(WorkspaceConnectionOption? selection)
    {
        var workspaceRootPath = selection?.WorkspaceRootPath;
        if (string.IsNullOrWhiteSpace(workspaceRootPath))
        {
            ReplaceAgentsReadmeWatcher(null);
            DispatchToUi(() =>
            {
                AgentsReadmeFilePath = "";
                AgentsReadmeFileTimestampText = "";
                AgentsReadmeLoadedAtLocalText = "";
                AgentsReadmeContent = "";
                AgentsReadmeStatusText = selection == null
                    ? "Waiting for workspace selection..."
                    : $"AGENTS file unavailable: no workspace root path for {selection.DisplayName}.";
            });
            return;
        }

        var filePath = Path.Combine(workspaceRootPath.Trim(), AgentsReadmeFileName);
        ReplaceAgentsReadmeWatcher(filePath);
        DispatchToUi(() => AgentsReadmeFilePath = filePath);
        QueueAgentsReadmeReload(filePath);
    }

    private void ReplaceAgentsReadmeWatcher(string? filePath)
    {
        if (string.Equals(_agentsReadmeWatchedFilePath, filePath, StringComparison.OrdinalIgnoreCase))
            return;

        if (_agentsReadmeWatcher != null)
        {
            try
            {
                _agentsReadmeWatcher.EnableRaisingEvents = false;
                _agentsReadmeWatcher.Changed -= OnAgentsReadmeFileChanged;
                _agentsReadmeWatcher.Created -= OnAgentsReadmeFileChanged;
                _agentsReadmeWatcher.Deleted -= OnAgentsReadmeFileChanged;
                _agentsReadmeWatcher.Renamed -= OnAgentsReadmeFileRenamed;
                _agentsReadmeWatcher.Error -= OnAgentsReadmeWatcherError;
                _agentsReadmeWatcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing AGENTS file watcher");
            }
            finally
            {
                _agentsReadmeWatcher = null;
            }
        }

        _agentsReadmeWatchedFilePath = filePath;
        Interlocked.Increment(ref _agentsReadmeReloadVersion);

        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var directory = Path.GetDirectoryName(filePath);
        var filter = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(filter) || !Directory.Exists(directory))
            return;

        try
        {
            _agentsReadmeWatcher = new FileSystemWatcher(directory, filter)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };
            _agentsReadmeWatcher.Changed += OnAgentsReadmeFileChanged;
            _agentsReadmeWatcher.Created += OnAgentsReadmeFileChanged;
            _agentsReadmeWatcher.Deleted += OnAgentsReadmeFileChanged;
            _agentsReadmeWatcher.Renamed += OnAgentsReadmeFileRenamed;
            _agentsReadmeWatcher.Error += OnAgentsReadmeWatcherError;
            _agentsReadmeWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start AGENTS file watcher for {FilePath}", filePath);
            DispatchToUi(() => AgentsReadmeStatusText = $"AGENTS watcher failed: {ex.Message}");
        }
    }

    private void OnAgentsReadmeFileChanged(object sender, FileSystemEventArgs e)
    {
        var watched = _agentsReadmeWatchedFilePath;
        if (string.IsNullOrWhiteSpace(watched))
            return;

        if (!string.Equals(e.FullPath, watched, StringComparison.OrdinalIgnoreCase))
            return;

        QueueAgentsReadmeReload(watched);
    }

    private void OnAgentsReadmeFileRenamed(object sender, RenamedEventArgs e)
    {
        var watched = _agentsReadmeWatchedFilePath;
        if (string.IsNullOrWhiteSpace(watched))
            return;

        if (!string.Equals(e.FullPath, watched, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(e.OldFullPath, watched, StringComparison.OrdinalIgnoreCase))
            return;

        QueueAgentsReadmeReload(watched);
    }

    private void OnAgentsReadmeWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogWarning(ex, "AGENTS file watcher error");
        DispatchToUi(() =>
        {
            AgentsReadmeStatusText = $"AGENTS watcher error: {ex?.Message ?? "Unknown error"}";
        });
    }

    private void QueueAgentsReadmeReload(string filePath)
    {
        var version = Interlocked.Increment(ref _agentsReadmeReloadVersion);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150).ConfigureAwait(false);
                if (version != Volatile.Read(ref _agentsReadmeReloadVersion))
                    return;

                await LoadAgentsReadmeFileAsync(filePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AGENTS file reload scheduling failed");
            }
        });
    }

    private async Task LoadAgentsReadmeFileAsync(string filePath)
    {
        var loadedAtLocal = DateTimeOffset.Now;
        string content = "";
        string status;
        string fileTimestampText = "";

        try
        {
            if (!File.Exists(filePath))
            {
                status = $"AGENTS file not found: {filePath}";
            }
            else
            {
                content = await ReadTextFileWithEncodingAsync(filePath).ConfigureAwait(false);
                var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
                if (lastWriteUtc != DateTime.MinValue)
                    fileTimestampText = lastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");

                status = "AGENTS file loaded.";
            }
        }
        catch (Exception ex)
        {
            status = $"AGENTS file load failed: {ex.Message}";
        }

        await DispatchToUiAsync(() =>
        {
            if (!string.Equals(_agentsReadmeWatchedFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                return;

            AgentsReadmeFilePath = filePath;
            AgentsReadmeFileTimestampText = fileTimestampText;
            AgentsReadmeLoadedAtLocalText = loadedAtLocal.ToString("yyyy-MM-dd HH:mm:ss zzz");
            AgentsReadmeContent = content;
            AgentsReadmeStatusText = status;
        }).ConfigureAwait(true);
    }

    private static string FormatWorkspaceHealthTooltip(string displayName, McpWorkspaceHealthResult health)
    {
        var status = health.StatusCode > 0 ? $"HTTP {health.StatusCode}" : "HTTP n/a";
        var endpoint = string.IsNullOrWhiteSpace(health.Url) ? "" : $" @ {health.Url}";
        var error = string.IsNullOrWhiteSpace(health.Error) ? "" : $" ({health.Error})";
        return $"{(health.Success ? "Healthy" : "Unhealthy")}: {displayName} - {status}{endpoint}{error}";
    }

    [RelayCommand]
    private async Task LoadWorkspaceConnectionsAsync()
    {
        var preferredSelection = SelectedWorkspaceConnection;
        var preferredBaseUrl = preferredSelection?.BaseUrl ?? _activeMcpBaseUrl;

        try
        {
            await LoadWorkspaceConnectionsAsync(preferredSelection, preferredBaseUrl, suppressStatusFailure: false)
                .ConfigureAwait(true);
        }
        catch
        {
            // Failure status is already handled by the shared loader for command-driven refreshes.
        }
    }

    private async Task LoadWorkspaceConnectionsAsync(
        WorkspaceConnectionOption? preferredSelection,
        string preferredBaseUrl,
        bool suppressStatusFailure)
    {
        preferredBaseUrl = string.IsNullOrWhiteSpace(preferredBaseUrl)
            ? _activeMcpBaseUrl
            : preferredBaseUrl;

        try
        {
            var query = await _workspaceCatalogService.QueryAsync().ConfigureAwait(true);
            var options = BuildWorkspaceConnectionOptions(query.Items);
            DispatchToUi(() => ApplyWorkspaceConnectionOptions(options, preferredSelection, preferredBaseUrl));
        }
        catch (Exception ex)
        {
            if (!suppressStatusFailure)
                DispatchToUi(() => StatusMessage = $"Workspace list failed: {ex.Message}");
            throw;
        }
    }

    private List<WorkspaceConnectionOption> BuildWorkspaceConnectionOptions(List<McpWorkspaceItem>? workspaces)
    {
        var defaultWorkspaceRootPath = workspaces?
            .FirstOrDefault(item => item.IsPrimary == true && !string.IsNullOrWhiteSpace(item.WorkspacePath))
            ?.WorkspacePath?
            .Trim();

        var options = new List<WorkspaceConnectionOption>
        {
            WorkspaceConnectionOption.CreateDefault(_defaultMcpBaseUri, defaultWorkspaceRootPath, _defaultMcpApiKey)
        };

        if (workspaces == null)
            return options;

        foreach (var workspace in workspaces)
        {
            if (workspace.WorkspacePort is < 1 or > 65535)
                continue;

            var candidate = WorkspaceConnectionOption.FromWorkspace(_defaultMcpBaseUri, workspace);
            options.Add(candidate);
        }

        return options;
    }

    private void ApplyWorkspaceConnectionOptions(
        IEnumerable<WorkspaceConnectionOption> options,
        WorkspaceConnectionOption? preferredSelection,
        string preferredBaseUrl)
    {
        var list = options.ToList();
        if (list.Count == 0)
            list.Add(WorkspaceConnectionOption.CreateDefault(_defaultMcpBaseUri, primaryWorkspaceRootPath: null, apiKey: _defaultMcpApiKey));

        var normalizedPreferred = NormalizeMcpBaseUrl(preferredBaseUrl);
        WorkspaceConnections = new ObservableCollection<WorkspaceConnectionOption>(list);
        var selected = FindWorkspaceConnectionOption(preferredSelection)
            ?? WorkspaceConnections.FirstOrDefault(item =>
                string.Equals(NormalizeMcpBaseUrl(item.BaseUrl), normalizedPreferred, StringComparison.OrdinalIgnoreCase))
            ?? WorkspaceConnections[0];

        _suppressWorkspaceSelectionChanged = true;
        SelectedWorkspaceConnection = selected;
        _suppressWorkspaceSelectionChanged = false;

        // Initial picker population suppresses selection-changed switching to avoid duplicate work,
        // but we still need one real connection switch to resolve the active API key and refresh views.
        var selectedBaseUrl = NormalizeMcpBaseUrl(selected.BaseUrl);
        var activeBaseUrl = string.IsNullOrWhiteSpace(_activeMcpBaseUrl) ? "" : NormalizeMcpBaseUrl(_activeMcpBaseUrl);
        var needsInitialSwitch = string.IsNullOrWhiteSpace(_activeMcpApiKey)
            || !string.Equals(activeBaseUrl, selectedBaseUrl, StringComparison.OrdinalIgnoreCase);
        if (needsInitialSwitch && !IsSwitchingWorkspace)
            _ = SwitchWorkspaceConnectionAsync(selected);
    }

    private async Task RefreshWorkspacePickerAfterCatalogChangeAsync(WorkspaceCatalogChangeEvent change)
    {
        var preferredSelection = SelectedWorkspaceConnection;
        var preferredBaseUrl = preferredSelection?.BaseUrl ?? _activeMcpBaseUrl;

        try
        {
            await LoadWorkspaceConnectionsAsync(preferredSelection, preferredBaseUrl, suppressStatusFailure: true)
                .ConfigureAwait(true);

            if (change.ChangeKind == WorkspaceCatalogChangeKind.Deleted)
                return;

            await DispatchToUiAsync(() =>
            {
                var present = WorkspaceConnections.Any(item =>
                    !item.IsDefault &&
                    !string.IsNullOrWhiteSpace(item.WorkspaceKey) &&
                    string.Equals(item.WorkspaceKey, change.WorkspaceKey, StringComparison.OrdinalIgnoreCase));

                if (present)
                    return;

                if (change.WorkspacePort is < 1 or > 65535)
                {
                    var portLabel = change.WorkspacePort?.ToString() ?? "n/a";
                    StatusMessage = $"Saved {change.WorkspaceKey}, but workspace picker skipped it due to invalid port ({portLabel}).";
                    return;
                }

                StatusMessage = $"Saved {change.WorkspaceKey}, but workspace picker did not include it after refresh.";
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            DispatchToUi(() =>
            {
                StatusMessage = $"Workspace picker refresh failed after {change.ChangeKind.ToString().ToLowerInvariant()} {change.WorkspaceKey}: {ex.Message}";
            });
        }
    }

    private async Task SwitchWorkspaceConnectionAsync(WorkspaceConnectionOption option)
    {
        _logger.LogInformation($"[Workspace Switch] Switching to '{option.DisplayName}' (BaseUrl={option.BaseUrl})");
        var previousBaseUrl = _activeMcpBaseUrl;
        var previousApiKey = _activeMcpApiKey;
        var previousSelection = FindWorkspaceConnectionOptionByBaseUrl(previousBaseUrl);
        var selectedBaseUrl = NormalizeMcpBaseUrl(option.BaseUrl);
        IsSwitchingWorkspace = true;

        try
        {
            var preflight = await ProbeWorkspaceConnectionHealthAsync(selectedBaseUrl).ConfigureAwait(true);
            if (!preflight.Success)
            {
                throw new InvalidOperationException($"Health check failed ({FormatHealthFailure(preflight)})");
            }

            var selectedApiKey = await ResolveActiveConnectionApiKeyAsync(option, selectedBaseUrl).ConfigureAwait(true);
            ApplyActiveMcpBaseUrl(selectedBaseUrl, selectedApiKey);
            await RefreshAllViewsForConnectionChangeAsync().ConfigureAwait(true);
            _logger.LogInformation($"[Workspace Switch] Successfully connected to '{option.DisplayName}'");
            DispatchToUi(() => StatusMessage = $"Connected: {option.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Workspace Switch] Failed switching to '{option.DisplayName}'");
            ApplyActiveMcpBaseUrl(previousBaseUrl, previousApiKey);
            var revertedDisplayName = previousSelection?.DisplayName ?? previousBaseUrl;
            await DispatchToUiAsync(() =>
            {
                RestoreWorkspaceSelection(previousSelection, previousBaseUrl);
                StatusMessage = $"Workspace switch failed: {option.DisplayName} ({ex.Message}). Reverted to {revertedDisplayName}.";
            }).ConfigureAwait(true);
        }
        finally
        {
            IsSwitchingWorkspace = false;
        }
    }

    private async Task RefreshAllViewsForConnectionChangeAsync()
    {
        _logger.LogDebug("[Workspace Switch] Refreshing session logs...");
        try
        {
            await ReloadFromMcpAsyncInternal().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "[Workspace Switch] Session log refresh failed (endpoint may not exist on target workspace); continuing with remaining views");
        }

        if (_todoViewModel != null)
        {
            _logger.LogDebug("[Workspace Switch] Refreshing todo view...");
            try
            {
                await _todoViewModel.RefreshForConnectionChangeAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "[Workspace Switch] Todo refresh failed; continuing");
            }
        }

        if (_workspaceViewModel != null)
        {
            _logger.LogDebug("[Workspace Switch] Refreshing workspace view...");
            try
            {
                await _workspaceViewModel.RefreshForConnectionChangeAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "[Workspace Switch] Workspace view refresh failed; continuing");
            }
        }

        _logger.LogDebug("[Workspace Switch] All views refreshed.");
    }

    private async Task<McpWorkspaceHealthResult> ProbeWorkspaceConnectionHealthAsync(string baseUrl)
    {
        var service = new McpWorkspaceService(baseUrl);
        return await service.GetHealthAsync().ConfigureAwait(true);
    }

    private static string FormatHealthFailure(McpWorkspaceHealthResult health)
    {
        var status = health.StatusCode > 0 ? $"HTTP {health.StatusCode}" : "HTTP n/a";
        var target = string.IsNullOrWhiteSpace(health.Url) ? "" : $" @ {health.Url}";
        var error = string.IsNullOrWhiteSpace(health.Error) ? "" : $" - {health.Error}";
        return $"{status}{target}{error}";
    }

    private WorkspaceConnectionOption? FindWorkspaceConnectionOptionByBaseUrl(string baseUrl)
    {
        var normalized = NormalizeMcpBaseUrl(baseUrl);
        return WorkspaceConnections.FirstOrDefault(item =>
            string.Equals(NormalizeMcpBaseUrl(item.BaseUrl), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private WorkspaceConnectionOption? FindWorkspaceConnectionOption(WorkspaceConnectionOption? preferredSelection)
    {
        if (preferredSelection == null)
            return null;

        if (preferredSelection.IsDefault)
        {
            return WorkspaceConnections.FirstOrDefault(item =>
                item.IsDefault &&
                string.Equals(NormalizeMcpBaseUrl(item.BaseUrl), NormalizeMcpBaseUrl(preferredSelection.BaseUrl), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(preferredSelection.WorkspaceKey))
        {
            var byWorkspaceKey = WorkspaceConnections.FirstOrDefault(item =>
                !item.IsDefault &&
                string.Equals(item.WorkspaceKey, preferredSelection.WorkspaceKey, StringComparison.OrdinalIgnoreCase));
            if (byWorkspaceKey != null)
                return byWorkspaceKey;
        }

        return FindWorkspaceConnectionOptionByBaseUrl(preferredSelection.BaseUrl);
    }

    private void RestoreWorkspaceSelection(WorkspaceConnectionOption? preferredSelection, string baseUrl)
    {
        var match = FindWorkspaceConnectionOption(preferredSelection)
            ?? FindWorkspaceConnectionOptionByBaseUrl(baseUrl);

        match ??= WorkspaceConnections.FirstOrDefault();
        if (match == null)
            return;

        _suppressWorkspaceSelectionChanged = true;
        try
        {
            SelectedWorkspaceConnection = match;
        }
        finally
        {
            _suppressWorkspaceSelectionChanged = false;
        }

        _ = RefreshSelectedWorkspaceHealthAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        UpdateFilteredSearchEntriesInternal();
    }

    partial void OnRequestIdFilterChanged(string value) => UpdateFilteredSearchEntriesInternal();
    partial void OnDisplayFilterChanged(string value) => UpdateFilteredSearchEntriesInternal();
    partial void OnModelFilterChanged(string value) => UpdateFilteredSearchEntriesInternal();
    partial void OnTimestampFilterChanged(string value) => UpdateFilteredSearchEntriesInternal();
    partial void OnAgentFilterChanged(string value) => UpdateFilteredSearchEntriesInternal();

    partial void OnSelectedSearchEntryChanged(SearchableEntry? value)
    {
        if (value == null) return;
        var node = FindJsonNodeBySourcePath(JsonTree, value.SourcePath);
        if (node != null)
        {
            SelectedJsonNode = node;
            ExpandToJsonNode(JsonTree, node);
        }
    }

    internal void UpdateFilteredSearchEntriesInternal()
    {
        var q = (SearchQuery ?? "").Trim();
        var rid = (RequestIdFilter ?? "").Trim().ToLowerInvariant();
        var disp = (DisplayFilter ?? "").Trim().ToLowerInvariant();
        var mod = (ModelFilter ?? "").Trim().ToLowerInvariant();
        var ts = (TimestampFilter ?? "").Trim().ToLowerInvariant();

        IEnumerable<SearchableEntry> filtered = SearchableEntries;

        if (!string.IsNullOrEmpty(q))
        {
            var matcher = Services.BooleanSearchParser.Parse(q);
            filtered = filtered.Where(e => matcher(e.SearchText ?? "") ||
                                          matcher(e.RequestId ?? "") ||
                                          matcher(e.DisplayText ?? "") ||
                                          matcher(e.Model ?? "") ||
                                          matcher(e.Agent ?? "") ||
                                          matcher(e.Timestamp ?? ""));
        }
        if (!string.IsNullOrEmpty(rid))
            filtered = filtered.Where(e => string.Equals(e.RequestId ?? "", rid, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(disp))
            filtered = filtered.Where(e => string.Equals(e.DisplayText ?? "", disp, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(mod))
            filtered = filtered.Where(e => string.Equals(e.Model ?? "", mod, StringComparison.OrdinalIgnoreCase));
        var agent = (AgentFilter ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(agent))
            filtered = filtered.Where(e => string.Equals(e.Agent ?? "", agent, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(ts))
            filtered = filtered.Where(e => string.Equals(e.TimestampDisplay ?? "", ts, StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(e.Timestamp ?? "", ts, StringComparison.OrdinalIgnoreCase));

        var sorted = filtered.OrderByDescending(e => e.SortableTimestamp ?? DateTime.MinValue).ToList();
        FilteredSearchEntries = new ObservableCollection<SearchableEntry>(sorted);

        if (IsRequestDetailsVisible)
        {
            // Re-bind SelectedUnifiedRequest to the matching entry in the new list so
            // the details view shows fresh data and nav buttons work after refresh.
            int idx = GetCurrentRequestIndexInFilteredList();
            if (idx >= 0)
                SelectedUnifiedRequest = FilteredSearchEntries[idx].UnifiedEntry;
            NavigateToPreviousRequestCommand.NotifyCanExecuteChanged();
            NavigateToNextRequestCommand.NotifyCanExecuteChanged();
        }

        NotifyContextConsumer();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private void NavigateBack() => _mediator.SendAsync(new Commands.NavigateBackCommand(this));

    public void NavigateBackInternal()
    {
        if (_backStack.Count > 0 && SelectedNode != null)
        {
            _isNavigatingHistory = true;
            _forwardStack.Push(SelectedNode);
            SelectedNode = _backStack.Pop();
            _isNavigatingHistory = false;

            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanNavigateBack() => _backStack.Count > 0;

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private void NavigateForward() => _mediator.SendAsync(new Commands.NavigateForwardCommand(this));

    public void NavigateForwardInternal()
    {
        if (_forwardStack.Count > 0 && SelectedNode != null)
        {
            _isNavigatingHistory = true;
            _backStack.Push(SelectedNode);
            SelectedNode = _forwardStack.Pop();
            _isNavigatingHistory = false;

            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanNavigateForward() => _forwardStack.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        _isRefreshing = true;
        RefreshCommand.NotifyCanExecuteChanged();
        try
        {
            await _mediator.SendAsync(new Commands.RefreshViewCommand(this));
        }
        finally
        {
            _isRefreshing = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRefresh() => !_isRefreshing;

    public async Task RefreshInternalAsync()
    {
        if (SelectedNode != null)
        {
            if (SelectedNode.Path == "ALL_JSON_VIRTUAL_NODE" ||
                IsMcpSessionNode(SelectedNode) ||
                SelectedNode.Path.StartsWith("MCP_", StringComparison.OrdinalIgnoreCase))
            {
                await ReloadFromMcpAsyncInternal();
                return;
            }

            // Force regenerate
             string hash = SelectedNode.Path.GetHashCode().ToString("X");
             string tempFileName = $"{Path.GetFileNameWithoutExtension(SelectedNode.Path)}_{hash}.html";
             string tempDir = GetHtmlCacheDir();
             string tempPath = Path.Combine(tempDir, tempFileName);

             if (File.Exists(tempPath)) File.Delete(tempPath);

             GenerateAndNavigateInternal(SelectedNode);
        }
    }

    partial void OnSelectedUnifiedRequestChanged(UnifiedRequestEntry? value)
    {
        LogUnifiedEntryActionsForGrid(value);
    }

    /// <summary>
    /// Detailed logging when the unified entry (and its Actions) are bound by the details grid.
    /// </summary>
    private static void LogUnifiedEntryActionsForGrid(UnifiedRequestEntry? entry)
    {
        const string prefix = "[Unified Grid Actions]";
        if (entry == null)
        {
            _logger.LogDebug($"{prefix} Bound entry is null; grid will show no actions.");
            return;
        }
        string requestId = entry.RequestId ?? "(null)";
        _logger.LogDebug($"{prefix} Binding entry for details grid RequestId={requestId} HasActions={entry.HasActions}");
        var actions = entry.Actions;
        if (actions == null || actions.Count == 0)
        {
            _logger.LogDebug($"{prefix}   Actions count=0 (grid will be empty/hidden).");
            return;
        }
        _logger.LogDebug($"{prefix}   Actions count={actions.Count}");
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            string desc = string.IsNullOrEmpty(a.Description) ? "" : (a.Description.Length <= 80 ? a.Description : a.Description.Substring(0, 77) + "...");
            _logger.LogDebug($"{prefix}   [{i + 1}] Order={a.Order} Type={a.Type} Status={a.Status} FilePath={a.FilePath} Description={desc}");
        }
    }

    [RelayCommand]
    private void ShowRequestDetails(SearchableEntry entry) => _mediator.SendAsync(new Commands.ShowRequestDetailsCommand(this, entry));

    public void ShowRequestDetailsInternal(SearchableEntry entry)
    {
        if (entry != null && entry.UnifiedEntry != null)
        {
            SelectedUnifiedRequest = entry.UnifiedEntry;
            IsMarkdownVisible = false;
            IsJsonVisible = false;
            IsRequestDetailsVisible = true;
            ArchiveCommand.NotifyCanExecuteChanged();
            NavigateToPreviousRequestCommand.NotifyCanExecuteChanged();
            NavigateToNextRequestCommand.NotifyCanExecuteChanged();
            NotifyContextConsumer();
        }
    }

    /// <summary>Navigates to the request details view for the currently selected search entry (e.g. on double-click).</summary>
    public void TryNavigateToSelectedSearchEntry()
    {
        if (SelectedSearchEntry is { } e && e.UnifiedEntry != null)
            ShowRequestDetails(e);
    }

    /// <summary>Called when the chat window is open; we push current context so the agent stays in sync with navigation.</summary>
    private Action<string>? _contextConsumer;

    /// <summary>Raised when the VM wants the platform to open the chat window (desktop-only).</summary>
    public event EventHandler? OpenChatWindowRequested;

    [RelayCommand]
    private void OpenChatWindow() => OpenChatWindowRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Register or clear the context consumer (chat window). When set, we call it when the user navigates to JSON or details.</summary>
    public void SetContextConsumer(Action<string>? consumer) => _contextConsumer = consumer;

    /// <summary>Called when the chat window is open; we set the AI model based on tree selection (e.g. codellama for source files, llama3 otherwise).</summary>
    private Action<string>? _modelConsumer;

    /// <summary>Register or clear the model consumer (chat window). When set, we call it with the desired model name when the selected node changes.</summary>
    public void SetModelConsumer(Action<string>? consumer) => _modelConsumer = consumer;

    /// <summary>Applies the model for the currently selected node (codellama for source files, llama3 for other files). Directory nodes are ignored. Call when the chat window is opened so the model matches the current selection.</summary>
    public void ApplyModelForCurrentSelection()
    {
        if (SelectedNode == null || SelectedNode.IsDirectory) return;
        bool isSourceFile = !string.IsNullOrEmpty(Path.GetExtension(SelectedNode.Path)) && IsCodeFile(Path.GetExtension(SelectedNode.Path));
        NotifyModelConsumer(isSourceFile ? "codellama:latest" : "llama3:latest");
    }

    private void NotifyModelConsumer(string model)
    {
        if (_modelConsumer == null) return;
        try
        {
            _modelConsumer(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyModelConsumer");
        }
    }

    private void NotifyContextConsumer()
    {
        if (_contextConsumer == null) return;
        try
        {
            var ctx = GetLogContextForAgent();
            _contextConsumer(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyContextConsumer");
        }
    }

    /// <summary>Builds a short summary of the current log view for the AI assistant (filtered entries and selected request). Includes agent config, docs folder, src folder, and current log view.</summary>
    public string GetLogContextForAgent()
    {
        var entries = FilteredSearchEntries ?? SearchableEntries;
        var sb = new StringBuilder();
        string? resolvedPath = null;
        try { resolvedPath = GetResolvedTargetPath(); } catch (Exception ex) { _logger.LogDebug(ex, "[Path] GetResolvedTargetPath failed"); }

        var agentConfig = AgentConfigIo.ReadContent();
        if (!string.IsNullOrWhiteSpace(agentConfig))
        {
            sb.AppendLine("--- Agent instructions (from agent_config.md) ---");
            sb.AppendLine(agentConfig.Trim());
            sb.AppendLine();
        }

        if (resolvedPath != null)
        {
            AppendDocsContext(sb, resolvedPath);
            AppendSourceContext(sb, resolvedPath);
        }

        // Navigation context: what the user is currently viewing
        if (IsRequestDetailsVisible)
            sb.AppendLine("Navigation: Request details view (one request selected).");
        else if (IsJsonVisible && SelectedNode != null)
            sb.AppendLine(SelectedNode.Path == "ALL_JSON_VIRTUAL_NODE"
                ? "Navigation: All JSON (aggregated log from MCP sessions)."
                : $"Navigation: JSON file: {SelectedNode.Path}");
        else if (IsMarkdownVisible && !string.IsNullOrEmpty(_currentMarkdownPath))
            sb.AppendLine($"Navigation: Markdown: {_currentMarkdownPath}");
        else
            sb.AppendLine("Navigation: (list or loading)");
        sb.AppendLine();

        if (entries == null || entries.Count == 0)
        {
            sb.AppendLine("(No log loaded or no entries in current view.)");
            return sb.ToString();
        }
        sb.AppendLine($"Current view: {entries.Count} request(s).");
        sb.AppendLine("Columns: RequestId | DisplayText | Model | Agent | Timestamp");
        int take = Math.Min(50, entries.Count);
        for (int i = 0; i < take; i++)
        {
            var e = entries[i];
            sb.AppendLine($"  {e.RequestId} | {TruncateForContext(e.DisplayText, 60)} | {e.Model} | {e.Agent} | {e.TimestampDisplay}");
        }
        if (entries.Count > take)
            sb.AppendLine($"  ... and {entries.Count - take} more.");
        if (SelectedUnifiedRequest != null)
        {
            sb.AppendLine();
            sb.AppendLine("Selected request:");
            var r = SelectedUnifiedRequest;
            sb.AppendLine($"  RequestId: {r.RequestId}; Model: {r.Model}; Agent: {r.Agent}; Status: {r.Status}");
            if (!string.IsNullOrWhiteSpace(r.QueryTitle)) sb.AppendLine($"  Title: {r.QueryTitle}");
            if (!string.IsNullOrWhiteSpace(r.QueryText)) sb.AppendLine($"  Query: {TruncateForContext(r.QueryText, 200)}");
        }
        return sb.ToString();
    }

    private const int MaxDocsContextChars = 12_000;
    private const int MaxSourceContextChars = 16_000;
    private const int MaxSourceFileLines = 400;

    private static void AppendDocsContext(StringBuilder sb, string resolvedPath)
    {
        string? docsPath = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrEmpty(docsPath) || !Directory.Exists(docsPath))
            return;
        try
        {
            var files = new DirectoryInfo(docsPath).GetFiles("*.md")
                .Where(f => !IsArchivedName(f.Name))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0)
                return;
            sb.AppendLine("--- Documents (from docs folder) ---");
            int remaining = MaxDocsContextChars;
            foreach (var file in files)
            {
                if (remaining <= 0) break;
                string content;
                try
                {
                    content = File.ReadAllText(file.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CodeFiles] Could not read {FileName}", file.Name);
                    content = $"(could not read {file.Name})";
                }
                string block = $"\n### {file.Name}\n{content}\n";
                if (block.Length > remaining)
                    block = block.AsSpan(0, remaining).ToString() + "\n...(truncated)";
                sb.Append(block);
                remaining -= block.Length;
            }
            if (remaining <= 0)
                sb.AppendLine("\n...(docs truncated)");
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(Docs folder unavailable: {ex.Message})");
            sb.AppendLine();
        }
    }

    private static void AppendSourceContext(StringBuilder sb, string resolvedPath)
    {
        string? sourcePath = GetSourcePath(resolvedPath);
        if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            return;
        try
        {
            var filePaths = new List<string>();
            CollectCodeFiles(sourcePath, filePaths, resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (filePaths.Count == 0)
                return;
            sb.AppendLine("--- Source code (from src folder) ---");
            int remaining = MaxSourceContextChars;
            foreach (var path in filePaths)
            {
                if (remaining <= 0) break;
                string fileName = Path.GetFileName(path);
                string content;
                try
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length > MaxSourceFileLines)
                        lines = lines.Take(MaxSourceFileLines).ToArray();
                    content = string.Join(Environment.NewLine, lines);
                    if (content.Length > 2000)
                        content = content.AsSpan(0, 2000).ToString() + "\n... (truncated)";
                }
                catch
                {
                    content = $"(could not read {fileName})";
                }
                string block = $"\n### {fileName}\n```\n{content}\n```\n";
                if (block.Length > remaining)
                    block = block.AsSpan(0, remaining).ToString() + "\n...(truncated)";
                sb.Append(block);
                remaining -= block.Length;
            }
            if (remaining <= 0)
                sb.AppendLine("\n...(source truncated)");
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(Source folder unavailable: {ex.Message})");
            sb.AppendLine();
        }
    }

    private static void CollectCodeFiles(string dirPath, List<string> filePaths, string sessionsPathToIgnore)
    {
        try
        {
            var dirInfo = new DirectoryInfo(dirPath);
            foreach (var dir in dirInfo.GetDirectories())
            {
                if (SourceDirectoriesToSkip.Contains(dir.Name))
                    continue;
                var childFull = dir.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(childFull, sessionsPathToIgnore, StringComparison.OrdinalIgnoreCase))
                    continue;
                CollectCodeFiles(dir.FullName, filePaths, sessionsPathToIgnore);
            }
            foreach (var file in dirInfo.GetFiles())
            {
                if (IsCodeFile(file.Extension))
                    filePaths.Add(file.FullName);
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "[CodeFiles] Error traversing directory"); }
    }

    private static string TruncateForContext(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= maxLen ? s : s.AsSpan(0, maxLen).ToString() + "...";
    }

    /// <summary>If the given path matches a request/entry (e.g. "entries[0]", "requests[1]"), navigates to that request's detail view. Returns true if navigation occurred.</summary>
    public bool TryNavigateToDetailsForSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return false;
        var entries = FilteredSearchEntries ?? SearchableEntries;
        if (entries == null) return false;
        foreach (var entry in entries)
        {
            if (string.Equals(entry.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) && entry.UnifiedEntry != null)
            {
                SelectedSearchEntry = entry;
                ShowRequestDetails(entry);
                return true;
            }
        }
        return false;
    }

    [RelayCommand]
    private void SelectSearchEntry(SearchableEntry entry) => _mediator.SendAsync(new Commands.SelectSearchEntryCommand(this, entry));

    public void SelectSearchEntryInternal(SearchableEntry entry)
    {
        if (entry != null)
            SelectedSearchEntry = entry;
    }

    [RelayCommand]
    private void CloseRequestDetails() => _mediator.SendAsync(new Commands.CloseRequestDetailsCommand(this));

    public void CloseRequestDetailsInternal()
    {
        IsRequestDetailsVisible = false;
        IsJsonVisible = true;
        IsMarkdownVisible = false;
        ArchiveCommand.NotifyCanExecuteChanged();
        NotifyContextConsumer();
    }

    private int GetCurrentRequestIndexInFilteredList()
    {
        if (SelectedUnifiedRequest == null || FilteredSearchEntries == null || FilteredSearchEntries.Count == 0)
            return -1;
        for (int i = 0; i < FilteredSearchEntries.Count; i++)
        {
            var entry = FilteredSearchEntries[i].UnifiedEntry;
            if (entry == SelectedUnifiedRequest)
                return i;
            if (entry != null && !string.IsNullOrEmpty(entry.RequestId) &&
                string.Equals(entry.RequestId, SelectedUnifiedRequest.RequestId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private bool CanNavigateToPreviousRequest()
    {
        return GetCurrentRequestIndexInFilteredList() > 0;
    }

    private bool CanNavigateToNextRequest()
    {
        int i = GetCurrentRequestIndexInFilteredList();
        return i >= 0 && i < FilteredSearchEntries.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanNavigateToPreviousRequest))]
    private void NavigateToPreviousRequest() => _mediator.SendAsync(new Commands.NavigateToPreviousRequestCommand(this));

    public void NavigateToPreviousRequestInternal()
    {
        int i = GetCurrentRequestIndexInFilteredList();
        if (i <= 0) return;
        var entry = FilteredSearchEntries[i - 1];
        if (entry?.UnifiedEntry == null) return;
        SelectedSearchEntry = entry;
        ShowRequestDetails(entry);
        NavigateToPreviousRequestCommand.NotifyCanExecuteChanged();
        NavigateToNextRequestCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateToNextRequest))]
    private void NavigateToNextRequest() => _mediator.SendAsync(new Commands.NavigateToNextRequestCommand(this));

    public void NavigateToNextRequestInternal()
    {
        int i = GetCurrentRequestIndexInFilteredList();
        if (i < 0 || i >= FilteredSearchEntries.Count - 1) return;
        var entry = FilteredSearchEntries[i + 1];
        if (entry?.UnifiedEntry == null) return;
        SelectedSearchEntry = entry;
        ShowRequestDetails(entry);
        NavigateToPreviousRequestCommand.NotifyCanExecuteChanged();
        NavigateToNextRequestCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNodeChanging(FileNode? value)
    {
        if (!_isNavigatingHistory && SelectedNode != null && value != null && SelectedNode != value)
        {
            _backStack.Push(SelectedNode);
            _forwardStack.Clear();

            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedNodeChanged(FileNode? value)
    {
        _logger.LogInformation($"Selected Node Changed: {value?.Path}");

        // Start or stop the auto-refresh timer based on whether an MCP node is selected.
        if (value != null && IsMcpVirtualNode(value))
            StartMcpAutoRefresh();
        else
            StopMcpAutoRefresh();

        // Avoid reloading content when selection was restored to same path (e.g. after tree rebuild / file watcher).
        // MCP nodes always reload so clicks refresh data from the server.
        if (value != null && string.Equals(value.Path, _lastNavigatedPath, StringComparison.OrdinalIgnoreCase)
            && !IsMcpVirtualNode(value))
        {
            ExpandToNode(Nodes, value);
            return;
        }
        GenerateAndNavigateInternal(value);
        if (value != null)
        {
             ExpandToNode(Nodes, value);
        }
    }

    [RelayCommand]
    private async Task CopyText(string text) => await _mediator.SendAsync(new Commands.CopyTextCommand(this, text));

    public async Task CopyTextInternal(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            await _clipboardService.SetTextAsync(text);
            SetStatus($"Copied: {text}");
        }
    }

    [RelayCommand]
    private async Task CopyOriginalJson(UnifiedRequestEntry? entry) => await _mediator.SendAsync(new Commands.CopyOriginalJsonCommand(this, entry));

    public async Task CopyOriginalJsonInternal(UnifiedRequestEntry? entry)
    {
        if (entry?.OriginalEntry == null)
        {
            SetStatus("No original JSON to copy.");
            return;
        }
        try
        {
            var json = JsonSerializer.Serialize(entry.OriginalEntry, new JsonSerializerOptions { WriteIndented = true });
            await _clipboardService.SetTextAsync(json);
            SetStatus("Copied original JSON to clipboard.");
        }
        catch (Exception ex)
        {
            SetStatus($"Copy failed: {ex.Message}");
        }
    }

    public void HandleNavigation(string path)
    {
        // Path might be the resolved path in the temp directory, e.g., C:\Users\...\Temp\McpServerManager_Cache\next.md
        // We need to resolve it relative to the _currentMarkdownPath

        if (_currentMarkdownPath == null) return;

        string fileName = Path.GetFileName(path);
        // Assuming relative links are just filenames or relative paths.
        // If the browser resolved it to the temp dir, we just want the relative part.
        // But extracting the relative part from the temp path is hard if we don't know the temp root.

        // Simple approach: Take the filename and look in the current markdown's directory.
        // Better approach: If the path contains "McpServerManager_Cache", strip it and the prefix.

        // Actually, pandoc generates relative links.
        // If I am at "doc.html" in "temp/", and link is "sub/next.md", browser goes to "temp/sub/next.md".
        // I want "original_dir/sub/next.md".

        string? currentDir = Path.GetDirectoryName(_currentMarkdownPath);
        if (currentDir == null) return;

        // Try to handle navigation relative to the current markdown file's directory
        // The path might be absolute (e.g. temp dir) or relative
        // e.g. C:\Users\...\Temp\McpServerManager_Cache\copilot\session-2026-02-02-073300\session-log.md
        // But we want to map it to the configured sessions root (e.g. <SessionsRootPath>\copilot\session-2026-02-02-073300\session-log.md)

        // If the path contains "McpServerManager_Cache", we should try to extract the relative part
        // But since we flatten the cache (or do we?), wait, let's check GenerateAndNavigate.
        // We generate into tempDir directly: Path.Combine(tempDir, tempFileName);
        // tempFileName is Name_Hash.html.
        // So all HTML files are in the root of McpServerManager_Cache.

        // However, pandoc generated links might be relative.
        // If README.md links to "copilot/session.md", browser tries to go to "McpServerManager_Cache/copilot/session.md"
        // In that case, 'path' will be ".../McpServerManager_Cache/copilot/session.md"

        string tempDir = GetHtmlCacheDir();
        if (path.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
        {
             // It's inside our temp dir structure.
             // Get the relative path from tempDir
             string relativePath = Path.GetRelativePath(tempDir, path);

             // Now combine with the root target path? Or current markdown's directory?
             // Since we don't replicate directory structure in temp (we flatten or hash), this is tricky.
             // But wait, if pandoc generates relative links, and the browser resolves them against the base URL (the temp html file),
             // then "copilot/session.md" becomes "temp/copilot/session.md".

             // If we assume the link was relative to the markdown file, we should combine it with the markdown's directory.

             string targetPath = Path.Combine(currentDir, relativePath);

             // Normalize path
             targetPath = Path.GetFullPath(targetPath);

             if (File.Exists(targetPath))
             {
                 SelectNodeByPath(targetPath);
                 return;
             }
        }

        // Fallback: Try just the filename in current dir
        string targetPathSimple = Path.Combine(currentDir, fileName);
        if (File.Exists(targetPathSimple))
        {
            SelectNodeByPath(targetPathSimple);
        }
        else
        {
            _logger.LogWarning($"Could not resolve navigation: {path} -> {targetPathSimple}");
        }
    }

    private void SelectNodeByPath(string path)
    {
        // Recursive search for the node
        var node = FindNode(Nodes, path);
        if (node != null)
        {
            SelectedNode = node;
            ExpandToNode(Nodes, node);
        }
        else
        {
             _logger.LogWarning($"Node not found in tree for: {path}");
        }
    }

    private void ExpandToNode(ObservableCollection<FileNode> nodes, FileNode target)
    {
        foreach (var node in nodes)
        {
            if (node == target) return;

            if (ContainsNode(node, target))
            {
                node.IsExpanded = true;
                ExpandToNode(node.Children, target);
                return;
            }
        }
    }

    private bool ContainsNode(FileNode parent, FileNode target)
    {
        foreach (var child in parent.Children)
        {
            if (child == target) return true;
            if (ContainsNode(child, target)) return true;
        }
        return false;
    }

    private FileNode? FindNode(ObservableCollection<FileNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (node.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) return node;

            var found = FindNode(node.Children, path);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>After rebuilding the file tree, restore selection by path so we don't force "All JSON" every time (e.g. on watcher refresh).</summary>
    private void RestoreTreeSelection(string? previousPath, FileNode allJsonNode)
    {
        var toSelect = !string.IsNullOrEmpty(previousPath) ? FindNode(Nodes, previousPath) : null;
        SelectedNode = toSelect ?? allJsonNode;
    }

    private bool IsMcpSessionNode(FileNode? node) =>
        node != null &&
        !node.IsDirectory &&
        node.Path.StartsWith(McpSessionPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsMcpAgentNode(FileNode? node) =>
        node != null &&
        node.Path.StartsWith(McpAgentPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns true for ALL_JSON, MCP agent, and MCP session virtual nodes.</summary>
    public static bool IsMcpVirtualNode(FileNode? node) =>
        node != null &&
        (node.Path == "ALL_JSON_VIRTUAL_NODE" || IsMcpAgentNode(node) ||
         node.Path.StartsWith(McpSessionPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Re-navigates the currently selected MCP node, refreshing its data from the server. Called from code-behind when the user re-taps an already-selected node.</summary>
    public void RefreshCurrentMcpNode()
    {
        if (SelectedNode != null && IsMcpVirtualNode(SelectedNode))
            GenerateAndNavigateInternal(SelectedNode);
    }

    /// <summary>Starts an auto-refresh timer for MCP nodes (60s). Stops any existing timer first.</summary>
    private void StartMcpAutoRefresh()
    {
        StopMcpAutoRefresh();
        var interval = TimeSpan.FromMinutes(1);
        _mcpAutoRefreshTimer = new Timer(_ =>
        {
            DispatchToUi(() =>
            {
                if (!_isRefreshing && SelectedNode != null && IsMcpVirtualNode(SelectedNode))
                    _ = RefreshAsync();
            });
        }, null, interval, interval);
    }

    private void StopMcpAutoRefresh()
    {
        _mcpAutoRefreshTimer?.Dispose();
        _mcpAutoRefreshTimer = null;
    }

    private static string GetAgentNameFromVirtualPath(string virtualPath) =>
        virtualPath.StartsWith(McpAgentPrefix, StringComparison.OrdinalIgnoreCase)
            ? virtualPath.Substring(McpAgentPrefix.Length).Trim()
            : virtualPath.Trim();

    internal static string BuildMcpSessionPath(string sourceType, string sessionId) =>
        $"{McpSessionPrefix}{sourceType}/{sessionId}";

    internal static DateTime GetSessionSortTimestamp(UnifiedSessionLog s) =>
        s.LastUpdated ?? s.Started ?? DateTime.MinValue;

    internal static IEnumerable<UnifiedSessionLog> OrderSessionsNewestFirst(IEnumerable<UnifiedSessionLog> sessions) =>
        sessions
            .OrderByDescending(GetSessionSortTimestamp)
            .ThenByDescending(s => s.SessionId ?? "", StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds a path-keyed dictionary of sessions, keeping the newest per path.</summary>
    internal Dictionary<string, UnifiedSessionLog> BuildSessionsByPathDict(IReadOnlyList<UnifiedSessionLog> sessions)
    {
        var byPath = new Dictionary<string, UnifiedSessionLog>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sessions)
        {
            var path = BuildMcpSessionPath(s.SourceType ?? "Unknown", s.SessionId ?? "");
            if (!byPath.TryGetValue(path, out var existing) || GetSessionSortTimestamp(s) >= GetSessionSortTimestamp(existing))
                byPath[path] = s;
        }
        return byPath;
    }

    /// <summary>Deduplicates and orders sessions newest-first.</summary>
    internal List<UnifiedSessionLog> OrderAndDeduplicateSessions(Dictionary<string, UnifiedSessionLog> byPath)
        => OrderSessionsNewestFirst(byPath.Values).ToList();

    /// <summary>Updates the cached MCP session state on the UI thread.</summary>
    internal void SetMcpSessionState(List<UnifiedSessionLog> sessions, Dictionary<string, UnifiedSessionLog> byPath)
    {
        _mcpSessions = sessions;
        _mcpSessionsByPath = byPath;
    }

    internal async Task ReloadFromMcpAsyncInternal()
    {
        var sessions = await McpSessionService.GetAllSessionsAsync(CancellationToken.None).ConfigureAwait(true);
        var byPath = new Dictionary<string, UnifiedSessionLog>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sessions)
        {
            var path = BuildMcpSessionPath(s.SourceType ?? "Unknown", s.SessionId ?? "");
            if (!byPath.TryGetValue(path, out var existing) || GetSessionSortTimestamp(s) >= GetSessionSortTimestamp(existing))
                byPath[path] = s;
        }

        var uniqueSessions = OrderSessionsNewestFirst(byPath.Values).ToList();
        var (allJsonNode, documentsDto, sourceDto) = BuildMcpTreeOffThread(uniqueSessions);
        // Await the UI dispatch so the tracked task stays alive until the tree selection
        // (and any subsequent navigation commands it triggers) has been dispatched.
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _mcpSessions = uniqueSessions;
            _mcpSessionsByPath = byPath;

            var previousPath = SelectedNode?.Path;
            Nodes.Clear();
            Nodes.Add(allJsonNode);
            if (documentsDto != null)
            {
                var documentsNode = ApplyTreeDtoToNodes(documentsDto);
                documentsNode.Name = "Documents";
                Nodes.Add(documentsNode);
            }
            if (sourceDto != null)
            {
                var sourceNode = ApplyTreeDtoToNodes(sourceDto);
                sourceNode.Name = "Source";
                Nodes.Add(sourceNode);
            }

             // Cache sessions so the auto-triggered ALL_JSON load doesn't re-fetch from MCP.
            _cachedSessionsForAutoLoad = uniqueSessions;
            RestoreTreeSelection(previousPath, allJsonNode);
            StatusMessage = $"Loaded {_mcpSessions.Count} session(s) from MCP.";
        });
    }

    private static (FileNode allJsonNode, TreeDto? documentsDto, TreeDto? sourceDto) BuildMcpTreeOffThread(IReadOnlyList<UnifiedSessionLog> sessions)
    {
        var allJsonNode = new FileNode("ALL_JSON_VIRTUAL_NODE", true) { Name = "All JSON", IsExpanded = true };
        var byAgent = sessions
            .GroupBy(s => string.IsNullOrWhiteSpace(s.SourceType) ? "Unknown" : s.SourceType.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Max(GetSessionSortTimestamp))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in byAgent)
        {
            var agentNode = new FileNode($"{McpAgentPrefix}{group.Key}", true) { Name = group.Key };
            foreach (var session in OrderSessionsNewestFirst(group))
            {
                var sessionPath = BuildMcpSessionPath(group.Key, session.SessionId ?? "");
                var title = !string.IsNullOrWhiteSpace(session.Title) ? session.Title!.Trim() : session.SessionId ?? "(session)";
                var item = new FileNode(sessionPath, false)
                {
                    Name = $"{title} ({session.SessionId})"
                };
                agentNode.Children.Add(item);
            }
            allJsonNode.Children.Add(agentNode);
        }

        string? resolvedPath = null;
        try { resolvedPath = GetResolvedTargetPath(); } catch (Exception ex) { _logger.LogDebug(ex, "[Path] GetResolvedTargetPath failed"); }
        var documentsDto = resolvedPath != null ? BuildDocumentsDto(resolvedPath) : null;
        var sourceDto = resolvedPath != null ? BuildSourceDto(resolvedPath) : null;
        return (allJsonNode, documentsDto, sourceDto);
    }

    private static JsonTreeNode? FindJsonNodeBySourcePath(ObservableCollection<JsonTreeNode> nodes, string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return null;
        foreach (var node in nodes)
        {
            if (string.Equals(node.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                return node;
            var found = FindJsonNodeBySourcePath(node.Children, sourcePath);
            if (found != null) return found;
        }
        return null;
    }

    private static void ExpandToJsonNode(ObservableCollection<JsonTreeNode> nodes, JsonTreeNode target)
    {
        foreach (var node in nodes)
        {
            if (node == target) return;
            if (ContainsJsonNode(node, target))
            {
                node.IsExpanded = true;
                ExpandToJsonNode(node.Children, target);
                return;
            }
        }
    }

    private static bool ContainsJsonNode(JsonTreeNode parent, JsonTreeNode target)
    {
        foreach (var child in parent.Children)
        {
            if (child == target) return true;
            if (ContainsJsonNode(child, target)) return true;
        }
        return false;
    }


    /// <summary>Dispatches an action to the UI thread. Use for any property/collection updates from background work.</summary>
    internal void DispatchToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(() => action());
    }

    internal Task DispatchToUiAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>Reads a text file with encoding detection (BOM) and strips BOM so Markdown.Avalonia displays correctly.</summary>
    private static async Task<string> ReadTextFileWithEncodingAsync(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", fullPath);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(text)) return text;
        // Strip Unicode BOM if present (can cause Markdown.Avalonia to show nothing)
        if (text[0] == '\uFEFF')
            text = text.Substring(1);
        return text;
    }

    internal void GenerateAndNavigateInternal(FileNode? node)
    {
         CancelMarkdownPreview();
         IsPreviewOpenedInBrowser = false;
         CurrentPreviewHtmlPath = null;
         CurrentPreviewMarkdownText = "";

         // Reset details view only when navigating to a different node (preserve on refresh)
         bool preserveDetailsView = node != null && node.Path == _lastNavigatedPath && IsRequestDetailsVisible;
         if (!preserveDetailsView)
             IsRequestDetailsVisible = false;

         if (node == null)
         {
             _lastNavigatedPath = null;
             return;
         }
         _lastNavigatedPath = node.Path;

         if (preserveDetailsView)
         {
             NavigateToPreviousRequestCommand.NotifyCanExecuteChanged();
             NavigateToNextRequestCommand.NotifyCanExecuteChanged();
         }

         // Sync AI model with selection: codellama for source files, llama3 for other files; ignore directory nodes.
         if (!node.IsDirectory)
         {
             bool isSourceFile = !string.IsNullOrEmpty(Path.GetExtension(node.Path)) && IsCodeFile(Path.GetExtension(node.Path));
             NotifyModelConsumer(isSourceFile ? "codellama:latest" : "llama3:latest");
         }

         if (node.Path == "ALL_JSON_VIRTUAL_NODE")
         {
             IsMarkdownVisible = false;
             if (!preserveDetailsView) IsJsonVisible = true;
             ArchiveCommand.NotifyCanExecuteChanged();
             var cached = _cachedSessionsForAutoLoad;
             _cachedSessionsForAutoLoad = null;
             _ = _mediator.SendAsync(new Commands.RefreshAndLoadAllJsonCommand(this) { CachedSessions = cached });
             return;
         }

         if (IsMcpAgentNode(node))
         {
             IsMarkdownVisible = false;
             if (!preserveDetailsView) IsJsonVisible = true;
             ArchiveCommand.NotifyCanExecuteChanged();
             var agentName = GetAgentNameFromVirtualPath(node.Path);
             _ = _mediator.SendAsync(new Commands.RefreshAndLoadAgentJsonCommand(this, agentName));
             return;
         }

         if (IsMcpSessionNode(node))
         {
             IsMarkdownVisible = false;
             if (!preserveDetailsView) IsJsonVisible = true;
             ArchiveCommand.NotifyCanExecuteChanged();
             var virtualPath = node.Path;
             _ = _mediator.SendAsync(new Commands.RefreshAndLoadSessionCommand(this, virtualPath));
             return;
         }

         if (node.IsDirectory) return;

         if (node.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
         {
             IsMarkdownVisible = false;
             if (!preserveDetailsView) IsJsonVisible = true;
             ArchiveCommand.NotifyCanExecuteChanged();
             _ = _mediator.SendAsync(new Commands.LoadJsonFileCommand(this, node.Path));
             return;
         }

         if (node.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
         {
             _ = _mediator.SendAsync(new Commands.LoadMarkdownFileCommand(this, node));
             return;
         }

         // Source tree: display code/project files in the markdown previewer (as a code block)
         if (!node.IsDirectory)
         {
             var ext = Path.GetExtension(node.Path);
             if (!string.IsNullOrEmpty(ext) && IsCodeFile(ext))
             {
                 _ = _mediator.SendAsync(new Commands.LoadSourceFileCommand(this, node));
                 return;
             }
         }
    }

    internal void LoadMarkdownFileInternal(FileNode node)
    {
         IsMarkdownVisible = true;
         IsJsonVisible = false;
         ShowMarkdownAsRawText = false;
         _currentMarkdownPath = node.Path;
         CurrentPreviewHtmlPath = null;
         ArchiveCommand.NotifyCanExecuteChanged();
         NotifyContextConsumer();

         string pathForThisPreview = Path.GetFullPath(node.Path);

         _markdownPreviewCts?.Dispose();
         _markdownPreviewCts = new CancellationTokenSource();
         var token = _markdownPreviewCts.Token;

         _ = Task.Run(async () =>
         {
             string markdownText;
             try
             {
                 markdownText = await ReadTextFileWithEncodingAsync(pathForThisPreview);
             }
             catch (Exception ex)
             {
                 markdownText = "";
                 DispatchToUi(() =>
                 {
                     if (_currentMarkdownPath != node.Path) return;
                     CurrentPreviewMarkdownText = "Could not read file: " + ex.Message;
                     IsPreviewOpenedInBrowser = true;
                 });
                 return;
             }
             if (token.IsCancellationRequested) return;
             DispatchToUi(() =>
             {
                 if (token.IsCancellationRequested || _currentMarkdownPath != node.Path) return;
                 CurrentPreviewMarkdownText = "";
                 IsPreviewOpenedInBrowser = true;
                 StatusMessage = "Markdown loaded.";
             });
             if (token.IsCancellationRequested) return;
             DispatchToUi(() =>
             {
                 if (token.IsCancellationRequested || _currentMarkdownPath != node.Path) return;
                 CurrentPreviewMarkdownText = markdownText ?? "";
             });
         });
    }

    internal void LoadSourceFileInternal(FileNode node)
    {
         IsMarkdownVisible = true;
         IsJsonVisible = false;
         _currentMarkdownPath = node.Path;
         CurrentPreviewHtmlPath = null;
         ArchiveCommand.NotifyCanExecuteChanged();
         NotifyContextConsumer();

         string pathForThisPreview = node.Path;
         _markdownPreviewCts?.Dispose();
         _markdownPreviewCts = new CancellationTokenSource();
         var token = _markdownPreviewCts.Token;

         _ = Task.Run(async () =>
         {
             string content;
             try
             {
                 content = await File.ReadAllTextAsync(pathForThisPreview);
             }
             catch (Exception ex)
             {
                 content = "";
                 DispatchToUi(() =>
                 {
                     if (_currentMarkdownPath != pathForThisPreview) return;
                     CurrentPreviewMarkdownText = "Could not read file: " + ex.Message;
                     IsPreviewOpenedInBrowser = true;
                 });
                 return;
             }
             if (token.IsCancellationRequested) return;
             var lang = GetCodeBlockLanguage(pathForThisPreview);
             var markdownText = $"```{lang}\n{content}\n```";
             DispatchToUi(() =>
             {
                 if (token.IsCancellationRequested || _currentMarkdownPath != pathForThisPreview) return;
                 CurrentPreviewMarkdownText = markdownText;
                 IsPreviewOpenedInBrowser = true;
                 StatusMessage = "Source file loaded.";
             });
         });
    }

    private static string GetCodeBlockLanguage(string filePath)
    {
         var ext = Path.GetExtension(filePath);
         if (string.IsNullOrEmpty(ext)) return "";
         ext = ext.TrimStart('.').ToLowerInvariant();
         if (ext == "csproj" || ext == "vbproj" || ext == "fsproj" || ext == "sln") return "xml";
         return ext;
    }

    /// <summary>Removes duplicate entries by RequestId (case-insensitive). Keeps the first occurrence when ordered by timestamp descending (newest wins). Entries with empty RequestId are not deduplicated.</summary>
    internal static List<UnifiedRequestEntry> DeduplicateUnifiedEntries(List<UnifiedRequestEntry> orderedByNewestFirst)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<UnifiedRequestEntry>();
        foreach (var e in orderedByNewestFirst)
        {
            if (!string.IsNullOrWhiteSpace(e.RequestId))
            {
                if (!seenIds.Add(e.RequestId.Trim()))
                    continue;
            }
            result.Add(e);
        }
        return result;
    }

    /// <summary>Opens an HTML file in the system default browser.</summary>
    private static void OpenHtmlInDefaultBrowser(string htmlFilePath)
    {
        if (!File.Exists(htmlFilePath)) return;
        try
        {
            var path = Path.GetFullPath(htmlFilePath);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = path,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open in browser");
        }
    }

    [RelayCommand]
    private void OpenPreviewInBrowser() => _mediator.SendAsync(new Commands.OpenPreviewInBrowserCommand(this));

    public void OpenPreviewInBrowserInternal()
    {
        if (!string.IsNullOrEmpty(CurrentPreviewHtmlPath))
        {
            OpenHtmlInDefaultBrowser(CurrentPreviewHtmlPath);
            return;
        }
        SetStatus("HTML preview is disabled.");
    }

    [RelayCommand]
    private void ToggleShowRawMarkdown() => _mediator.SendAsync(new Commands.ToggleShowRawMarkdownCommand(this));

    public void ToggleShowRawMarkdownInternal()
    {
        ShowMarkdownAsRawText = !ShowMarkdownAsRawText;
    }

    [RelayCommand]
    private void OpenAgentConfig() => _mediator.SendAsync(new Commands.OpenAgentConfigCommand(this));

    public void OpenAgentConfigInternal()
    {
        AgentConfigIo.EnsureExists();
        OpenFileInDefaultEditor(AgentConfigIo.GetFilePath(), "config");
    }

    [RelayCommand]
    private void OpenPromptTemplates() => _mediator.SendAsync(new Commands.OpenPromptTemplatesCommand(this));

    public void OpenPromptTemplatesInternal()
    {
        PromptTemplatesIo.EnsureExists();
        OpenFileInDefaultEditor(PromptTemplatesIo.GetFilePath(), "prompts");
    }

    private void OpenFileInDefaultEditor(string path, string label)
    {
        if (!File.Exists(path))
        {
            SetStatus($"Could not open {label}: file not found.");
            return;
        }
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use cmd /c start so the file reliably opens with the default app (Process.Start with
                // the file path can succeed without opening when launched from IDE or certain contexts).
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{fullPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = fullPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            SetStatus($"Opened {label}: {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open {label}: {ex.Message}");
        }
    }

    private bool CanArchive() =>
        _currentMarkdownPath != null &&
        File.Exists(_currentMarkdownPath) &&
        IsMarkdownVisible &&
        !_currentMarkdownPath.StartsWith(McpSessionPrefix, StringComparison.OrdinalIgnoreCase) &&
        !IsArchivedName(Path.GetFileName(_currentMarkdownPath));

    [RelayCommand(CanExecute = nameof(CanArchive))]
    private void Archive() => _mediator.SendAsync(new Commands.ArchiveCurrentCommand(this));

    public void ArchiveInternal()
    {
        string? path = _currentMarkdownPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        string? dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return;
        string name = Path.GetFileName(path);
        string newName = "Archived-" + name;
        string newPath = Path.Combine(dir, newName);
        Task.Run(() =>
        {
            try
            {
                File.Move(path, newPath);
                DispatchToUi(() =>
                {
                    _currentMarkdownPath = null;
                    RebuildFileTree();
                    ArchiveCommand.NotifyCanExecuteChanged();
                    StatusMessage = $"Archived: {newName}";
                });
            }
            catch (Exception ex)
            {
                DispatchToUi(() =>
                {
                    StatusMessage = $"Archive failed: {ex.Message}";
                    ArchiveCommand.NotifyCanExecuteChanged();
                });
            }
        });
    }

    /// <summary>Opens (navigates to) the tree node. Used by tree context menu.</summary>
    [RelayCommand]
    private void OpenTreeItem(FileNode? node) => _mediator.SendAsync(new Commands.OpenTreeItemCommand(this, node));

    public void OpenTreeItemInternal(FileNode? node)
    {
        if (node == null) return;
        SelectedNode = node;
        ExpandToNode(Nodes, node);
    }

    /// <summary>Archives the file represented by the tree node by renaming to archive-&lt;name&gt;. Used by tree context menu. Not available for All JSON or directories.</summary>
    [RelayCommand(CanExecute = nameof(CanArchiveTreeItem))]
    private void ArchiveTreeItem(FileNode? node) => _mediator.SendAsync(new Commands.ArchiveTreeItemCommand(this, node));

    public void ArchiveTreeItemInternal(FileNode? node)
    {
        if (node == null || node.Path == "ALL_JSON_VIRTUAL_NODE" || node.IsDirectory || node.Path.StartsWith(McpSessionPrefix, StringComparison.OrdinalIgnoreCase)) return;
        string path = node.Path;
        if (!File.Exists(path)) return;
        string? dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return;
        string name = Path.GetFileName(path);
        if (IsArchivedName(name)) return;
        string newName = "archive-" + name;
        string newPath = Path.Combine(dir, newName);
        Task.Run(() =>
        {
            try
            {
                File.Move(path, newPath);
                DispatchToUi(() =>
                {
                    if (SelectedNode == node)
                        _currentMarkdownPath = null;
                    RebuildFileTree();
                    StatusMessage = $"Archived: {newName}";
                });
            }
            catch (Exception ex)
            {
                DispatchToUi(() => StatusMessage = $"Archive failed: {ex.Message}");
            }
        });
    }

    private static bool CanArchiveTreeItem(FileNode? node) =>
        node != null &&
        node.Path != "ALL_JSON_VIRTUAL_NODE" &&
        !node.Path.StartsWith(McpSessionPrefix, StringComparison.OrdinalIgnoreCase) &&
        !node.IsDirectory &&
        !IsArchivedName(Path.GetFileName(node.Path));

    /// <summary>Rebuilds the file tree on a background thread and applies on UI; selects All JSON.</summary>
    private void RebuildFileTree()
    {
        _ = ReloadFromMcpAsyncInternal();
    }

    private static string? ResolveCssPath()
    {
        string? configuredPath = AppSettings.ResolveCssFallbackPath();
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath)) return configuredPath;
            _logger.LogWarning($"Configured CSS path not found: {configuredPath}");
        }

        string cssPath = Path.Combine(AppContext.BaseDirectory, "Assets", "styles.css");
        if (File.Exists(cssPath)) return cssPath;
        string sourceCss = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "McpServerManager", "Assets", "styles.css");
        if (File.Exists(sourceCss)) return Path.GetFullPath(sourceCss);
        return null;
    }

    private static bool? _isPandocAvailable;

    private static bool IsPandocAvailable()
    {
        if (_isPandocAvailable.HasValue) return _isPandocAvailable.Value;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pandoc",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(5000);
            _isPandocAvailable = p is { ExitCode: 0 };
        }
        catch
        {
            _isPandocAvailable = false;
        }
        if (!_isPandocAvailable.Value)
            _logger.LogWarning("Pandoc is not installed or not on PATH. Markdown preview will be limited.");
        return _isPandocAvailable.Value;
    }

    private bool ConvertMarkdownToHtml(string srcPath, string destPath)
    {
        try
        {
            if (!IsPandocAvailable()) return false;
            string? cssPath = ResolveCssPath();
            if (cssPath == null) return false;

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "pandoc";
            process.StartInfo.Arguments = $"\"{srcPath}\" -f markdown -t html -s --css \"{cssPath}\" --metadata title=\"Request Tracker\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // Read before WaitForExit to avoid deadlock when pipe buffer fills
            string htmlContent = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogError("Pandoc failed: ExitCode={ExitCode}. stderr: {Stderr}", process.ExitCode, stderr);
                return false;
            }
            File.WriteAllText(destPath, htmlContent);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting markdown to HTML");
            return false;
        }
    }

    private static async Task<bool> ConvertMarkdownToHtmlAsync(string srcPath, string destPath)
    {
        try
        {
            if (!IsPandocAvailable()) return false;
            string? cssPath = ResolveCssPath();
            if (cssPath == null)
            {
                _logger.LogWarning("ConvertMarkdownToHtmlAsync: CSS path not found");
                return false;
            }

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "pandoc";
            process.StartInfo.Arguments = $"\"{srcPath}\" -f markdown -t html -s --css \"{cssPath}\" --metadata title=\"Request Tracker\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // Read output before waiting for exit to avoid deadlock (full pipe blocks process)
            Task<string> outTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            string htmlContent = await outTask;
            string stderr = await errTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("Pandoc failed: ExitCode={ExitCode}. stderr: {Stderr}", process.ExitCode, stderr);
                return false;
            }
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogError("Pandoc produced no output. stderr: {Stderr}", stderr);
                return false;
            }
            await File.WriteAllTextAsync(destPath, htmlContent);
            _logger.LogInformation("Pandoc completed: wrote {Length} chars to {DestPath}", htmlContent.Length, destPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting markdown to HTML");
            return false;
        }
    }

    private void SetFallbackHtmlSource(string message)
    {
        CurrentPreviewMarkdownText = message;
        IsPreviewOpenedInBrowser = true;
    }

    internal void LoadJsonInternal(string path)
    {
        DispatchToUi(() => StatusMessage = "Loading JSON...");

        Task.Run(() =>
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                var jsonNode = JsonNode.Parse(jsonContent);
                string schemaType = "Unknown";
                var summary = new JsonLogSummary();
                UnifiedSessionLog? unifiedLog = null;

                if (jsonNode is JsonObject obj)
                {
                    if (obj.ContainsKey("sessionId") && obj.ContainsKey("statistics"))
                    {
                        schemaType = "Copilot Session Log";
                        var model = JsonSerializer.Deserialize<CopilotSessionLog>(jsonContent, CopilotJsonOptions);
                        if (model != null)
                        {
                            BuildCopilotSummaryAndIndex(model, summary);
                            unifiedLog = UnifiedLogFactory.Create(model);
                        }
                    }
                    else if (HasKeyCI(obj, "entries") && HasKeyCI(obj, "session"))
                    {
                        schemaType = "Cursor Request Log";
                        var model = JsonSerializer.Deserialize<CursorRequestLog>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (model != null)
                        {
                            BuildCursorSummaryAndIndex(model, summary);
                            unifiedLog = UnifiedLogFactory.Create(model);
                            if (unifiedLog != null)
                                FillActionsFromRawJson(jsonContent, unifiedLog);
                        }
                    }
                    else if (IsSingleCursorRequest(obj))
                    {
                        schemaType = "Cursor Request (single)";
                        var singleEntry = JsonSerializer.Deserialize<CursorRequestEntry>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (singleEntry != null)
                        {
                            var syntheticLog = new CursorRequestLog
                            {
                                Session = "Single Request",
                                Description = singleEntry.RequestId ?? "Request",
                                Entries = new List<CursorRequestEntry> { singleEntry }
                            };
                            unifiedLog = UnifiedLogFactory.Create(syntheticLog);
                            if (unifiedLog != null && unifiedLog.Entries.Count > 0 && unifiedLog.Entries[0].Actions.Count == 0)
                                FillActionsFromSingleEntryJson(jsonContent, unifiedLog);
                        }
                    }
                    else if (HasKeyCI(obj, "entries") && HasKeyCI(obj, "sourceType"))
                    {
                        schemaType = "Unified Session Log";
                        var unified = JsonSerializer.Deserialize<UnifiedSessionLog>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (unified?.Entries != null)
                        {
                            UnifiedLogFactory.EnsureOriginalEntriesSet(unified);
                            unifiedLog = unified;
                        }
                    }
                }

                DispatchToUi(() => ApplyLoadedJsonToUi(path, jsonNode, schemaType, summary, unifiedLog));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JSON");
                var msg = ex.Message;
                DispatchToUi(() =>
                {
                    JsonTree.Clear();
                    SearchableEntries.Clear();
                    FilteredSearchEntries.Clear();
                    UpdateDistinctFilterValues();
                    JsonLogSummary = new JsonLogSummary();
                    JsonTree.Add(new JsonTreeNode("Error", msg, "Error"));
                    StatusMessage = $"Error loading JSON: {msg}";
                });
            }
        });
    }

    private void ApplyLoadedJsonToUi(string path, JsonNode? jsonNode, string schemaType, JsonLogSummary summary, UnifiedSessionLog? unifiedLog)
    {
        JsonTree.Clear();
        SearchableEntries.Clear();
        UpdateDistinctFilterValues();
        JsonLogSummary = new JsonLogSummary();

        if (unifiedLog != null)
        {
            schemaType = $"{unifiedLog.SourceType} (Unified)";
            BuildUnifiedSummaryAndIndexInternal(unifiedLog, summary);
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
            var unifiedNode = JsonSerializer.SerializeToNode(unifiedLog, options);
            summary.SummaryLines.Clear();
            summary.SummaryLines.Add($"Type: {unifiedLog.SourceType}");
            summary.SummaryLines.Add($"Session: {unifiedLog.SessionId}");
            summary.SummaryLines.Add($"Entries: {unifiedLog.EntryCount}");
            if (!string.IsNullOrEmpty(unifiedLog.Model))
                summary.SummaryLines.Add($"Model: {unifiedLog.Model}");
            if (unifiedLog.LastUpdated.HasValue)
                summary.SummaryLines.Add($"Last Updated: {unifiedLog.LastUpdated}");
            JsonLogSummary = summary;
            var root = new JsonTreeNode("Root", schemaType, "Object");
            root.IsExpanded = true;
            BuildJsonTreeInternal(unifiedNode, root, null);
            JsonTree.Add(root);
        }
        else
        {
            summary.SchemaType = schemaType;
            summary.SummaryLines.Clear();
            summary.SummaryLines.Add($"Schema: {schemaType}");
            summary.SummaryLines.Add($"Total: {summary.TotalCount}");
            if (!string.IsNullOrEmpty(summary.StatsByModel)) summary.SummaryLines.Add(summary.StatsByModel);
            if (!string.IsNullOrEmpty(summary.StatsBySuccess)) summary.SummaryLines.Add(summary.StatsBySuccess);
            if (!string.IsNullOrEmpty(summary.StatsCostOrTokens)) summary.SummaryLines.Add(summary.StatsCostOrTokens);
            JsonLogSummary = summary;
            var root = new JsonTreeNode("Root", schemaType, "Object");
            root.IsExpanded = true;
            BuildJsonTreeInternal(jsonNode, root, null);
            JsonTree.Add(root);
        }

        UpdateFilteredSearchEntriesInternal();
        StatusMessage = $"Loaded {Path.GetFileName(path)}";
    }

    /// <summary>
    /// Fills Actions on unified entries from raw JSON when the deserialized Cursor entry didn't have them
    /// (e.g. "Actions" vs "actions" or different structure). Ensures req-001-logging-system and others show actions.
    /// </summary>
    private static void FillActionsFromRawJson(string jsonContent, UnifiedSessionLog unifiedLog)
    {
        if (unifiedLog?.Entries == null || unifiedLog.Entries.Count == 0) return;
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            if (!TryGetPropertyCI(root, "entries", out var entriesProp) || entriesProp.ValueKind != JsonValueKind.Array)
                return;
            int i = 0;
            foreach (var entryEl in entriesProp.EnumerateArray())
            {
                if (i >= unifiedLog.Entries.Count) break;
                if (unifiedLog.Entries[i].Actions.Count == 0)
                {
                    var actions = UnifiedLogFactory.ParseActionsFromEntryElement(entryEl);
                    if (actions.Count > 0)
                        unifiedLog.Entries[i].Actions = new ObservableCollection<UnifiedAction>(actions);
                }
                i++;
            }
        }
        catch
        {
            // Ignore; we already have whatever the deserializer gave us
        }
    }

    private static bool TryGetPropertyCI(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (element.TryGetProperty(propertyName, out value)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        return false;
    }

    private static bool HasKeyCI(JsonObject obj, string key)
    {
        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsSingleCursorRequest(JsonObject obj)
    {
        if (!HasKeyCI(obj, "requestId")) return false;
        return HasKeyCI(obj, "exactRequest") || HasKeyCI(obj, "exactRequestNote") ||
               HasKeyCI(obj, "actions");
    }

    /// <summary>Parses a JSON file into a unified log using key-based format detection (matches single-file logic).
    /// Returns (unifiedLog, entryCount) or (null, -1) if not recognized.</summary>
    private static (UnifiedSessionLog? log, int entryCount) TryParseFileToUnifiedLog(string text)
    {
        try
        {
            var node = JsonNode.Parse(text);
            if (node is not JsonObject obj) return (null, -1);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1) Unified (entries + sourceType)
            if (HasKeyCI(obj, "entries") && HasKeyCI(obj, "sourceType"))
            {
                var unifiedLog = JsonSerializer.Deserialize<UnifiedSessionLog>(text, options);
                if (unifiedLog?.Entries != null)
                {
                    UnifiedLogFactory.EnsureOriginalEntriesSet(unifiedLog);
                    return (unifiedLog, unifiedLog.EntryCount);
                }
            }

            // 2) Cursor log (entries + session)
            if (HasKeyCI(obj, "entries") && HasKeyCI(obj, "session"))
            {
                var cursorLog = JsonSerializer.Deserialize<CursorRequestLog>(text, options);
                if (cursorLog?.Entries != null)
                {
                    var unified = UnifiedLogFactory.Create(cursorLog);
                    if (unified != null) return (unified, unified.EntryCount);
                }
            }

            // 3) Copilot (sessionId + statistics) — require non-empty Requests so Cursor files aren't mis-detected
            if (obj.ContainsKey("sessionId") && obj.ContainsKey("statistics"))
            {
                var copilotLog = JsonSerializer.Deserialize<CopilotSessionLog>(text, CopilotJsonOptions);
                if (copilotLog?.Requests != null && copilotLog.Requests.Count > 0)
                {
                    var unified = UnifiedLogFactory.Create(copilotLog);
                    if (unified != null) return (unified, unified.EntryCount);
                }
            }

            // 4) Single Cursor request
            if (IsSingleCursorRequest(obj))
            {
                var singleEntry = JsonSerializer.Deserialize<CursorRequestEntry>(text, options);
                if (singleEntry != null)
                {
                    var syntheticLog = new CursorRequestLog
                    {
                        Session = "Single Request",
                        Description = singleEntry.RequestId ?? "Request",
                        Entries = new List<CursorRequestEntry> { singleEntry }
                    };
                    var unified = UnifiedLogFactory.Create(syntheticLog);
                    if (unified != null) return (unified, unified.EntryCount);
                }
            }

            // 5) Older non-unified: entries array only (e.g. Cursor without "session")
            if (HasKeyCI(obj, "entries"))
            {
                var cursorLog = JsonSerializer.Deserialize<CursorRequestLog>(text, options);
                if (cursorLog?.Entries != null && cursorLog.Entries.Count > 0)
                {
                    var unified = UnifiedLogFactory.Create(cursorLog);
                    if (unified != null) return (unified, unified.EntryCount);
                }
            }

            return (null, -1);
        }
        catch
        {
            return (null, -1);
        }
    }

    private static void FillActionsFromSingleEntryJson(string jsonContent, UnifiedSessionLog unifiedLog)
    {
        if (unifiedLog?.Entries == null || unifiedLog.Entries.Count == 0) return;
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var actions = UnifiedLogFactory.ParseActionsFromEntryElement(doc.RootElement);
            if (actions.Count > 0)
                unifiedLog.Entries[0].Actions = new ObservableCollection<UnifiedAction>(actions);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "[Actions] Failed to parse actions from JSON entry"); }
    }

    private void BuildCopilotSummaryAndIndex(CopilotSessionLog log, JsonLogSummary summary)
    {
        var requests = log.Requests ?? new List<CopilotRequestEntry>();
        summary.TotalCount = requests.Count;
        summary.SearchIndex = new List<SearchableEntry>();

        var byModel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            var model = r.Model ?? "(unknown)";
            byModel[model] = byModel.GetValueOrDefault(model) + 1;

            var display = (r.Slug ?? r.Title ?? r.UserRequest ?? "").Trim();
            if (display.Length > 60) display = display.Substring(0, 57) + "...";
            var searchText = string.Join(" ", r.RequestId ?? "", r.Slug ?? "", r.Title ?? "", r.UserRequest ?? "", r.Model ?? "", r.Timestamp?.ToString() ?? "");
            summary.SearchIndex.Add(new SearchableEntry
            {
                RequestId = r.RequestId ?? "",
                DisplayText = string.IsNullOrEmpty(display) ? $"Request {i + 1}" : display,
                Timestamp = r.Timestamp?.ToString("o") ?? "",
                Model = r.Model ?? "",
                EntryIndex = i,
                SourcePath = $"requests[{i}]",
                SearchText = searchText
            });
        }
        summary.StatsByModel = "By model: " + string.Join(", ", byModel.Select(kv => $"{kv.Key}: {kv.Value}"));
        if (log.Statistics != null)
        {
            var s = log.Statistics;
            summary.StatsBySuccess = $"Completed: {s.CompletedCount}, In progress: {s.InProgressCount}, Failed: {s.FailedCount}";
            if (s.AverageSuccessScore.HasValue) summary.StatsBySuccess += $", Avg score: {s.AverageSuccessScore:F1}";
            if (s.TotalNetTokens.HasValue) summary.StatsCostOrTokens = $"Total tokens: {s.TotalNetTokens:N0}";
            if (s.TotalNetPremiumRequests.HasValue) summary.StatsCostOrTokens += $", Premium requests: {s.TotalNetPremiumRequests:N0}";
        }
    }

    private void BuildCursorSummaryAndIndex(CursorRequestLog log, JsonLogSummary summary)
    {
        var entries = log.Entries ?? new List<CursorRequestEntry>();
        summary.TotalCount = entries.Count;
        summary.SearchIndex = new List<SearchableEntry>();

        var byModel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var successScores = new List<int>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var model = e.Model ?? "(unknown)";
            byModel[model] = byModel.GetValueOrDefault(model) + 1;
            if (e.Successfulness?.Score.HasValue == true) successScores.Add(e.Successfulness.Score!.Value);

            var display = (e.ExactRequest ?? e.ExactRequestNote ?? "").Trim();
            if (display.Length > 60) display = display.Substring(0, 57) + "...";
            var searchText = string.Join(" ", e.RequestId ?? "", e.ExactRequest ?? "", e.ExactRequestNote ?? "", e.Model ?? "", e.Timestamp ?? "",
                e.Successfulness?.Score?.ToString() ?? "", e.ActionsTaken ?? new List<string>());
            summary.SearchIndex.Add(new SearchableEntry
            {
                RequestId = e.RequestId ?? "",
                DisplayText = string.IsNullOrEmpty(display) ? $"Entry {i + 1}" : display,
                Timestamp = e.Timestamp ?? "",
                Model = e.Model ?? "",
                EntryIndex = i,
                SourcePath = $"entries[{i}]",
                SearchText = searchText
            });
        }
        summary.StatsByModel = "By model: " + string.Join(", ", byModel.Select(kv => $"{kv.Key}: {kv.Value}"));
        if (successScores.Count > 0)
        {
            summary.StatsBySuccess = $"Success scores: min {successScores.Min()}, max {successScores.Max()}, avg {successScores.Average():F1}";
        }
    }

    internal void BuildUnifiedSummaryAndIndex(UnifiedSessionLog log)
    {
         var summary = new JsonLogSummary();
         BuildUnifiedSummaryAndIndexInternal(log, summary);

         // Update Summary Header for Aggregated view
         summary.SummaryLines.Clear();
         summary.SummaryLines.Add($"Type: {log.SourceType}");
         summary.SummaryLines.Add($"Total Entries: {log.EntryCount}");
         summary.SummaryLines.Add($"Total Tokens: {log.TotalTokens:N0}");
         summary.SummaryLines.Add($"Aggregated at: {log.Started}");

         JsonLogSummary = summary;

         // Build Tree
         JsonTree.Clear();
         var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
         var unifiedNode = JsonSerializer.SerializeToNode(log, options);
         var root = new JsonTreeNode("Root", "Aggregated Unified Log", "Object");
         root.IsExpanded = true;
         BuildJsonTreeInternal(unifiedNode, root, null);
         JsonTree.Add(root);

         UpdateFilteredSearchEntriesInternal();
    }

    internal void BuildUnifiedSummaryAndIndexInternal(UnifiedSessionLog log, JsonLogSummary summary)
    {
        summary.SearchIndex = new List<SearchableEntry>();
        var entries = log.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var display = (e.QueryText ?? "").Trim(); // Updated field name
            if (display.Length > 60) display = display.Substring(0, 57) + "...";

            var searchText = string.Join(" ",
                e.RequestId ?? "",
                e.QueryText ?? "",  // Updated field name
                e.QueryTitle ?? "", // Updated field name
                e.Model ?? "",
                e.Agent ?? "",
                e.Timestamp?.ToString() ?? "",
                e.Status ?? "");

            summary.SearchIndex.Add(new SearchableEntry
            {
                RequestId = e.RequestId ?? "",
                DisplayText = string.IsNullOrEmpty(display) ? $"Entry {i + 1}" : display,
                Timestamp = e.Timestamp?.ToString("o") ?? "",
                Model = e.Model ?? "",
                Agent = e.Agent ?? "",
                EntryIndex = i,
                SourcePath = $"entries[{i}]", // Matches Unified Model structure
                SearchText = searchText,
                UnifiedEntry = e
            });
        }
        var sorted = summary.SearchIndex.OrderByDescending(e => e.SortableTimestamp ?? DateTime.MinValue).ToList();
        SearchableEntries = new ObservableCollection<SearchableEntry>(sorted);
        UpdateDistinctFilterValues();
    }

    private void UpdateDistinctFilterValues()
    {
        var entries = SearchableEntries ?? new ObservableCollection<SearchableEntry>();
        var requestIds = new List<string> { "" };
        requestIds.AddRange(entries.Select(e => e.RequestId ?? "").Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        var displayTexts = new List<string> { "" };
        displayTexts.AddRange(entries.Select(e => e.DisplayText ?? "").Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        var models = new List<string> { "" };
        models.AddRange(entries.Select(e => e.Model ?? "").Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        var agents = new List<string> { "" };
        agents.AddRange(entries.Select(e => e.Agent ?? "").Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        var timestamps = new List<string> { "" };
        timestamps.AddRange(entries.Select(e => e.TimestampDisplay ?? "").Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

        DistinctRequestIds = new ObservableCollection<string>(requestIds);
        DistinctDisplayTexts = new ObservableCollection<string>(displayTexts);
        DistinctModels = new ObservableCollection<string>(models);
        DistinctAgents = new ObservableCollection<string>(agents);
        DistinctTimestamps = new ObservableCollection<string>(timestamps);
    }

    internal void BuildJsonTreeInternal(JsonNode? node, JsonTreeNode parent, string? pathPrefix)
    {
        if (node == null)
        {
            parent.Children.Add(new JsonTreeNode("null", "null", "null"));
            return;
        }

        if (node is JsonObject jsonObj)
        {
            foreach (var property in jsonObj)
            {
                string type = property.Value?.GetType().Name ?? "null";
                if (property.Value is JsonArray) type = "Array";
                else if (property.Value is JsonObject) type = "Object";
                else if (property.Value is JsonValue) type = "Value";

                var childKey = property.Key;
                var childPath = string.IsNullOrEmpty(pathPrefix) ? childKey : pathPrefix + "." + childKey;
                var child = new JsonTreeNode(childKey, "", type);
                if (parent.Name == "Root") child.IsExpanded = true;

                BuildJsonTreeInternal(property.Value, child, childPath);

                if (property.Value is JsonValue val)
                {
                    child.Value = val.ToString();
                }

                parent.Children.Add(child);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            IEnumerable<JsonNode?> items = jsonArray;

            // Sort requests/entries by timestamp descending if applicable
            if (parent.Name.Equals("requests", StringComparison.OrdinalIgnoreCase) ||
                parent.Name.Equals("entries", StringComparison.OrdinalIgnoreCase))
            {
                items = jsonArray.OrderByDescending(n => {
                     if (n is JsonObject obj)
                     {
                         if (obj.TryGetPropertyValue("timestamp", out var tsNode) && tsNode is JsonValue tsVal)
                         {
                             if (tsVal.TryGetValue(out DateTime dt)) return dt;
                             if (tsVal.TryGetValue(out string? s) && DateTime.TryParse(s, out var dt2)) return dt2;

                             // Try numeric timestamp (Unix seconds or milliseconds)
                             if (tsVal.TryGetValue(out long tsLong))
                             {
                                 // Assume milliseconds if large, seconds if small
                                 // 2020-01-01 is ~1.5 billion seconds or 1.5 trillion ms
                                 if (tsLong > 1000000000000) return DateTimeOffset.FromUnixTimeMilliseconds(tsLong).UtcDateTime;
                                 return DateTimeOffset.FromUnixTimeSeconds(tsLong).UtcDateTime;
                             }
                             // Try parsing string as long
                             if (tsVal.TryGetValue(out string? sLong) && long.TryParse(sLong, out var tsLong2))
                             {
                                 if (tsLong2 > 1000000000000) return DateTimeOffset.FromUnixTimeMilliseconds(tsLong2).UtcDateTime;
                                 return DateTimeOffset.FromUnixTimeSeconds(tsLong2).UtcDateTime;
                             }
                         }
                     }
                     return DateTime.MinValue;
                }).ToList();
            }

            int index = 0;
            foreach (var item in items)
            {
                var itemPath = pathPrefix + "[" + index + "]";
                var child = (pathPrefix == "requests" || pathPrefix == "entries")
                    ? new JsonTreeNode($"[{index}]", "", "Item", itemPath)
                    : new JsonTreeNode($"[{index}]", "", "Item");

                string timestampPrefix = "";
                if (item is JsonObject objTS && objTS.TryGetPropertyValue("timestamp", out var tsNode))
                {
                    // Format timestamp nicely if possible
                    string tsStr = tsNode?.ToString() ?? "";
                    if (tsNode is JsonValue tsVal)
                    {
                         if (tsVal.TryGetValue(out DateTime dt))
                         {
                             if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
                             else if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
                             tsStr = dt.ToString("MM/dd HH:mm");
                         }
                         else if (tsVal.TryGetValue(out long l))
                         {
                             // Try to format unix time
                             try {
                                 if (l > 1000000000000) tsStr = DateTimeOffset.FromUnixTimeMilliseconds(l).LocalDateTime.ToString("MM/dd HH:mm");
                                 else tsStr = DateTimeOffset.FromUnixTimeSeconds(l).LocalDateTime.ToString("MM/dd HH:mm");
                             } catch (Exception ex) { _logger.LogDebug(ex, "[Timestamp] Failed to parse unix time {Value}", l); }
                         }
                    }
                    timestampPrefix = $"[{tsStr}] ";
                }

                if (item is JsonObject objItem)
                {
                    foreach (var prop in objItem)
                    {
                        if (prop.Value is JsonValue propVal && propVal.TryGetValue(out string? text) && !string.IsNullOrEmpty(text))
                        {
                            if (text.Length > 50) text = text.Substring(0, 47) + "...";
                            child.Value = timestampPrefix + text;
                            break;
                        }
                    }
                }

                // If no text found but we have timestamp, show it
                if (string.IsNullOrEmpty(child.Value) && !string.IsNullOrEmpty(timestampPrefix))
                {
                    child.Value = timestampPrefix.Trim();
                }

                BuildJsonTreeInternal(item, child, itemPath);
                if (item is JsonValue val)
                {
                    child.Value = val.ToString();
                }
                parent.Children.Add(child);
                index++;
            }
        }
    }

    /// <summary>DTO for building the file tree on a background thread (no ObservableCollection).</summary>
    private sealed class TreeDto
    {
        public string Path { get; set; } = "";
        public bool IsDirectory { get; set; }
        public List<TreeDto> Children { get; } = new();
    }

    /// <summary>Builds the file tree on a background thread; returns allJson node, root DTO, Documents DTO, and Source DTO (code files from ../../src).</summary>
    private static (FileNode allJsonNode, TreeDto? rootDto, TreeDto? documentsDto, TreeDto? sourceDto) BuildTreeOffThread(string resolvedPath)
    {
        var allJsonNode = new FileNode("ALL_JSON_VIRTUAL_NODE", false) { Name = "All JSON" };
        TreeDto? rootDto = null;
        if (Directory.Exists(resolvedPath))
        {
            rootDto = new TreeDto { Path = resolvedPath, IsDirectory = true };
            LoadChildrenDto(rootDto);
        }
        TreeDto? documentsDto = BuildDocumentsDto(resolvedPath);
        TreeDto? sourceDto = BuildSourceDto(resolvedPath);
        return (allJsonNode, rootDto, documentsDto, sourceDto);
    }

    /// <summary>Builds a Source tree DTO: ../../src with code files only, recursing into subdirs but ignoring session log folders.</summary>
    private static TreeDto? BuildSourceDto(string resolvedPath)
    {
        string? sourcePath = GetSourcePath(resolvedPath);
        if (string.IsNullOrEmpty(sourcePath))
            return null;
        var sourceDto = new TreeDto { Path = sourcePath, IsDirectory = true };
        try
        {
            LoadSourceChildrenDto(sourceDto, resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error building Source tree");
        }
        return sourceDto;
    }

    /// <summary>Recursively adds code files and subdirs to the node, skipping session log folder(s).</summary>
    private static void LoadSourceChildrenDto(TreeDto node, string sessionsPathToIgnore)
    {
        var dirInfo = new DirectoryInfo(node.Path);
        foreach (var dir in dirInfo.GetDirectories())
        {
            if (SourceDirectoriesToSkip.Contains(dir.Name))
                continue;
            var childFull = dir.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(childFull, sessionsPathToIgnore, StringComparison.OrdinalIgnoreCase))
                continue;
            var child = new TreeDto { Path = dir.FullName, IsDirectory = true };
            LoadSourceChildrenDto(child, sessionsPathToIgnore);
            if (child.Children.Count > 0)
                node.Children.Add(child);
        }
        foreach (var file in dirInfo.GetFiles())
        {
            if (IsCodeFile(file.Extension))
                node.Children.Add(new TreeDto { Path = file.FullName, IsDirectory = false });
        }
    }

    /// <summary>Builds a Documents tree DTO: parent of resolvedPath with .md files, recursing into subfolders (ignoring session log subfolders).</summary>
    private static TreeDto? BuildDocumentsDto(string resolvedPath)
    {
        string? parentPath = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrEmpty(parentPath) || !Directory.Exists(parentPath))
            return null;
        var documentsDto = new TreeDto { Path = parentPath, IsDirectory = true };
        try
        {
            string sessionsPathToIgnore = resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            LoadDocumentsChildrenDto(documentsDto, sessionsPathToIgnore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error building Documents tree");
        }
        return documentsDto;
    }

    private static void LoadDocumentsChildrenDto(TreeDto node, string sessionsPathToIgnore)
    {
        var dirInfo = new DirectoryInfo(node.Path);
        foreach (var dir in dirInfo.GetDirectories())
        {
            var childFull = dir.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(childFull, sessionsPathToIgnore, StringComparison.OrdinalIgnoreCase))
                continue;
            var child = new TreeDto { Path = dir.FullName, IsDirectory = true };
            LoadDocumentsChildrenDto(child, sessionsPathToIgnore);
            if (child.Children.Count > 0)
                node.Children.Add(child);
        }
        foreach (var file in dirInfo.GetFiles("*.md"))
        {
            if (IsArchivedName(file.Name))
                continue;
            node.Children.Add(new TreeDto { Path = file.FullName, IsDirectory = false });
        }
    }

    private static bool IsArchivedName(string name) =>
        name.StartsWith("archived-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("archive-", StringComparison.OrdinalIgnoreCase);

    private static readonly HashSet<string> CodeFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".rs", ".go", ".java", ".kt", ".kts", ".vb", ".fs", ".fsx",
        ".cpp", ".c", ".h", ".hpp", ".m", ".mm", ".swift", ".r", ".rb", ".php", ".scala", ".lua", ".sql", ".sh", ".ps1",
        ".csproj", ".vbproj", ".fsproj", ".sln"
    };

    private static readonly HashSet<string> SourceDirectoriesToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin"
    };

    private static bool IsCodeFile(string extension) =>
        !string.IsNullOrEmpty(extension) && CodeFileExtensions.Contains(extension);

    /// <summary>Resolves ../../src relative to the sessions path.</summary>
    private static string? GetSourcePath(string resolvedPath)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(resolvedPath, "..", "..", "src"));
            return Directory.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private static void LoadChildrenDto(TreeDto node)
    {
        try
        {
            var dirInfo = new DirectoryInfo(node.Path);
            foreach (var dir in dirInfo.GetDirectories())
            {
                var child = new TreeDto { Path = dir.FullName, IsDirectory = true };
                LoadChildrenDto(child);
                node.Children.Add(child);
            }
            foreach (var file in dirInfo.GetFiles())
            {
                if (IsArchivedName(file.Name))
                    continue;
                if (file.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    node.Children.Add(new TreeDto { Path = file.FullName, IsDirectory = false });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error accessing {node.Path}");
        }
    }

    /// <summary>Converts DTO tree to FileNode tree on the UI thread (ObservableCollection safe).</summary>
    private static FileNode ApplyTreeDtoToNodes(TreeDto dto)
    {
        var node = new FileNode(dto.Path, dto.IsDirectory);
        foreach (var childDto in dto.Children)
            node.Children.Add(ApplyTreeDtoToNodes(childDto));
        return node;
    }

    private void InitializeTree()
    {
        Nodes.Clear();
        string? resolvedPath = null;
        try { resolvedPath = GetResolvedTargetPath(); } catch (Exception ex) { _logger.LogDebug(ex, "[Path] GetResolvedTargetPath failed"); }
        var allJsonNode = new FileNode("ALL_JSON_VIRTUAL_NODE", false) { Name = "All JSON" };
        Nodes.Add(allJsonNode);
        if (resolvedPath != null)
        {
            var documentsNode = BuildDocumentsNode(resolvedPath);
            if (documentsNode != null)
            {
                documentsNode.Name = "Documents";
                Nodes.Add(documentsNode);
            }
            var sourceNode = BuildSourceNode(resolvedPath);
            if (sourceNode != null)
            {
                sourceNode.Name = "Source";
                Nodes.Add(sourceNode);
            }
            if (!Directory.Exists(resolvedPath))
            {
                SetStatus($"Directory not found: {resolvedPath}");
                Nodes.Add(new FileNode(resolvedPath, true) { Name = "Directory not found" });
                SelectedNode = allJsonNode;
                return;
            }
            SetStatus($"Loaded: {resolvedPath}");
            var root = new FileNode(resolvedPath, true);
            LoadChildren(root);
            Nodes.Add(root);
        }
        SelectedNode = allJsonNode;
    }

    /// <summary>Builds the Documents FileNode (md files from parent of resolvedPath, recursing into subfolders). Call on UI thread.</summary>
    private static FileNode? BuildDocumentsNode(string resolvedPath)
    {
        string? parentPath = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrEmpty(parentPath) || !Directory.Exists(parentPath))
            return null;
        var documentsNode = new FileNode(parentPath, true);
        try
        {
            string sessionsPathToIgnore = resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            LoadDocumentsChildren(documentsNode, sessionsPathToIgnore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error building Documents node");
        }
        return documentsNode;
    }

    private static void LoadDocumentsChildren(FileNode node, string sessionsPathToIgnore)
    {
        var dirInfo = new DirectoryInfo(node.Path);
        foreach (var dir in dirInfo.GetDirectories())
        {
            var childFull = dir.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(childFull, sessionsPathToIgnore, StringComparison.OrdinalIgnoreCase))
                continue;
            var childNode = new FileNode(dir.FullName, true);
            LoadDocumentsChildren(childNode, sessionsPathToIgnore);
            if (childNode.Children.Count > 0)
                node.Children.Add(childNode);
        }
        foreach (var file in dirInfo.GetFiles("*.md"))
        {
            if (IsArchivedName(file.Name))
                continue;
            node.Children.Add(new FileNode(file.FullName, false));
        }
    }

    /// <summary>Builds the Source FileNode (code files from ../../src, recursing; skipping session log folders). Call on UI thread.</summary>
    private static FileNode? BuildSourceNode(string resolvedPath)
    {
        string? sourcePath = GetSourcePath(resolvedPath);
        if (string.IsNullOrEmpty(sourcePath))
            return null;
        var sourceNode = new FileNode(sourcePath, true);
        try
        {
            LoadSourceChildren(sourceNode, resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error building Source node");
        }
        return sourceNode;
    }

    private static void LoadSourceChildren(FileNode node, string sessionsPathToIgnore)
    {
        var dirInfo = new DirectoryInfo(node.Path);
        foreach (var dir in dirInfo.GetDirectories())
        {
            if (SourceDirectoriesToSkip.Contains(dir.Name))
                continue;
            var childFull = dir.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(childFull, sessionsPathToIgnore, StringComparison.OrdinalIgnoreCase))
                continue;
            var childNode = new FileNode(dir.FullName, true);
            LoadSourceChildren(childNode, sessionsPathToIgnore);
            if (childNode.Children.Count > 0)
                node.Children.Add(childNode);
        }
        foreach (var file in dirInfo.GetFiles())
        {
            if (IsCodeFile(file.Extension))
                node.Children.Add(new FileNode(file.FullName, false));
        }
    }

    private void LoadChildren(FileNode node)
    {
        try
        {
            var dirInfo = new DirectoryInfo(node.Path);

            foreach (var dir in dirInfo.GetDirectories())
            {
                var childNode = new FileNode(dir.FullName, true);
                LoadChildren(childNode);
                node.Children.Add(childNode);
            }

            foreach (var file in dirInfo.GetFiles())
            {
                if (IsArchivedName(file.Name))
                    continue;
                if (file.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    node.Children.Add(new FileNode(file.FullName, false));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error accessing {node.Path}");
        }
    }

    private void SetupWatcher()
    {
        // Session logs now come from the MCP server; no local session-file watcher is needed.
    }

    public void SetStatus(string message)
    {
        StatusMessage = message;
    }

    private void OnTreeChanged(object sender, FileSystemEventArgs e)
    {
        string? resolvedPath = null;
        try { resolvedPath = GetResolvedTargetPath(); } catch (Exception ex) { _logger.LogDebug(ex, "[Path] GetResolvedTargetPath failed"); }
        if (resolvedPath == null) return;
        Task.Run(() =>
        {
            try
            {
                var (allJsonNode, rootDto, documentsDto, sourceDto) = BuildTreeOffThread(resolvedPath);
                DispatchToUi(() =>
                {
                    try
                    {
                        var previousPath = SelectedNode?.Path;
                        Nodes.Clear();
                        Nodes.Add(allJsonNode);
                        if (documentsDto != null)
                        {
                            var documentsNode = ApplyTreeDtoToNodes(documentsDto);
                            documentsNode.Name = "Documents";
                            Nodes.Add(documentsNode);
                        }
                        if (sourceDto != null)
                        {
                            var sourceNode = ApplyTreeDtoToNodes(sourceDto);
                            sourceNode.Name = "Source";
                            Nodes.Add(sourceNode);
                        }
                        if (rootDto != null)
                        {
                            var root = ApplyTreeDtoToNodes(rootDto);
                            Nodes.Add(root);
                        }
                        else
                        {
                            Nodes.Add(new FileNode(resolvedPath, true) { Name = "Directory not found" });
                        }
                        RestoreTreeSelection(previousPath, allJsonNode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OnTreeChanged apply failed");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnTreeChanged build failed");
            }
        });
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // If the changed file is the current one, regenerate
            if (_currentMarkdownPath != null && e.FullPath.Equals(_currentMarkdownPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation($"Detected change in current file: {e.FullPath}");

                // Log the change
                ChangeLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Rebuilt: {Path.GetFileName(e.FullPath)}");

                // Invalidate cache by forcing generation (the date check in GenerateAndNavigate will handle it since file date is newer)
                // But we need to call GenerateAndNavigate with a node.
                var node = new FileNode(e.FullPath, false);
                GenerateAndNavigateInternal(node);
            }
        });
    }
}

public sealed class WorkspaceConnectionOption
{
    public string Key { get; init; } = "";
    public string? WorkspaceKey { get; init; }
    public string? WorkspaceRootPath { get; init; }
    public string? ApiKey { get; init; }
    public string DisplayName { get; init; } = "";
    public string BaseUrl { get; init; } = "";
    public bool IsDefault { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsEnabled { get; init; } = true;

    public static WorkspaceConnectionOption CreateDefault(Uri defaultBaseUri, string? primaryWorkspaceRootPath, string? apiKey = null)
    {
        var normalizedBaseUrl = defaultBaseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var resolvedApiKey = string.IsNullOrWhiteSpace(apiKey)
            ? McpServerRestClientFactory.TryResolveApiKeyForWorkspaceRoot(primaryWorkspaceRootPath, normalizedBaseUrl)
            : apiKey?.Trim();

        return new WorkspaceConnectionOption
        {
            Key = "__DEFAULT__",
            WorkspaceKey = null,
            WorkspaceRootPath = string.IsNullOrWhiteSpace(primaryWorkspaceRootPath) ? null : primaryWorkspaceRootPath.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(resolvedApiKey) ? null : resolvedApiKey,
            DisplayName = $"Default ({defaultBaseUri.Host}:{defaultBaseUri.Port})",
            BaseUrl = normalizedBaseUrl,
            IsDefault = true,
            IsPrimary = false,
            IsEnabled = true
        };
    }

    public static WorkspaceConnectionOption FromWorkspace(Uri defaultBaseUri, McpWorkspaceItem workspace)
    {
        var isPrimary = workspace.IsPrimary ?? false;
        var uriBuilder = new UriBuilder(defaultBaseUri)
        {
            Port = isPrimary ? defaultBaseUri.Port : workspace.WorkspacePort
        };

        var key = string.IsNullOrWhiteSpace(workspace.WorkspacePath)
            ? workspace.WorkspacePort.ToString()
            : workspace.WorkspacePath.Trim();

        var label = string.IsNullOrWhiteSpace(workspace.Name)
            ? key
            : workspace.Name.Trim();
        var isEnabled = workspace.IsEnabled ?? true;
        var flags = new List<string>();
        if (isPrimary)
            flags.Add("Primary");
        if (!isEnabled)
            flags.Add("Disabled");
        var flagsSuffix = flags.Count == 0 ? "" : $" [{string.Join(", ", flags)}]";
        var baseUrl = uriBuilder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var workspaceRootPath = string.IsNullOrWhiteSpace(workspace.WorkspacePath) ? null : workspace.WorkspacePath.Trim();
        var apiKey = McpServerRestClientFactory.TryResolveApiKeyForWorkspaceRoot(workspaceRootPath, baseUrl);

        return new WorkspaceConnectionOption
        {
            Key = key,
            WorkspaceKey = key,
            WorkspaceRootPath = workspaceRootPath,
            ApiKey = apiKey,
            DisplayName = $"{label} ({uriBuilder.Host}:{uriBuilder.Port}){flagsSuffix}",
            BaseUrl = baseUrl,
            IsDefault = false,
            IsPrimary = isPrimary,
            IsEnabled = isEnabled
        };
    }
}
