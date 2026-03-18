using System;
using System.Threading.Tasks;
using McpServer.Cqrs.Mvvm;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using UiCoreAppSettings = McpServer.UI.Core.AppSettings;
using UiCoreMainWindowViewModel = McpServer.UI.Core.ViewModels.MainWindowViewModel;
using UiCoreStatusViewModel = McpServer.UI.Core.ViewModels.StatusViewModel;

namespace McpServerManager.Core.ViewModels;

public partial class MainWindowViewModel : McpServer.UI.Core.ViewModels.MainWindowViewModel, Commands.ICommandTarget
{
    private readonly CqrsDispatcher _dispatcher;

    public static new System.Collections.Generic.List<McpServer.UI.Core.Models.Json.UnifiedSessionTurn> DeduplicateUnifiedTurns(
        System.Collections.Generic.IEnumerable<McpServer.UI.Core.Models.Json.UnifiedSessionTurn> entries)
        => UiCoreMainWindowViewModel.DeduplicateUnifiedTurns(entries);

    private IClipboardService CoreClipboardService => (IClipboardService)ClipboardService;

    public CqrsRelayCommand<bool> CloseRequestDetailsCommand { get; }
    public CqrsRelayCommand<bool> OpenPreviewInBrowserCommand { get; }
    public CqrsRelayCommand<bool> ToggleShowRawMarkdownCommand { get; }
    public CqrsRelayCommand<bool> OpenAgentConfigCommand { get; }
    public CqrsRelayCommand<bool> OpenPromptTemplatesCommand { get; }

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

    public MainWindowViewModel(IClipboardService clipboardService)
        : this(clipboardService, UiCoreAppSettings.ResolveMcpBaseUrl(), mcpApiKey: null, bearerToken: null, systemNotificationService: null)
    {
    }

    public MainWindowViewModel(IClipboardService clipboardService, string mcpBaseUrl)
        : this(clipboardService, mcpBaseUrl, mcpApiKey: null, bearerToken: null, systemNotificationService: null)
    {
    }

    public MainWindowViewModel(IClipboardService clipboardService, string mcpBaseUrl, string? mcpApiKey)
        : this(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken: null, systemNotificationService: null)
    {
    }

    public MainWindowViewModel(
        IClipboardService clipboardService,
        string mcpBaseUrl,
        string? mcpApiKey,
        string? bearerToken,
        ISystemNotificationService? systemNotificationService = null)
        : base(clipboardService, mcpBaseUrl, mcpApiKey, bearerToken, systemNotificationService)
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

    void Commands.INavigationTarget.NavigateBack() => ((McpServer.UI.Core.Commands.INavigationTarget)this).NavigateBack();
    void Commands.INavigationTarget.NavigateForward() => ((McpServer.UI.Core.Commands.INavigationTarget)this).NavigateForward();
    Task Commands.INavigationTarget.RefreshAsync() => ((McpServer.UI.Core.Commands.INavigationTarget)this).RefreshAsync();
    void Commands.INavigationTarget.PhoneNavigateSection(string? sectionKey) => ((McpServer.UI.Core.Commands.INavigationTarget)this).PhoneNavigateSection(sectionKey);
    void Commands.INavigationTarget.GenerateAndNavigate(McpServer.UI.Core.Models.FileNode? node) => ((McpServer.UI.Core.Commands.INavigationTarget)this).GenerateAndNavigate(node);
    void Commands.INavigationTarget.OpenTreeItem(McpServer.UI.Core.Models.FileNode? node) => ((McpServer.UI.Core.Commands.INavigationTarget)this).OpenTreeItem(node);
    void Commands.INavigationTarget.TreeItemTapped(McpServer.UI.Core.Models.FileNode? node) => ((McpServer.UI.Core.Commands.INavigationTarget)this).TreeItemTapped(node);
    void Commands.INavigationTarget.JsonNodeDoubleTapped(McpServer.UI.Core.Models.Json.JsonTreeNode? node) => ((McpServer.UI.Core.Commands.INavigationTarget)this).JsonNodeDoubleTapped(node);

