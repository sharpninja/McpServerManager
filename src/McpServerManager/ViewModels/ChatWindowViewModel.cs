using System;
using McpServerManager.Services;
using CoreChatWindowViewModelFactory = McpServerManager.Core.Services.ChatWindowViewModelFactory;
using CoreLogAgentService = McpServerManager.Core.Services.ILogAgentService;

namespace McpServerManager.ViewModels;

public partial class ChatWindowViewModel : McpServerManager.Core.ViewModels.ChatWindowViewModel
{
    public ChatWindowViewModel(
        ILogAgentService agentService,
        Func<string> getContext,
        string? initialModelFromConfig = null,
        Action<string?>? onModelChanged = null)
        : base(CoreChatWindowViewModelFactory.CreateFallbackDispatcher(new CoreLogAgentServiceAdapter(agentService)), getContext, initialModelFromConfig, onModelChanged)
    {
        _ = agentService ?? throw new ArgumentNullException(nameof(agentService));
    }

    public ChatWindowViewModel()
        : this(new OllamaLogAgentService(), () => string.Empty, null, null)
    {
    }

    private sealed class CoreLogAgentServiceAdapter : CoreLogAgentService
    {
        private readonly ILogAgentService _inner;

        public CoreLogAgentServiceAdapter(ILogAgentService inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public System.Threading.Tasks.Task<string> SendMessageAsync(
            string userMessage,
            string contextSummary,
            string? model = null,
            IProgress<string>? contentProgress = null,
            System.Threading.CancellationToken cancellationToken = default)
            => _inner.SendMessageAsync(userMessage, contextSummary, model, contentProgress, cancellationToken);
    }
}
