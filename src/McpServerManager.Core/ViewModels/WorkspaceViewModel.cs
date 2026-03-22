using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using UiCoreWorkspaceDetailViewModel = McpServer.UI.Core.ViewModels.WorkspaceDetailViewModel;
using UiCoreWorkspaceGlobalPromptViewModel = McpServer.UI.Core.ViewModels.WorkspaceGlobalPromptViewModel;
using UiCoreWorkspaceHealthProbeViewModel = McpServer.UI.Core.ViewModels.WorkspaceHealthProbeViewModel;
using UiCoreWorkspaceListViewModel = McpServer.UI.Core.ViewModels.WorkspaceListViewModel;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Core wrapper that preserves CQRS command bindings while delegating workspace logic to UI.Core.
/// </summary>
public partial class WorkspaceViewModel : McpServer.UI.Core.ViewModels.WorkspaceViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    internal WorkspaceViewModel(IClipboardService clipboardService, McpServer.UI.Core.Services.UiCoreHostRuntime runtime)
        : base(
            clipboardService,
            runtime.GetRequiredService<UiCoreWorkspaceListViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceDetailViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceGlobalPromptViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceHealthProbeViewModel>(),
            runtime.GetRequiredService<McpServer.UI.Core.Services.ITimerService>(),
            runtime.GetRequiredService<McpServer.UI.Core.Services.IUiDispatcherService>())
    {
        _dispatcher = runtime.GetRequiredService<CqrsDispatcher>();
    }

    protected override void NotifyCheckSelectedWorkspaceHealthCanExecuteChanged()
    {
        CheckSelectedWorkspaceHealthCommand.NotifyCanExecuteChanged();
    }
}

