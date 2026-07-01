using System;
using Microsoft.Extensions.Logging;
using McpServerManager.UI.Core.Services;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;

namespace McpServerManager.Core.ViewModels;

public partial class ConnectionViewModel : McpServerManager.UI.Core.ViewModels.ConnectionViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    public ConnectionViewModel(
        CqrsDispatcher dispatcher,
        ILogger<McpServerManager.UI.Core.ViewModels.ConnectionViewModel>? logger = null,
        IUiDispatcherService? uiDispatcher = null)
        : base(dispatcher, logger, uiDispatcher ?? new AvaloniaUiDispatcherService())
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }
}
