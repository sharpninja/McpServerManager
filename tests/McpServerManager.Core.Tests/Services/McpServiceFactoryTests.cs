using FluentAssertions;
using McpServer.Client;
using McpServerManager.Core.Services;
using Xunit;

namespace McpServerManager.Core.Tests.Services;

public sealed class McpServiceFactoryTests
{
    private readonly McpServiceFactory _sut = new();

    private static McpServerClient CreateClient()
    {
        var http = new HttpClient();
        var options = new McpServerClientOptions { BaseUrl = new Uri("http://localhost:9999") };
        return new McpServerClient(http, options);
    }

    [Fact]
    public void CreateSessionLogService_ReturnsNonNull()
    {
        var client = CreateClient();
        var service = _sut.CreateSessionLogService(client);
        service.Should().NotBeNull();
        service.Should().BeOfType<McpSessionLogService>();
    }

    [Fact]
    public void CreateTodoService_ReturnsNonNull()
    {
        var client = CreateClient();
        var promptClient = CreateClient();
        var service = _sut.CreateTodoService(client, promptClient);
        service.Should().NotBeNull();
        service.Should().BeOfType<McpTodoService>();
    }

    [Fact]
    public void CreateWorkspaceService_ReturnsNonNull()
    {
        var client = CreateClient();
        var service = _sut.CreateWorkspaceService(client, new Uri("http://localhost:9999"));
        service.Should().NotBeNull();
        service.Should().BeOfType<McpWorkspaceService>();
    }

    [Fact]
    public void CreateVoiceService_ReturnsConfiguredInstance()
    {
        var service = _sut.CreateVoiceService(
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
    public void CreateEventStreamService_ReturnsConfiguredInstance()
    {
        var service = _sut.CreateEventStreamService(
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
}
