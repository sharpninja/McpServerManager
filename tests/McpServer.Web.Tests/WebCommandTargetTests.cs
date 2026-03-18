using System;
using System.Threading.Tasks;
using McpServer.UI.Core.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpServer.Web.Tests;

public sealed class WebCommandTargetTests
{
    [Fact]
    public void AddWebServices_RegistersWebCommandTarget_AsCommandTarget()
    {
        using var provider = CreateProvider();

        var concrete = provider.GetRequiredService<WebCommandTarget>();
        var target = provider.GetRequiredService<ICommandTarget>();

        Assert.Same(concrete, target);
    }

    [Fact]
    public async Task RunAsync_ExecutesWrappedAction()
    {
        using var provider = CreateProvider();
        var target = provider.GetRequiredService<WebCommandTarget>();
        var invoked = false;

        await target.RunAsync(() => invoked = true);

        Assert.True(invoked);
        Assert.True(string.IsNullOrEmpty(target.StatusMessage));
    }

    [Fact]
    public async Task RefreshAsync_ThrowsExplicitNotSupportedException()
    {
        using var provider = CreateProvider();
        var target = provider.GetRequiredService<WebCommandTarget>();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => target.RefreshAsync());

        Assert.Contains("RefreshAsync", ex.Message, StringComparison.Ordinal);
        Assert.Equal(ex.Message, target.StatusMessage);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = "test-api-key",
                ["McpServer:WorkspacePath"] = @"E:\\repo"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddWebServices();
        return services.BuildServiceProvider();
    }
}
