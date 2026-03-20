using System.Net;
using System.Net.Sockets;
using McpServer.Web;
using Xunit;

namespace McpServer.Web.Tests;

public sealed class WebListenUrlSelectorTests
{
    [Fact]
    public void ResolveSelection_UsesCommandLineUrls_WhenProvided()
    {
        var selection = WebListenUrlSelector.ResolveSelection(["--urls", "http://+:9900"], _ => null);

        Assert.True(selection.IsExplicit);
        Assert.Equal("http://+:9900", selection.Urls);
    }

    [Fact]
    public void ResolveSelection_UsesEnvironmentUrls_WhenProvided()
    {
        var selection = WebListenUrlSelector.ResolveSelection([], name =>
            name == "ASPNETCORE_URLS" ? "http://127.0.0.1:9901" : null);

        Assert.True(selection.IsExplicit);
        Assert.Equal("http://127.0.0.1:9901", selection.Urls);
    }

    [Fact]
    public void FindAvailableLoopbackUrl_SkipsOccupiedStartPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var selectedUrl = WebListenUrlSelector.FindAvailableLoopbackUrl(occupiedPort);
        var selectedPort = new Uri(selectedUrl).Port;

        Assert.True(selectedPort > occupiedPort);
        Assert.StartsWith("http://127.0.0.1:", selectedUrl, StringComparison.Ordinal);
    }
}
