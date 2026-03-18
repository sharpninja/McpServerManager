using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;
using UiCoreTodoDetailViewModel = McpServer.UI.Core.ViewModels.TodoDetailViewModel;
using UiCoreTodoListViewModel = McpServer.UI.Core.ViewModels.TodoListViewModel;
using UiCoreWorkspaceContextViewModel = McpServer.UI.Core.ViewModels.WorkspaceContextViewModel;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Core wrapper that preserves CQRS command bindings while delegating TODO host logic to UI.Core.
/// </summary>
public partial class TodoListViewModel : McpServer.UI.Core.ViewModels.TodoListHostViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    internal TodoListViewModel(IClipboardService clipboardService, McpServer.UI.Core.Services.UiCoreAppRuntime runtime)
        : base(
            clipboardService,
            runtime.GetRequiredService<UiCoreTodoListViewModel>(),
            runtime.GetRequiredService<UiCoreTodoDetailViewModel>(),
            runtime.GetRequiredService<UiCoreWorkspaceContextViewModel>(),
            runtime.Services,
            runtime.GetRequiredService<McpServer.UI.Core.Services.ITimerService>(),
            runtime.GetRequiredService<ILogger<McpServer.UI.Core.ViewModels.TodoListHostViewModel>>())
    {
        _dispatcher = runtime.GetRequiredService<CqrsDispatcher>();
    }
}

