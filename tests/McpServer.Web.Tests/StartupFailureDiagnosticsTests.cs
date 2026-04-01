using System.IO;
using System.Net.Sockets;
using McpServerManager.Web;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class StartupFailureDiagnosticsTests
{
    [Fact]
    public void IsAddressAlreadyInUse_ReturnsTrue_ForNestedSocketException()
    {
        var exception = new IOException(
            "Failed to bind to address http://[::]:7147: address already in use.",
            new SocketException((int)SocketError.AddressAlreadyInUse));

        var result = StartupFailureDiagnostics.IsAddressAlreadyInUse(exception);

        Assert.True(result);
    }

    [Fact]
    public void BuildOperatorHint_ReturnsActionableMessage_ForAddressConflict()
    {
        var exception = new IOException(
            "Failed to bind to address http://[::]:7147: address already in use.",
            new SocketException((int)SocketError.AddressAlreadyInUse));

        var hint = StartupFailureDiagnostics.BuildOperatorHint(exception, "http://127.0.0.1:8901");

        Assert.NotNull(hint);
        Assert.Contains("http://[::]:7147", hint, StringComparison.Ordinal);
        Assert.Contains("--urls", hint, StringComparison.Ordinal);
        Assert.Contains("8901", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOperatorHint_ReturnsNull_ForNonBindingFailure()
    {
        var exception = new InvalidOperationException("OIDC discovery failed.");

        var hint = StartupFailureDiagnostics.BuildOperatorHint(exception);

        Assert.Null(hint);
    }
}
