using System;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Handlers;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace McpServerManager.UI.Core.Tests.TestInfrastructure;

/// <summary>
/// Harness for ViewModel CQRS dispatch verification using the real sealed dispatcher.
/// Tests assert the dispatched handler's service dependency and resulting observable state.
/// </summary>
public static class ViewModelDispatchTestHelper
{
    /// <summary>
    /// Create a ChatWindowViewModel under test with a real dispatcher and substituted chat service.
    /// The caller can configure the chat service before invoking VM actions.
    /// </summary>
    public static (Dispatcher dispatcher, ChatWindowViewModel vm, IChatWindowService chatService) CreateChatWindow(
        Func<string>? getContext = null,
        string? initialModel = null,
        Action<string?>? onModelChanged = null)
    {
        var chatService = Substitute.For<IChatWindowService>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(chatService);
        services.AddSingleton<IQueryHandler<LoadChatPromptsQuery, IReadOnlyList<PromptTemplate>>, LoadChatPromptsQueryHandler>();
        services.AddSingleton<IQueryHandler<LoadChatModelsQuery, ChatLoadModelsResult>, LoadChatModelsQueryHandler>();
        services.AddSingleton<IQueryHandler<PopulateChatPromptQuery, string>, PopulateChatPromptQueryHandler>();
        services.AddSingleton<IQueryHandler<SubmitChatPromptQuery, Messages.ChatPreparedPromptResult>, SubmitChatPromptQueryHandler>();
        services.AddSingleton<ICommandHandler<SendChatMessageCommand, ChatSendMessageResult>, SendChatMessageCommandHandler>();
        services.AddSingleton<ICommandHandler<OpenChatAgentConfigCommand, ChatFileOpenResult>, OpenChatAgentConfigCommandHandler>();
        services.AddSingleton<ICommandHandler<OpenChatPromptTemplatesCommand, ChatFileOpenResult>, OpenChatPromptTemplatesCommandHandler>();
        services.AddSingleton(sp => new Dispatcher(sp, NullLogger<Dispatcher>.Instance));

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<Dispatcher>();
        var uiDisp = new ImmediateUiDispatcherService();
        var vm = new ChatWindowViewModel(
            dispatcher,
            getContext ?? (() => string.Empty),
            initialModel,
            onModelChanged,
            uiDisp);
        return (dispatcher, vm, chatService);
    }

    /// <summary>
    /// Generic helper for VMs that take a real dispatcher.
    /// For composite or special VMs, callers can wire services manually.
    /// </summary>
    public static (Dispatcher dispatcher, T vm) CreateWithDispatcher<T>(Func<Dispatcher, T> factory) where T : class
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(provider, NullLogger<Dispatcher>.Instance);
        var vm = factory(dispatcher);
        return (dispatcher, vm);
    }
}
