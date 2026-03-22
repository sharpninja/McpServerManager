using FluentAssertions;
using McpServer.Cqrs;
using McpServer.Client;
using McpServer.UI.Core.Hosting;
using McpServer.UI.Core.Services;
using McpServerManager.Core;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Threading.Tasks;
using Xunit;
using CoreMcpTodoService = McpServerManager.Core.Services.McpTodoService;

namespace McpServerManager.Core.Tests.Integration;

public sealed class CommandRoundTripTests : IDisposable
{
    private readonly Mock<ICommandTarget> _target = new();
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
        var result = await _dispatcher.SendAsync(new NavigateBackCommand());

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.NavigateBack(), Times.Once);
    }

    [Fact]
    public async Task NavigateForwardCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new NavigateForwardCommand());

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.NavigateForward(), Times.Once);
    }

    [Fact]
    public async Task OpenAgentConfigCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new OpenAgentConfigCommand());

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.OpenAgentConfig(), Times.Once);
    }

    [Fact]
    public async Task OpenPromptTemplatesCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new OpenPromptTemplatesCommand());

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.OpenPromptTemplates(), Times.Once);
    }

    [Fact]
    public async Task ArchiveCurrentCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new ArchiveCurrentCommand());

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.Archive(), Times.Once);
    }

    [Fact]
    public async Task ToggleShowRawMarkdownCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new ToggleShowRawMarkdownCommand());

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.ToggleShowRawMarkdown(), Times.Once);
    }

    [Fact]
    public async Task PhoneNavigateSectionCommand_DispatchesThroughDispatcher()
    {
        var result = await _dispatcher.SendAsync(new PhoneNavigateSectionCommand("details"));

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.PhoneNavigateSection("details"), Times.Once);
    }

    [Fact]
    public async Task CopilotPlanCommand_DispatchesThroughDispatcher()
    {
        var uiCoreTarget = _target.As<McpServer.UI.Core.Commands.ITodoCopilotTarget>();
        uiCoreTarget.Setup(t => t.CopilotPlanAsync()).Returns(Task.CompletedTask);

        var result = await _dispatcher.SendAsync(new McpServer.UI.Core.Commands.CopilotPlanCommand());

        result.IsSuccess.Should().BeTrue();
        uiCoreTarget.Verify(t => t.CopilotPlanAsync(), Times.Once);
    }

    [Fact]
    public async Task MultipleCommands_DispatchSequentially()
    {
        await _dispatcher.SendAsync(new NavigateBackCommand());
        await _dispatcher.SendAsync(new NavigateForwardCommand());
        await _dispatcher.SendAsync(new NavigateBackCommand());

        _target.Verify(t => t.NavigateBack(), Times.Exactly(2));
        _target.Verify(t => t.NavigateForward(), Times.Once);
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
            new McpServer.UI.Core.Commands.InvokeUiActionCommand(() => throw new InvalidOperationException("boom")));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("boom");
    }

    public void Dispose() => _provider.Dispose();

    private static ServiceProvider CreateProvider(
        Mock<ICommandTarget> target,
        CoreMcpTodoService todoService,
        McpServerClient client,
        Uri baseUri)
    {
        var uiTarget = target.As<McpServer.UI.Core.Commands.ICommandTarget>();
        var services = new ServiceCollection();
        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Singleton;
            options.CommandTarget = uiTarget.Object;
            options.TodoClient = new UiCoreTodoApiClientAdapter(todoService);
            options.HealthClient = new UiCoreHealthApiClientAdapter(client, baseUri);
            options.AdditionalHandlerAssemblies = [typeof(NavigateBackCommand).Assembly];
        });

        services.AddSingleton<ICommandTarget>(target.Object);
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
