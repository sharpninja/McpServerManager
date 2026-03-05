using FluentAssertions;
using McpServer.Client;
using McpServerManager.Core.Services;
using Xunit;

namespace McpServerManager.Core.Tests.Integration;

public sealed class FactoryIntegrationTests
{
    private readonly McpServiceFactory _factory = new();

    private static McpServerClient CreateClient()
    {
        var http = new HttpClient();
        var options = new McpServerClientOptions { BaseUrl = new Uri("http://localhost:9999") };
        return new McpServerClient(http, options);
    }

    [Fact]
    public void CreateSessionLogService_ReturnsWorkingService()
    {
        var client = CreateClient();
        var service = _factory.CreateSessionLogService(client);

        service.Should().NotBeNull();
        service.Should().BeOfType<McpSessionLogService>();
    }

    [Fact]
    public void CreateTodoService_ReturnsWorkingService()
    {
        var client = CreateClient();
        var service = _factory.CreateTodoService(client, client);

        service.Should().NotBeNull();
        service.Should().BeOfType<McpTodoService>();
    }

    [Fact]
    public void CreateWorkspaceService_ReturnsWorkingService()
    {
        var client = CreateClient();
        var service = _factory.CreateWorkspaceService(client, new Uri("http://localhost:9999"));

        service.Should().NotBeNull();
        service.Should().BeOfType<McpWorkspaceService>();
    }

    [Fact]
    public void CreateVoiceService_ReturnsWorkingService()
    {
        var service = _factory.CreateVoiceService(
            baseUrl: "http://localhost:9999",
            apiKey: "test-key",
            bearerToken: null,
            resolveBaseUrl: () => "http://localhost:9999",
            resolveBearerToken: () => null,
            resolveApiKey: () => "test-key",
            resolveWorkspacePath: () => "/workspace");

        service.Should().NotBeNull();
        service.Should().BeOfType<McpVoiceConversationService>();
    }

    [Fact]
    public void CreateEventStreamService_ReturnsWorkingService()
    {
        var service = _factory.CreateEventStreamService(
            baseUrl: "http://localhost:9999",
            apiKey: "test-key",
            bearerToken: null,
            resolveBaseUrl: () => "http://localhost:9999",
            resolveBearerToken: () => null,
            resolveApiKey: () => "test-key",
            resolveWorkspacePath: () => "/workspace");

        service.Should().NotBeNull();
        service.Should().BeOfType<McpAgentEventStreamService>();
    }

    [Fact]
    public void CreateSessionLogService_WithDifferentClients_ReturnsDistinctInstances()
    {
        var client1 = CreateClient();
        var client2 = CreateClient();

        var service1 = _factory.CreateSessionLogService(client1);
        var service2 = _factory.CreateSessionLogService(client2);

        service1.Should().NotBeSameAs(service2);
    }
}
