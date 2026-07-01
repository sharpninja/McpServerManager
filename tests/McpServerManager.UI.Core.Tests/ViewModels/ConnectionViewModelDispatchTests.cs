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
/// Pure mock-only dispatch verification tests for ConnectionViewModel.
/// </summary>
public sealed class ConnectionViewModelDispatchTests
{
    [Fact]
    public async Task PerformOidcLogoutAsync_DispatchesLogoutCommand()
    {
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateConnection();

        // Setup private state via reflection for test (last url etc).
        SetPrivateField(vm, "_lastMcpBaseUrl", "https://example.com");
        SetPrivateField(vm, "_lastOidcAuthority", "https://auth.example.com");
        SetPrivateField(vm, "_oidcBearerToken", "token123");

        ViewModelDispatchTestHelper.SetupSendResult<LogoutCommand, bool>(dispatcher, true);

        // Call private method via reflection for verification.
        await CallPrivateMethod(vm, "PerformOidcLogoutAsync");

        await dispatcher.Received(1).SendAsync(Arg.Any<LogoutCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_DispatchesProbeHealthAndResolveUrlQuery()
    {
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateConnection();

        vm.Host = "example.com";
        vm.Port = "7147";

        ViewModelDispatchTestHelper.SetupQueryResult<ProbeHealthAndResolveUrlQuery, string>(dispatcher, "https://example.com");

        // Call the protected ConnectAsync via reflection.
        await CallPrivateMethod(vm, "ConnectAsync");

        await dispatcher.Received(1).QueryAsync(Arg.Any<ProbeHealthAndResolveUrlQuery>(), Arg.Any<CancellationToken>());
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private static async Task CallPrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var task = (Task?)method?.Invoke(target, null);
        if (task != null) await task;
    }
}