    void Commands.IRequestDetailsTarget.ShowRequestDetails(McpServer.UI.Core.Models.Json.SearchableTurn entry) => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).ShowRequestDetails(entry);
    void Commands.IRequestDetailsTarget.CloseRequestDetails() => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).CloseRequestDetails();
    void Commands.IRequestDetailsTarget.NavigateToPreviousRequest() => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).NavigateToPreviousRequest();
    void Commands.IRequestDetailsTarget.NavigateToNextRequest() => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).NavigateToNextRequest();
    void Commands.IRequestDetailsTarget.SelectSearchTurn(McpServer.UI.Core.Models.Json.SearchableTurn entry) => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).SelectSearchTurn(entry);
    void Commands.IRequestDetailsTarget.SearchRowTapped(McpServer.UI.Core.Models.Json.SearchableTurn? entry) => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).SearchRowTapped(entry);
    void Commands.IRequestDetailsTarget.SearchRowDoubleTapped(McpServer.UI.Core.Models.Json.SearchableTurn? entry) => ((McpServer.UI.Core.Commands.IRequestDetailsTarget)this).SearchRowDoubleTapped(entry);

    void Commands.IPreviewTarget.OpenPreviewInBrowser() => ((McpServer.UI.Core.Commands.IPreviewTarget)this).OpenPreviewInBrowser();
    void Commands.IPreviewTarget.ToggleShowRawMarkdown() => ((McpServer.UI.Core.Commands.IPreviewTarget)this).ToggleShowRawMarkdown();

    void Commands.IArchiveTarget.Archive() => ((McpServer.UI.Core.Commands.IArchiveTarget)this).Archive();
    void Commands.IArchiveTarget.ArchiveTreeItem(McpServer.UI.Core.Models.FileNode? node) => ((McpServer.UI.Core.Commands.IArchiveTarget)this).ArchiveTreeItem(node);

    Task Commands.IClipboardTarget.CopyText(string text) => ((McpServer.UI.Core.Commands.IClipboardTarget)this).CopyText(text);
    Task Commands.IClipboardTarget.CopyOriginalJson(McpServer.UI.Core.Models.Json.UnifiedSessionTurn? entry) => ((McpServer.UI.Core.Commands.IClipboardTarget)this).CopyOriginalJson(entry);

    void Commands.IConfigTarget.OpenAgentConfig() => ((McpServer.UI.Core.Commands.IConfigTarget)this).OpenAgentConfig();
    void Commands.IConfigTarget.OpenPromptTemplates() => ((McpServer.UI.Core.Commands.IConfigTarget)this).OpenPromptTemplates();

    Task Commands.ISessionDataTarget.ReloadFromMcpAsync() => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).ReloadFromMcpAsync();
    void Commands.ISessionDataTarget.BuildUnifiedSummaryAndIndex(McpServer.UI.Core.Models.Json.UnifiedSessionLog session, McpServer.UI.Core.Models.Json.JsonLogSummary summary) => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).BuildUnifiedSummaryAndIndex(session, summary);
    void Commands.ISessionDataTarget.BuildJsonTree(System.Text.Json.Nodes.JsonNode? node, McpServer.UI.Core.Models.Json.JsonTreeNode root, string? pathPrefix) => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).BuildJsonTree(node, root, pathPrefix);
    void Commands.ISessionDataTarget.LoadJson(string filePath) => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).LoadJson(filePath);
    void Commands.ISessionDataTarget.LoadMarkdownFile(McpServer.UI.Core.Models.FileNode node) => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).LoadMarkdownFile(node);
    void Commands.ISessionDataTarget.LoadSourceFile(McpServer.UI.Core.Models.FileNode node) => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).LoadSourceFile(node);
    void Commands.ISessionDataTarget.UpdateFilteredSearchTurns() => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).UpdateFilteredSearchTurns();
    McpServer.UI.Core.Services.McpSessionLogService Commands.ISessionDataTarget.McpSessionService => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).McpSessionService;
    McpServer.UI.Core.Models.Json.JsonLogSummary Commands.ISessionDataTarget.JsonLogSummary
    {
        get => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).JsonLogSummary;
        set => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).JsonLogSummary = value;
    }
    System.Collections.ObjectModel.ObservableCollection<McpServer.UI.Core.Models.Json.JsonTreeNode> Commands.ISessionDataTarget.JsonTree => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).JsonTree;
    string Commands.ISessionDataTarget.AgentFilter
    {
        get => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).AgentFilter;
        set => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).AgentFilter = value;
    }
    System.Collections.Generic.Dictionary<string, McpServer.UI.Core.Models.Json.UnifiedSessionLog> Commands.ISessionDataTarget.BuildSessionsByPathDict(System.Collections.Generic.IReadOnlyList<McpServer.UI.Core.Models.Json.UnifiedSessionLog> sessions)
        => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).BuildSessionsByPathDict(sessions);
    System.Collections.Generic.List<McpServer.UI.Core.Models.Json.UnifiedSessionLog> Commands.ISessionDataTarget.OrderAndDeduplicateSessions(System.Collections.Generic.Dictionary<string, McpServer.UI.Core.Models.Json.UnifiedSessionLog> byPath)
        => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).OrderAndDeduplicateSessions(byPath);
    void Commands.ISessionDataTarget.SetMcpSessionState(System.Collections.Generic.List<McpServer.UI.Core.Models.Json.UnifiedSessionLog> sessions, System.Collections.Generic.Dictionary<string, McpServer.UI.Core.Models.Json.UnifiedSessionLog> byPath)
        => ((McpServer.UI.Core.Commands.ISessionDataTarget)this).SetMcpSessionState(sessions, byPath);

    void Commands.IUiDispatchTarget.TrackBackgroundWork(Task task) => ((McpServer.UI.Core.Commands.IUiDispatchTarget)this).TrackBackgroundWork(task);
    void Commands.IUiDispatchTarget.DispatchToUi(Action action) => ((McpServer.UI.Core.Commands.IUiDispatchTarget)this).DispatchToUi(action);
    string Commands.IUiDispatchTarget.StatusMessage
    {
        get => ((McpServer.UI.Core.Commands.IUiDispatchTarget)this).StatusMessage;
        set => ((McpServer.UI.Core.Commands.IUiDispatchTarget)this).StatusMessage = value;
    }

    Task Commands.ITodoCopilotTarget.CopilotStatusAsync() => CopilotStatusAsync();
    Task Commands.ITodoCopilotTarget.CopilotPlanAsync() => CopilotPlanAsync();
    Task Commands.ITodoCopilotTarget.CopilotImplementAsync() => CopilotImplementAsync();
}
