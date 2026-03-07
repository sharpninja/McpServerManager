using Microsoft.Extensions.Logging;

namespace McpServerManager.Core.ViewModels;

/// <summary>
/// Core wrapper that exposes host command bindings while delegating voice logic to UI.Core.
/// </summary>
public partial class VoiceConversationViewModel : McpServer.UI.Core.ViewModels.VoiceConversationViewModel
{
    public VoiceConversationViewModel(
        McpServer.UI.Core.Services.McpVoiceConversationService service,
        ILogger<McpServer.UI.Core.ViewModels.VoiceConversationViewModel>? logger = null)
        : base(service, logger)
    {
    }
}

