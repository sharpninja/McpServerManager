using McpServer.Client;
using System.Reflection;
using Xunit;

namespace McpServerManager.Core.Tests.Integration;

public sealed class MainWindowViewModelAuthTests
{
    [Fact]
    public void ApplyClientConnectionState_SwitchesFromBearerToApiKeyMode()
    {
        using var http = new HttpClient();
        var client = new McpServerClient(http, new McpServerClientOptions
        {
            BaseUrl = new Uri("http://localhost:7147"),
            BearerToken = "jwt-token"
        });

        InvokeApplyClientConnectionState(
            client,
            bearerToken: null,
            apiKey: "agent-key",
            workspacePath: @"E:\github\RequestTracker");

        Assert.Equal(string.Empty, client.BearerToken);
        Assert.Equal("agent-key", client.ApiKey);
        Assert.False(client.Todo.RequireBearerToken);
        Assert.Equal(@"E:\github\RequestTracker", client.WorkspacePath);
    }

    [Fact]
    public void ApplyClientConnectionState_SwitchesFromApiKeyToBearerMode()
    {
        using var http = new HttpClient();
        var client = new McpServerClient(http, new McpServerClientOptions
        {
            BaseUrl = new Uri("http://localhost:7147"),
            ApiKey = "agent-key"
        });

        InvokeApplyClientConnectionState(
            client,
            bearerToken: "jwt-token",
            apiKey: "ignored-agent-key",
            workspacePath: @"E:\github\RequestTracker");

        Assert.Equal("jwt-token", client.BearerToken);
        Assert.Equal(string.Empty, client.ApiKey);
        Assert.True(client.Todo.RequireBearerToken);
        Assert.Equal(@"E:\github\RequestTracker", client.WorkspacePath);
    }

    private static void InvokeApplyClientConnectionState(
        McpServerClient client,
        string? bearerToken,
        string? apiKey,
        string workspacePath)
    {
        var method = typeof(McpServer.UI.Core.ViewModels.MainWindowViewModel)
            .GetMethod("ApplyClientConnectionState", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(null, [client, bearerToken, apiKey, workspacePath]);
    }
}
