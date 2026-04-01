using System;
using System.Threading.Tasks;
using McpServer.Cqrs.Mvvm;
using McpServerManager.UI.Core.Services;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using CoreClipboardService = McpServerManager.Core.Services.IClipboardService;
using CoreSystemNotificationService = McpServerManager.Core.Services.ISystemNotificationService;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using UiCoreAppSettings = McpServerManager.UI.Core.AppSettings;
using UiCoreMainWindowViewModel = McpServerManager.UI.Core.ViewModels.MainWindowViewModel;
using UiCoreStatusViewModel = McpServerManager.UI.Core.ViewModels.StatusViewModel;

namespace McpServerManager.Core.ViewModels;

public partial class MainWindowViewModel : McpServerManager.UI.Core.ViewModels.MainWindowViewModel, Commands.ICommandTarget
{
    private CqrsDispatcher _dispatcher = null!;

    public static new System.Collections.Generic.List<McpServerManager.UI.Core.Models.Json.UnifiedSessionTurn> DeduplicateUnifiedTurns(
        System.Collections.Generic.IEnumerable<McpServerManager.UI.Core.Models.Json.UnifiedSessionTurn> entries)
        => UiCoreMainWindowViewModel.DeduplicateUnifiedTurns(entries);

    private CoreClipboardService CoreClipboardService => (CoreClipboardService)ClipboardService;

    public CqrsRelayCommand<bool> CloseRequestDetailsCommand { get; private set; } = null!;
    public CqrsRelayCommand<bool> OpenPreviewInBrowserCommand { get; private set; } = null!;
    public CqrsRelayCommand<bool> ToggleShowRawMarkdownCommand { get; private set; } = null!;
    public CqrsRelayCommand<bool> OpenAgentConfigCommand { get; private set; } = null!;
    public CqrsRelayCommand<bool> OpenPromptTemplatesCommand { get; private set; } = null!;

    /// <summary>ViewModel for the Todo tab. Created lazily on first access.</summary>
    public TodoListViewModel TodoViewModel => _todoViewModel ??= CreateTodoViewModel();
    private TodoListViewModel? _todoViewModel;

    private TodoListViewModel CreateTodoViewModel()
    {
        var vm = new TodoListViewModel(CoreClipboardService, UiCoreRuntime);
        vm.ApplyWorkspacePath(McpClient.WorkspacePath);
        vm.GlobalStatusChanged += msg => DispatchToUi(() => StatusMessage = msg);
        WorkspacePathChanged += path => DispatchToUi(() =>
        {
            vm.ApplyWorkspacePath(path);
            _ = vm.RefreshForConnectionChangeAsync();
        });
        return vm;
    }

    /// <summary>ViewModel for the Workspace tab. Created lazily on first access.</summary>
    public WorkspaceViewModel WorkspaceViewModel => _workspaceViewModel ??= CreateWorkspaceViewModel();
    private WorkspaceViewModel? _workspaceViewModel;

    private WorkspaceViewModel CreateWorkspaceViewModel()
    {
        var vm = new WorkspaceViewModel(CoreClipboardService, UiCoreRuntime);
        vm.GlobalStatusChanged += msg => DispatchToUi(() => StatusMessage = msg);
        vm.WorkspaceCatalogChanged += change => _ = RefreshWorkspacePickerAfterCatalogChangeAsync(change);
        WorkspacePathChanged += path => DispatchToUi(() => _ = vm.RefreshForConnectionChangeAsync());
        return vm;
    }

    /// <summary>ViewModel for the Log tab. Created lazily on first access.</summary>
    public LogViewModel LogViewModel => _logViewModel ??= new LogViewModel(CoreClipboardService, _dispatcher);
    private LogViewModel? _logViewModel;

    /// <summary>ViewModel for the Settings tab. Created lazily on first access.</summary>
    public SettingsViewModel SettingsViewModel => _settingsViewModel ??= new SettingsViewModel(_dispatcher);
    private SettingsViewModel? _settingsViewModel;

    /// <summary>Global status sink for exception reporting and app-wide status messages.</summary>
    public UiCoreStatusViewModel StatusViewModel => UiCoreStatusViewModel.Instance;

    /// <summary>ViewModel for the Voice tab. Created lazily on first access.</summary>
    public VoiceConversationViewModel VoiceConversationViewModel => _voiceConversationViewModel ??= CreateVoiceConversationViewModel();
    private VoiceConversationViewModel? _voiceConversationViewModel;

