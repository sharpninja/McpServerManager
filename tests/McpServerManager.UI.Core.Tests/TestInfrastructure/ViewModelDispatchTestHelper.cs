using System;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace McpServerManager.UI.Core.Tests.TestInfrastructure;

/// <summary>
/// Mock-only harness for ViewModel CQRS dispatch verification (Byrd compliant).
/// Constructs VM passing a Substitute.For<Dispatcher>() directly.
/// No AddCqrs, no AddUiCore, no ServiceProvider.GetRequiredService<Dispatcher>().
/// Tests must assert Received(1) exact message and resulting observable state.
/// </summary>
public static class ViewModelDispatchTestHelper
{
    /// <summary>
    /// Create a ChatWindowViewModel under test with mocked dispatcher.
    /// The caller can configure dispatcher.QueryAsync / SendAsync .Returns(...) before invoking VM actions.
    /// </summary>
    public static (Dispatcher dispatcher, ChatWindowViewModel vm) CreateChatWindow(
        Func<string>? getContext = null,
        string? initialModel = null,
        Action<string?>? onModelChanged = null)
    {
        var dispatcher = Substitute.For<Dispatcher>();
        var uiDisp = new ImmediateUiDispatcherService();
        // Note: after VM change, ctor will take Dispatcher + ui services; no IChatWindowService in VM.
        var vm = new ChatWindowViewModel(
            dispatcher,
            getContext ?? (() => string.Empty),
            initialModel,
            onModelChanged,
            uiDisp);
        return (dispatcher, vm);
    }

    /// <summary>
    /// Generic helper for VMs that take Dispatcher as first or prominent dep + other pure UI deps.
    /// For composite or special VMs, callers can wire manually.
    /// </summary>
    public static (Dispatcher dispatcher, T vm) CreateWithDispatcher<T>(Func<Dispatcher, T> factory) where T : class
    {
        var dispatcher = Substitute.For<Dispatcher>();
        var vm = factory(dispatcher);
        return (dispatcher, vm);
    }

    /// <summary>
    /// Helper to configure a canned query result on the mocked dispatcher.
    /// </summary>
    public static void SetupQueryResult<TMessage, TResult>(Dispatcher dispatcher, TResult value)
        where TMessage : IQuery<TResult>
    {
        dispatcher.QueryAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<TResult>.Success(value));
    }

    /// <summary>
    /// Helper to configure a canned send result.
    /// </summary>
    public static void SetupSendResult<TMessage, TResult>(Dispatcher dispatcher, TResult value)
        where TMessage : ICommand<TResult>
    {
        dispatcher.SendAsync(Arg.Any<TMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<TResult>.Success(value));
    }
}