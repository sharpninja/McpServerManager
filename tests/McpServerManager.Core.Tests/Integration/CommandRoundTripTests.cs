using FluentAssertions;
using McpServer.Cqrs;
using McpServer.Client;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.Services;
using McpServerManager.Core;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Threading.Tasks;
using Xunit;
using CoreMcpTodoService = McpServerManager.Core.Services.McpTodoService;

namespace McpServerManager.Core.Tests.Integration;

public sealed class CommandRoundTripTests : IDisposable
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget, McpServerManager.UI.Core.Commands.ICommandTarget>();
    private readonly ServiceProvider _provider;
    private readonly Dispatcher _dispatcher;

    public CommandRoundTripTests()
    {
        var http = new HttpClient();
        var options = new McpServerClientOptions { BaseUrl = new Uri("http://localhost:9999") };
        var client = new McpServerClient(http, options);
        var todoService = new CoreMcpTodoService(client, client);

        _provider = CreateProvider(_target, todoService, client, options.BaseUrl);
        _provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .AddProvider(_provider.GetRequiredService<Dispatcher>());

        _dispatcher = _provider.GetRequiredService<Dispatcher>();
    }

    [Fact]
    public async Task NavigateBackCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new NavigateBackCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).NavigateBack();
    }

    [Fact]
    public async Task NavigateForwardCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new NavigateForwardCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).NavigateForward();
    }

    [Fact]
    public async Task OpenAgentConfigCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new OpenAgentConfigCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).OpenAgentConfig();
    }

    [Fact]
    public async Task OpenPromptTemplatesCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new OpenPromptTemplatesCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).OpenPromptTemplates();
    }

    [Fact]
    public async Task ArchiveCurrentCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new ArchiveCurrentCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).Archive();
    }

    [Fact]
    public async Task ToggleShowRawMarkdownCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new ToggleShowRawMarkdownCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).ToggleShowRawMarkdown();
    }

    [Fact]
    public async Task PhoneNavigateSectionCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new PhoneNavigateSectionCommand("details"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).PhoneNavigateSection("details");
    }

    [Fact]
    public async Task CopilotPlanCommand_DispatchesThroughDispatcher()
    {
        var uiCoreTarget = (McpServerManager.UI.Core.Commands.ITodoCopilotTarget)_target;
        uiCoreTarget.CopilotPlanAsync().Returns(Task.CompletedTask);

        var result = await _dispatcher.SendAsync(new McpServerManager.UI.Core.Commands.CopilotPlanCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await uiCoreTarget.Received(1).CopilotPlanAsync();
    }

    [Fact]
    public async Task MultipleCommands_DispatchSequentially()
    {
        await _dispatcher.SendAsync(new NavigateBackCommand(), TestContext.Current.CancellationToken);
        await _dispatcher.SendAsync(new NavigateForwardCommand(), TestContext.Current.CancellationToken);
        await _dispatcher.SendAsync(new NavigateBackCommand(), TestContext.Current.CancellationToken);

        _target.Received(2).NavigateBack();
        _target.Received(1).NavigateForward();
    }

    [Fact]
    public void CqrsRelayFactory_GenericCanExecutePredicate_UsesCommandParameter()
    {
        var command = CqrsRelayFactory.Create<string?>(
            _dispatcher,
            _ => Task.CompletedTask,
            value => !string.IsNullOrWhiteSpace(value));

        command.CanExecute(null).Should().BeFalse();
        command.CanExecute(string.Empty).Should().BeFalse();
        command.CanExecute("ready").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeUiActionCommand_WhenActionThrows_ReturnsFailureResult()
    {
        var result = await _dispatcher.SendAsync(
            new McpServerManager.UI.Core.Commands.InvokeUiActionCommand(() => throw new InvalidOperationException("boom")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("boom");
    }

    public void Dispose() => _provider.Dispose();

    private static ServiceProvider CreateProvider(
        ICommandTarget target,
        CoreMcpTodoService todoService,
        McpServerClient client,
        Uri baseUri)
    {
        var uiTarget = (McpServerManager.UI.Core.Commands.ICommandTarget)target;
        var services = new ServiceCollection();
        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Singleton;
            options.CommandTarget = uiTarget;
            options.TodoClient = new UiCoreTodoApiClientAdapter(todoService);
            options.HealthClient = new UiCoreHealthApiClientAdapter(client, baseUri);
            options.AdditionalHandlerAssemblies = [typeof(NavigateBackCommand).Assembly];
        });

        services.AddSingleton(target);
        services.AddSingleton<INavigationTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<IRequestDetailsTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<IPreviewTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<IArchiveTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<ISessionDataTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<IClipboardTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<IConfigTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<IUiDispatchTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.AddSingleton<ITodoCopilotTarget>(sp => sp.GetRequiredService<ICommandTarget>());

        return services.BuildServiceProvider();
    }
}
