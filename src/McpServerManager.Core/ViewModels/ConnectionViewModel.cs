using Microsoft.Extensions.Logging;
using McpServer.UI.Core.Services;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;

namespace McpServerManager.Core.ViewModels;

public partial class ConnectionViewModel : McpServer.UI.Core.ViewModels.ConnectionViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    public ConnectionViewModel()
        : this(new ConnectionAuthServiceAdapter(), uiDispatcher: new AvaloniaUiDispatcherService())
    {
    }

    public ConnectionViewModel(
        IConnectionAuthService connectionAuthService,
        ILogger<McpServer.UI.Core.ViewModels.ConnectionViewModel>? logger = null,
        IUiDispatcherService? uiDispatcher = null,
        CqrsDispatcher? dispatcher = null)
        : base(connectionAuthService, logger, uiDispatcher ?? new AvaloniaUiDispatcherService())
    {
        _dispatcher = dispatcher ?? LocalCqrsDispatcher.Instance;
    }
}