    private VoiceConversationViewModel CreateVoiceConversationViewModel()
    {
        var voiceSettingsService = VoiceChatSettingsService.Instance;
        var savedVoiceSettings = voiceSettingsService.Load();
        var vm = new VoiceConversationViewModel(McpVoiceService)
        {
            Language = savedVoiceSettings.Language,
            ResolveWorkspacePath = ResolveActiveWorkspacePath,
            ResolveWorkspaceReady = ResolveWorkspaceReady
        };
        vm.GlobalStatusChanged += msg => DispatchToUi(() => StatusMessage = msg);
        voiceSettingsService.SettingsChanged += updatedSettings => DispatchToUi(() => vm.Language = updatedSettings.Language);
        WorkspacePathChanged += path => DispatchToUi(() => _ = vm.RefreshForConnectionChangeAsync());
        return vm;
    }

    public MainWindowViewModel(CoreClipboardService clipboardService)
        : this(clipboardService, UiCoreAppSettings.ResolveMcpBaseUrl(), mcpApiKey: null, bearerToken: null, systemNotificationService: null, uiDispatcher: null)
    {
    }

    public MainWindowViewModel(CoreClipboardService clipboardService, McpServerManager.UI.Core.Services.IUiDispatcherService uiDispatcher)
        : this(clipboardService, UiCoreAppSettings.ResolveMcpBaseUrl(), mcpApiKey: null, bearerToken: null, systemNotificationService: null, uiDispatcher)
    {
    }

    public MainWindowViewModel(CoreClipboardService clipboardService, string mcpBaseUrl)
        : this(clipboardService, mcpBaseUrl, mcpApiKey: null, bearerToken: null, systemNotificationService: null, uiDispatcher: null)
    {
    }

    public MainWindowViewModel(CoreClipboardService clipboardService, string mcpBaseUrl, McpServerManager.UI.Core.Services.IUiDispatcherService uiDispatcher)
        : this(clipboardService, mcpBaseUrl, mcpApiKey: null, bearerToken: null, systemNotificationService: null, uiDispatcher)
    {
    }

    public MainWindowViewModel(CoreClipboardService clipboardService, string mcpBaseUrl, string? mcpApiKey)
        : this(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken: null, systemNotificationService: null, uiDispatcher: null)
    {
    }

    public MainWindowViewModel(CoreClipboardService clipboardService, string mcpBaseUrl, string? mcpApiKey, McpServerManager.UI.Core.Services.IUiDispatcherService uiDispatcher)
        : this(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken: null, systemNotificationService: null, uiDispatcher)
    {
    }

