using FluentAssertions;
using McpServer.Cqrs;
using McpServer.Client;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.Services;
using McpServerManager.Core;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using CoreMcpTodoService = McpServerManager.Core.Services.McpTodoService;

namespace McpServerManager.Core.Tests.Integration;

public sealed class DiContainerTests : IDisposable
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly ServiceProvider _provider;

    public DiContainerTests()
    {
        var http = new HttpClient();
        var options = new McpServerClientOptions { BaseUrl = new Uri("http://localhost:9999") };
        var client = new McpServerClient(http, options);
        var todoService = new CoreMcpTodoService(client, client);

        _provider = CreateProvider(_target, todoService, client, options.BaseUrl);
        _provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .AddProvider(_provider.GetRequiredService<Dispatcher>());
    }

    [Fact]
    public void ServiceProvider_Resolves_HealthApiClient()
    {
        var healthClient = _provider.GetService<McpServerManager.UI.Core.Services.IHealthApiClient>();
        healthClient.Should().NotBeNull();
        healthClient.Should().BeOfType<UiCoreHealthApiClientAdapter>();
    }

    [Fact]
    public void ServiceProvider_Resolves_Dispatcher()
    {
        var dispatcher = _provider.GetService<Dispatcher>();
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void ServiceProvider_Resolves_DefaultRoleAndAuthorizationServices()
    {
        var roleContext = _provider.GetRequiredService<IRoleContext>();
        var authPolicyService = _provider.GetRequiredService<IAuthorizationPolicyService>();
        var workspaceContext = _provider.GetRequiredService<McpServerManager.UI.Core.ViewModels.WorkspaceContextViewModel>();

        roleContext.GetType().Name.Should().Be("AllowAllRoleContext");
        authPolicyService.GetType().Name.Should().Be("AllowAllAuthorizationPolicyService");
        workspaceContext.Should().NotBeNull();
    }

    [Fact]
    public void ServiceProvider_Resolves_ICommandTarget()
    {
        var target = _provider.GetService<ICommandTarget>();
        target.Should().NotBeNull();
        target.Should().BeSameAs(_target.Object);
    }

    [Fact]
    public void ServiceProvider_Resolves_UiCoreTodoCopilotTarget()
    {
        var target = _provider.GetService<McpServerManager.UI.Core.Commands.ITodoCopilotTarget>();
        target.Should().NotBeNull();
        target.Should().BeSameAs(_target.Object);
    }

    [Fact]
    public void ServiceProvider_Resolves_NavigateBackHandler()
    {
        var handler = _provider.GetService<ICommandHandler<NavigateBackCommand, bool>>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<NavigateBackHandler>();
    }

    [Fact]
    public void ServiceProvider_Resolves_NavigateForwardHandler()
    {
        var handler = _provider.GetService<ICommandHandler<NavigateForwardCommand, bool>>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<NavigateForwardHandler>();
    }

    [Fact]
    public void ServiceProvider_Resolves_RefreshViewHandler()
    {
        var handler = _provider.GetService<ICommandHandler<RefreshViewCommand, bool>>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<RefreshViewHandler>();
    }

    [Fact]
    public void ServiceProvider_Resolves_AllNavigationHandlers()
    {
        _provider.GetService<ICommandHandler<NavigateBackCommand, bool>>()
            .Should().NotBeNull("NavigateBackHandler should be registered");

        _provider.GetService<ICommandHandler<NavigateForwardCommand, bool>>()
            .Should().NotBeNull("NavigateForwardHandler should be registered");

        _provider.GetService<ICommandHandler<PhoneNavigateSectionCommand, bool>>()
            .Should().NotBeNull("PhoneNavigateSectionHandler should be registered");
    }

    [Fact]
    public void ServiceProvider_Resolves_ClipboardHandlers()
    {
        _provider.GetService<ICommandHandler<CopyTextCommand, bool>>()
            .Should().NotBeNull("CopyTextHandler should be registered");

        _provider.GetService<ICommandHandler<CopyOriginalJsonCommand, bool>>()
            .Should().NotBeNull("CopyOriginalJsonHandler should be registered");
    }

    [Fact]
    public void ServiceProvider_Resolves_ArchiveHandlers()
    {
        _provider.GetService<ICommandHandler<ArchiveCurrentCommand, bool>>()
            .Should().NotBeNull("ArchiveCurrentHandler should be registered");

        _provider.GetService<ICommandHandler<ArchiveTreeItemCommand, bool>>()
            .Should().NotBeNull("ArchiveTreeItemHandler should be registered");
    }

    [Fact]
    public void DisposedProvider_RejectsFurtherResolution()
    {
        _provider.Dispose();

        var act = () => _provider.GetRequiredService<Dispatcher>();

        act.Should().Throw<ObjectDisposedException>();
    }

    public void Dispose() => _provider.Dispose();

    private static ServiceProvider CreateProvider(
        Mock<ICommandTarget> target,
        CoreMcpTodoService todoService,
        McpServerClient client,
        Uri baseUri)
    {
        var uiTarget = target.As<McpServerManager.UI.Core.Commands.ICommandTarget>();
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
