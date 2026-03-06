using System;
using McpServerManager.Services;
using LocalCqrsDispatcher = McpServerManager.Core.Services.LocalCqrsDispatcher;

namespace McpServerManager.ViewModels;

public partial class ChatWindowViewModel : McpServerManager.Core.ViewModels.ChatWindowViewModel
{
    public ChatWindowViewModel(
        ILogAgentService agentService,
        Func<string> getContext,
        string? initialModelFromConfig = null,
        Action<string?>? onModelChanged = null)
        : base(LocalCqrsDispatcher.Instance, getContext, initialModelFromConfig, onModelChanged)
    {
        _ = agentService ?? throw new ArgumentNullException(nameof(agentService));
    }

    public ChatWindowViewModel()
        : this(new OllamaLogAgentService(), () => string.Empty, null, null)
    {
    }
}
