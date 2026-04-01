using System;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// App wrapper for UI.Core log ViewModel.
/// </summary>
public partial class LogViewModel : McpServerManager.UI.Core.ViewModels.LogViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    public LogViewModel(
        IClipboardService clipboardService,
        CqrsDispatcher dispatcher,
        McpServerManager.UI.Core.Services.IAppLogService? appLogService = null,
        McpServerManager.UI.Core.Services.IUiDispatcherService? uiDispatcherService = null)
        : base(
            clipboardService,
            appLogService ?? AppLogService.Instance,
            uiDispatcherService ?? new AvaloniaUiDispatcherService())
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }
}
