using FluentAssertions;
using McpServer.Cqrs;
using McpServer.Client;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

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
        var todoService = new McpTodoService(client, client);

        _provider = UiCoreServiceProviderFactory.Build(
            _target.Object,
            todoService: todoService,
            mcpClient: client,
            mcpBaseUrl: options.BaseUrl);
    }

    [Fact]
    public void ServiceProvider_Resolves_HealthApiClient()
    {
        var healthClient = _provider.GetService<McpServer.UI.Core.Services.IHealthApiClient>();
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
    public void ServiceProvider_Resolves_ICommandTarget()
    {
        var target = _provider.GetService<ICommandTarget>();
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
    public void Build_WithoutAnyService_Throws()
    {
        var act = () => UiCoreServiceProviderFactory.Build(_target.Object);
        act.Should().Throw<ArgumentException>();
    }

    public void Dispose() => _provider.Dispose();
}