    public MainWindowViewModel(
        CoreClipboardService clipboardService,
        MainWindowHostServices hostServices,
        CoreSystemNotificationService? systemNotificationService = null,
        McpServerManager.UI.Core.Services.IUiDispatcherService? uiDispatcher = null)
        : base(clipboardService, hostServices, systemNotificationService, uiDispatcher ?? new AvaloniaUiDispatcherService())
    {
        _dispatcher = CqrsDispatcher;
        CloseRequestDetailsCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.CloseRequestDetailsCommand());
        OpenPreviewInBrowserCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.OpenPreviewInBrowserCommand());
        ToggleShowRawMarkdownCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.ToggleShowRawMarkdownCommand());
        OpenAgentConfigCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.OpenAgentConfigCommand());
        OpenPromptTemplatesCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.OpenPromptTemplatesCommand());
    }

    public MainWindowViewModel(
        CoreClipboardService clipboardService,
        string mcpBaseUrl,
        string? mcpApiKey,
        string? bearerToken,
        CoreSystemNotificationService? systemNotificationService = null,
        McpServerManager.UI.Core.Services.IUiDispatcherService? uiDispatcher = null)
        : base(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken, systemNotificationService, uiDispatcher ?? new AvaloniaUiDispatcherService())
    {
        _dispatcher = CqrsDispatcher;
        CloseRequestDetailsCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.CloseRequestDetailsCommand());
        OpenPreviewInBrowserCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.OpenPreviewInBrowserCommand());
        ToggleShowRawMarkdownCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.ToggleShowRawMarkdownCommand());
        OpenAgentConfigCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.OpenAgentConfigCommand());
        OpenPromptTemplatesCommand = new CqrsRelayCommand<bool>(_dispatcher, () => new Commands.OpenPromptTemplatesCommand());
    }

    protected override void NotifyHistoryNavigationCanExecuteChanged()
    {
        NavigateBackCommand.NotifyCanExecuteChanged();
        NavigateForwardCommand.NotifyCanExecuteChanged();
    }

    protected override void NotifyRequestNavigationCanExecuteChanged()
    {
        NavigateToPreviousRequestCommand.NotifyCanExecuteChanged();
        NavigateToNextRequestCommand.NotifyCanExecuteChanged();
    }

    protected override void NotifyRefreshCanExecuteChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
    }

    protected override void NotifyArchiveCanExecuteChanged()
    {
        ArchiveCommand.NotifyCanExecuteChanged();
    }

    public override Task CopilotStatusAsync() => TodoViewModel.CopilotStatusAsync();
    public override Task CopilotPlanAsync() => TodoViewModel.CopilotPlanAsync();
    public override Task CopilotImplementAsync() => TodoViewModel.CopilotImplementAsync();

    void Commands.INavigationTarget.NavigateBack() => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).NavigateBack();
    void Commands.INavigationTarget.NavigateForward() => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).NavigateForward();
    Task Commands.INavigationTarget.RefreshAsync() => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).RefreshAsync();
    void Commands.INavigationTarget.PhoneNavigateSection(string? sectionKey) => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).PhoneNavigateSection(sectionKey);
    void Commands.INavigationTarget.GenerateAndNavigate(McpServerManager.UI.Core.Models.FileNode? node) => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).GenerateAndNavigate(node);
    void Commands.INavigationTarget.OpenTreeItem(McpServerManager.UI.Core.Models.FileNode? node) => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).OpenTreeItem(node);
    void Commands.INavigationTarget.TreeItemTapped(McpServerManager.UI.Core.Models.FileNode? node) => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).TreeItemTapped(node);
    void Commands.INavigationTarget.JsonNodeDoubleTapped(McpServerManager.UI.Core.Models.Json.JsonTreeNode? node) => ((McpServerManager.UI.Core.Commands.INavigationTarget)this).JsonNodeDoubleTapped(node);

    void Commands.IRequestDetailsTarget.ShowRequestDetails(McpServerManager.UI.Core.Models.Json.SearchableTurn entry) => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).ShowRequestDetails(entry);
    void Commands.IRequestDetailsTarget.CloseRequestDetails() => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).CloseRequestDetails();
    void Commands.IRequestDetailsTarget.NavigateToPreviousRequest() => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).NavigateToPreviousRequest();
    void Commands.IRequestDetailsTarget.NavigateToNextRequest() => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).NavigateToNextRequest();
    void Commands.IRequestDetailsTarget.SelectSearchTurn(McpServerManager.UI.Core.Models.Json.SearchableTurn entry) => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).SelectSearchTurn(entry);
    void Commands.IRequestDetailsTarget.SearchRowTapped(McpServerManager.UI.Core.Models.Json.SearchableTurn? entry) => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).SearchRowTapped(entry);
    void Commands.IRequestDetailsTarget.SearchRowDoubleTapped(McpServerManager.UI.Core.Models.Json.SearchableTurn? entry) => ((McpServerManager.UI.Core.Commands.IRequestDetailsTarget)this).SearchRowDoubleTapped(entry);

    void Commands.IPreviewTarget.OpenPreviewInBrowser() => ((McpServerManager.UI.Core.Commands.IPreviewTarget)this).OpenPreviewInBrowser();
    void Commands.IPreviewTarget.ToggleShowRawMarkdown() => ((McpServerManager.UI.Core.Commands.IPreviewTarget)this).ToggleShowRawMarkdown();

    void Commands.IArchiveTarget.Archive() => ((McpServerManager.UI.Core.Commands.IArchiveTarget)this).Archive();
    void Commands.IArchiveTarget.ArchiveTreeItem(McpServerManager.UI.Core.Models.FileNode? node) => ((McpServerManager.UI.Core.Commands.IArchiveTarget)this).ArchiveTreeItem(node);

    Task Commands.IClipboardTarget.CopyText(string text) => ((McpServerManager.UI.Core.Commands.IClipboardTarget)this).CopyText(text);
    Task Commands.IClipboardTarget.CopyOriginalJson(McpServerManager.UI.Core.Models.Json.UnifiedSessionTurn? entry) => ((McpServerManager.UI.Core.Commands.IClipboardTarget)this).CopyOriginalJson(entry);

    void Commands.IConfigTarget.OpenAgentConfig() => ((McpServerManager.UI.Core.Commands.IConfigTarget)this).OpenAgentConfig();
    void Commands.IConfigTarget.OpenPromptTemplates() => ((McpServerManager.UI.Core.Commands.IConfigTarget)this).OpenPromptTemplates();

    Task Commands.ISessionDataTarget.ReloadFromMcpAsync() => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).ReloadFromMcpAsync();
    void Commands.ISessionDataTarget.BuildUnifiedSummaryAndIndex(McpServerManager.UI.Core.Models.Json.UnifiedSessionLog session, McpServerManager.UI.Core.Models.Json.JsonLogSummary summary) => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).BuildUnifiedSummaryAndIndex(session, summary);
    void Commands.ISessionDataTarget.BuildJsonTree(System.Text.Json.Nodes.JsonNode? node, McpServerManager.UI.Core.Models.Json.JsonTreeNode root, string? pathPrefix) => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).BuildJsonTree(node, root, pathPrefix);
    void Commands.ISessionDataTarget.LoadJson(string filePath) => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).LoadJson(filePath);
    void Commands.ISessionDataTarget.LoadMarkdownFile(McpServerManager.UI.Core.Models.FileNode node) => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).LoadMarkdownFile(node);
    void Commands.ISessionDataTarget.LoadSourceFile(McpServerManager.UI.Core.Models.FileNode node) => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).LoadSourceFile(node);
    void Commands.ISessionDataTarget.UpdateFilteredSearchTurns() => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).UpdateFilteredSearchTurns();
    McpServerManager.UI.Core.Services.McpSessionLogService Commands.ISessionDataTarget.McpSessionService => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).McpSessionService;
    McpServerManager.UI.Core.Models.Json.JsonLogSummary Commands.ISessionDataTarget.JsonLogSummary
    {
        get => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).JsonLogSummary;
        set => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).JsonLogSummary = value;
    }
    System.Collections.ObjectModel.ObservableCollection<McpServerManager.UI.Core.Models.Json.JsonTreeNode> Commands.ISessionDataTarget.JsonTree => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).JsonTree;
    string Commands.ISessionDataTarget.AgentFilter
    {
        get => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).AgentFilter;
        set => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).AgentFilter = value;
    }
    System.Collections.Generic.Dictionary<string, McpServerManager.UI.Core.Models.Json.UnifiedSessionLog> Commands.ISessionDataTarget.BuildSessionsByPathDict(System.Collections.Generic.IReadOnlyList<McpServerManager.UI.Core.Models.Json.UnifiedSessionLog> sessions)
        => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).BuildSessionsByPathDict(sessions);
    System.Collections.Generic.List<McpServerManager.UI.Core.Models.Json.UnifiedSessionLog> Commands.ISessionDataTarget.OrderAndDeduplicateSessions(System.Collections.Generic.Dictionary<string, McpServerManager.UI.Core.Models.Json.UnifiedSessionLog> byPath)
        => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).OrderAndDeduplicateSessions(byPath);
    void Commands.ISessionDataTarget.SetMcpSessionState(System.Collections.Generic.List<McpServerManager.UI.Core.Models.Json.UnifiedSessionLog> sessions, System.Collections.Generic.Dictionary<string, McpServerManager.UI.Core.Models.Json.UnifiedSessionLog> byPath)
        => ((McpServerManager.UI.Core.Commands.ISessionDataTarget)this).SetMcpSessionState(sessions, byPath);

    void Commands.IUiDispatchTarget.TrackBackgroundWork(Task task) => ((McpServerManager.UI.Core.Commands.IUiDispatchTarget)this).TrackBackgroundWork(task);
    void Commands.IUiDispatchTarget.DispatchToUi(Action action) => ((McpServerManager.UI.Core.Commands.IUiDispatchTarget)this).DispatchToUi(action);
    string Commands.IUiDispatchTarget.StatusMessage
    {
        get => ((McpServerManager.UI.Core.Commands.IUiDispatchTarget)this).StatusMessage;
        set => ((McpServerManager.UI.Core.Commands.IUiDispatchTarget)this).StatusMessage = value;
    }

    Task Commands.ITodoCopilotTarget.CopilotStatusAsync() => CopilotStatusAsync();
    Task Commands.ITodoCopilotTarget.CopilotPlanAsync() => CopilotPlanAsync();
    Task Commands.ITodoCopilotTarget.CopilotImplementAsync() => CopilotImplementAsync();
}
