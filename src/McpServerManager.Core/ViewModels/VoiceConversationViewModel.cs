using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;
using CqrsDispatcher = McpServer.Cqrs.Dispatcher;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Core wrapper that preserves CQRS command bindings while delegating voice logic to UI.Core.
/// </summary>
public partial class VoiceConversationViewModel : McpServer.UI.Core.ViewModels.VoiceConversationViewModel
{
    private readonly CqrsDispatcher _dispatcher;

    public VoiceConversationViewModel(
        McpServer.UI.Core.Services.McpVoiceConversationService service,
        CqrsDispatcher? dispatcher = null,
        ILogger<McpServer.UI.Core.ViewModels.VoiceConversationViewModel>? logger = null)
        : base(service, logger)
    {
        _dispatcher = dispatcher ?? LocalCqrsDispatcher.Instance;
    }
}

