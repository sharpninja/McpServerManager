using System;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Core.Services;

public interface IChatWindowViewModelFactory
{
    ChatWindowViewModel Create(Func<string> getContext);
}

/// <summary>Compliant composition factory for chat ViewModels. Owns handler registration and config-model integration.</summary>
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
        var mediator = CreateMediator(agentService);
        return new ChatWindowViewModel(
            mediator,
            getContext,
            _readSelectedModel(),
            _writeSelectedModel);
    }

    public static Mediator CreateMediator(ILogAgentService agentService)
    {
        if (agentService == null) throw new ArgumentNullException(nameof(agentService));

        var mediator = new Mediator();

        var promptTemplateService = new LocalChatPromptTemplateService();
        var configFilesService = new LocalChatConfigFilesService();
        var modelDiscoveryService = new OllamaChatModelDiscoveryService();
        var sendService = new LogAgentChatSendOrchestrationService(agentService);

        mediator.Register<ChatOpenAgentConfigCommand, ChatFileOpenResult>(new ChatOpenAgentConfigHandler(configFilesService));
        mediator.Register<ChatOpenPromptTemplatesCommand, ChatFileOpenResult>(new ChatOpenPromptTemplatesHandler(configFilesService));
        mediator.Register<ChatLoadPromptsCommand, System.Collections.Generic.IReadOnlyList<PromptTemplate>>(
            new ChatLoadPromptsHandler(promptTemplateService));
        mediator.Register<ChatPopulatePromptCommand, string>(new ChatPopulatePromptHandler());
        mediator.Register<ChatSubmitPromptCommand, ChatPreparedPromptResult>(new ChatSubmitPromptHandler());
        mediator.RegisterQuery(new ChatLoadModelsHandler(modelDiscoveryService));
        mediator.Register<ChatSendMessageCommand, ChatSendMessageResult>(new ChatSendMessageHandler(sendService));

        return mediator;
    }
}
