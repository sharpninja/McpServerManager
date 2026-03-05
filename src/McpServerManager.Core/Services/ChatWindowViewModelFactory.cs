using System;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core.Services;

public interface IChatWindowViewModelFactory
{
    ChatWindowViewModel Create(Func<string> getContext);
}

/// <summary>Compliant composition factory for chat ViewModels. Builds a DI container with chat handlers and Dispatcher.</summary>
public sealed class ChatWindowViewModelFactory : IChatWindowViewModelFactory
{
    private readonly Func<ILogAgentService> _agentServiceFactory;
    private readonly Func<string?> _readSelectedModel;
    private readonly Action<string?> _writeSelectedModel;

    public ChatWindowViewModelFactory(
        Func<ILogAgentService>? agentServiceFactory = null,
        Func<string?>? readSelectedModel = null,
        Action<string?>? writeSelectedModel = null)
    {
        _agentServiceFactory = agentServiceFactory ?? (() => new OllamaLogAgentService());
        _readSelectedModel = readSelectedModel ?? AgentConfigIo.GetModelFromConfig;
        _writeSelectedModel = writeSelectedModel ?? AgentConfigIo.SetModelInConfig;
    }

    public ChatWindowViewModel Create(Func<string> getContext)
    {
        var agentService = _agentServiceFactory();
        var dispatcher = CreateDispatcher(agentService);
        return new ChatWindowViewModel(
            dispatcher,
            getContext,
            _readSelectedModel(),
            _writeSelectedModel);
    }

    public static McpServer.Cqrs.Dispatcher CreateDispatcher(ILogAgentService agentService)
    {
        if (agentService == null) throw new ArgumentNullException(nameof(agentService));

        var services = new ServiceCollection();
        services.AddSingleton(AppLogService.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(sp => sp.GetRequiredService<AppLogService>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(AppLogger<>));

        // Register chat-specific services for handler DI
        services.AddSingleton<IChatPromptTemplateService>(new LocalChatPromptTemplateService());
        services.AddSingleton<IChatConfigFilesService>(new LocalChatConfigFilesService());
        services.AddSingleton<IChatModelDiscoveryService>(new OllamaChatModelDiscoveryService());
        services.AddSingleton<IChatSendOrchestrationService>(new LogAgentChatSendOrchestrationService(agentService));

        services.AddCqrsDispatcher();
        services.AddCqrsHandlers(typeof(ChatOpenAgentConfigCommand).Assembly);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<McpServer.Cqrs.Dispatcher>();
    }
}
