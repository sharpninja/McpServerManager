using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using UiCoreTodoDetailViewModel = McpServerManager.UI.Core.ViewModels.TodoDetailViewModel;
using UiCoreTodoListViewModel = McpServerManager.UI.Core.ViewModels.TodoListViewModel;
using UiCoreWorkspaceContextViewModel = McpServerManager.UI.Core.ViewModels.WorkspaceContextViewModel;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Core wrapper that preserves CQRS command bindings while delegating TODO host logic to UI.Core.
/// </summary>
public partial class TodoListViewModel : McpServerManager.UI.Core.ViewModels.TodoListHostViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    internal TodoListViewModel(IClipboardService clipboardService, McpServerManager.UI.Core.Services.UiCoreHostRuntime runtime)
        : base(
            clipboardService,
            runtime.GetRequiredService<UiCoreTodoListViewModel>(),
            runtime.GetRequiredService<UiCoreTodoDetailViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceContextViewModel>(),
            runtime.Services,
            runtime.GetRequiredService<McpServerManager.UI.Core.Services.ITimerService>(),
            runtime.GetRequiredService<ILogger<McpServerManager.UI.Core.ViewModels.TodoListHostViewModel>>())
    {
        _dispatcher = runtime.GetRequiredService<CqrsDispatcher>();
    }
}

