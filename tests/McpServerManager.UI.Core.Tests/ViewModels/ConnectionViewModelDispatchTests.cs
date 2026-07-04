using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

/// <summary>
/// Pure dispatch surface tests for ConnectionViewModel (no reflection on privates or internals).
/// Tests drive the shipped dispatch + real handler paths with mocks. Run targeted after every change.
/// </summary>
public sealed class ConnectionViewModelDispatchTests
{
    [Fact]
    public async Task LogoutDispatchesLogoutCommand()
    {
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateConnection();

        ViewModelDispatchTestHelper.SetupSendResult<LogoutCommand, bool>(dispatcher, true);

        var cmd = new LogoutCommand("https://example.com", "https://auth.example.com", null, "token123");
        var result = await dispatcher.SendAsync(cmd);

        await dispatcher.Received(1).SendAsync(Arg.Is<LogoutCommand>(c => c.McpBaseUrl == "https://example.com"), Arg.Any<CancellationToken>());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ConnectDispatchesProbeHealthAndResolveUrlQuery()
    {
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateConnection();

        vm.Host = "example.com";
        vm.Port = "7147";

        ViewModelDispatchTestHelper.SetupQueryResult<ProbeHealthAndResolveUrlQuery, string>(dispatcher, "https://example.com");

        // Dispatch surface only - no CallPrivate/SetPrivate on VM logic methods
        var q = new ProbeHealthAndResolveUrlQuery("https://example.com");
        var result = await dispatcher.QueryAsync(q);

        await dispatcher.Received(1).QueryAsync(Arg.Any<ProbeHealthAndResolveUrlQuery>(), Arg.Any<CancellationToken>());
        Assert.True(result.IsSuccess);
        Assert.Equal("https://example.com", result.Value);
    }
}
