using System;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace McpServerManager.UI.Core.Tests.TestInfrastructure;

/// <summary>
/// Mock-only harness for ViewModel CQRS dispatch verification (Byrd compliant, per strategist).
/// Constructs VMs using Substitute.For<Dispatcher>() directly.
/// Bans AddCqrs / AddUiCore / ServiceProvider.GetRequiredService<Dispatcher>() in dispatch-verification tests.
/// </summary>
public static class ViewModelDispatchTestHelper
{
    /// <summary>
    /// Creates a ChatWindowViewModel under test with mocked dispatcher.
    /// Caller configures dispatcher.QueryAsync/SendAsync .Returns(...) before calling VM actions.
    /// </summary>
    public static (IDispatcher dispatcher, ChatWindowViewModel vm) CreateChatWindow(
        Func<string>? getContext = null,
        string? initialModel = null,
        Action<string?>? onModelChanged = null,
        IUiDispatcherService? uiDispatcher = null)
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var ui = uiDispatcher ?? new ImmediateUiDispatcherService();
        var vm = new ChatWindowViewModel(
            dispatcher,
            getContext ?? (() => string.Empty),
            initialModel,
            onModelChanged,
            ui);
        return (dispatcher, vm);
    }

    /// <summary>
    /// Creates ConnectionViewModel with mocked dispatcher.
    /// </summary>
    public static (IDispatcher dispatcher, ConnectionViewModel vm) CreateConnection(
        IUiDispatcherService? uiDispatcher = null,
        ILogger<ConnectionViewModel>? logger = null)
    {
        var dispatcher = Substitute.For<IDispatcher>();
        var ui = uiDispatcher ?? new ImmediateUiDispatcherService();
        var log = logger ?? NullLogger<ConnectionViewModel>.Instance;
        var vm = new ConnectionViewModel(
            dispatcher,
            log,
            ui);
        return (dispatcher, vm);
    }

    /// <summary>
    /// Helper to setup a canned query result.
    /// </summary>
    public static void SetupQueryResult<TMessage, TResult>(IDispatcher dispatcher, TResult value)
        where TMessage : IQuery<TResult>
    {
        dispatcher.QueryAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<TResult>.Success(value));
    }

    /// <summary>
    /// Helper to setup a canned send result.
    /// </summary>
    public static void SetupSendResult<TMessage, TResult>(IDispatcher dispatcher, TResult value)
        where TMessage : ICommand<TResult>
    {
        dispatcher.SendAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<TResult>.Success(value));
    }

    /// <summary>
    /// Verifies exact message was sent (for dispatch tests).
    /// </summary>
    public static async Task ReceivedQuery<TMessage, TResult>(IDispatcher dispatcher, int count = 1)
        where TMessage : IQuery<TResult>
    {
        await dispatcher.Received(count).QueryAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>());
    }

    public static async Task ReceivedSend<TMessage, TResult>(IDispatcher dispatcher, int count = 1)
        where TMessage : ICommand<TResult>
    {
        await dispatcher.Received(count).SendAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>());
    }
}