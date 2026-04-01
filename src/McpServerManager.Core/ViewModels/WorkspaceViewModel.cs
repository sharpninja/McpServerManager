using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using UiCoreWorkspaceDetailViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceDetailViewModel;
using UiCoreWorkspaceGlobalPromptViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceGlobalPromptViewModel;
using UiCoreWorkspaceHealthProbeViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceHealthProbeViewModel;
using UiCoreWorkspaceListViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceListViewModel;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Core wrapper that preserves CQRS command bindings while delegating workspace logic to UI.Core.
/// </summary>
public partial class WorkspaceViewModel : McpServerManager.UI.Core.ViewModels.WorkspaceViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    internal WorkspaceViewModel(IClipboardService clipboardService, McpServerManager.UI.Core.Services.UiCoreHostRuntime runtime)
        : base(
            clipboardService,
            runtime.GetRequiredService<UiCoreWorkspaceListViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceDetailViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceGlobalPromptViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceHealthProbeViewModel>(),
            runtime.GetRequiredService<McpServerManager.UI.Core.Services.ITimerService>(),
            runtime.GetRequiredService<McpServerManager.UI.Core.Services.IUiDispatcherService>())
    {
        _dispatcher = runtime.GetRequiredService<CqrsDispatcher>();
    }

    protected override void NotifyCheckSelectedWorkspaceHealthCanExecuteChanged()
    {
        CheckSelectedWorkspaceHealthCommand.NotifyCanExecuteChanged();
    }
}

