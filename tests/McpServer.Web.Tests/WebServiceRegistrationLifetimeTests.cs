using McpServer.Cqrs;
using McpServer.UI.Core.Auth;
using McpServer.UI.Core.Hosting;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace McpServer.Web.Tests;

public sealed class WebServiceRegistrationLifetimeTests
{
    [Fact]
    public void AddWebServices_RegistersScopedRuntimeServices_PerScope()
    {
        using var rootProvider = CreateProvider();
        using var scopeA = rootProvider.CreateScope();
        using var scopeB = rootProvider.CreateScope();

        var dispatcherA1 = scopeA.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherA2 = scopeA.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherB = scopeB.ServiceProvider.GetRequiredService<Dispatcher>();

        var commandTargetA1 = scopeA.ServiceProvider.GetRequiredService<WebCommandTarget>();
        var commandTargetA2 = scopeA.ServiceProvider.GetRequiredService<McpServer.UI.Core.Commands.ICommandTarget>();
        var commandTargetB = scopeB.ServiceProvider.GetRequiredService<WebCommandTarget>();

        var hostContextA = scopeA.ServiceProvider.GetRequiredService<IMcpHostContext>();
        var hostContextB = scopeB.ServiceProvider.GetRequiredService<IMcpHostContext>();
        var webContextA1 = scopeA.ServiceProvider.GetRequiredService<WebMcpContext>();
        var webContextA2 = scopeA.ServiceProvider.GetRequiredService<WebMcpContext>();
        var webContextB = scopeB.ServiceProvider.GetRequiredService<WebMcpContext>();

        Assert.Same(dispatcherA1, dispatcherA2);
        Assert.NotSame(dispatcherA1, dispatcherB);

        Assert.Same(commandTargetA1, commandTargetA2);
        Assert.NotSame(commandTargetA1, commandTargetB);

        Assert.Same(webContextA1, hostContextA);
        Assert.Same(webContextB, hostContextB);
        Assert.Same(webContextA1, webContextA2);
        Assert.NotSame(webContextA1, webContextB);
    }

    [Fact]
    public void AddWebServices_UsesSharedWorkspaceContext_AcrossScopes()
    {
        using var rootProvider = CreateProvider();
        using var scopeA = rootProvider.CreateScope();
        using var scopeB = rootProvider.CreateScope();

        var workspaceContextA = scopeA.ServiceProvider.GetRequiredService<McpServer.UI.Core.ViewModels.WorkspaceContextViewModel>();
        var workspaceContextB = scopeB.ServiceProvider.GetRequiredService<McpServer.UI.Core.ViewModels.WorkspaceContextViewModel>();

        Assert.Same(workspaceContextA, workspaceContextB);
    }

    [Fact]
    public void AddWebServices_DoesNotRegisterDispatcher_AsLoggerProvider()
    {
        using var rootProvider = CreateProvider();
        using var scope = rootProvider.CreateScope();

        var loggerProviders = scope.ServiceProvider.GetServices<ILoggerProvider>();

        Assert.DoesNotContain(loggerProviders, static provider => provider is Dispatcher);
    }

    [Fact]
    public void AddWebServices_RegistersScopedHostIdentityProvider()
    {
        using var rootProvider = CreateProvider();
        using var scope = rootProvider.CreateScope();

        var identityProvider = scope.ServiceProvider.GetRequiredService<IHostIdentityProvider>();

        Assert.IsType<McpServer.Web.Authorization.WebHostIdentityProvider>(identityProvider);
        Assert.Equal(@"E:\\repo", identityProvider.GetWorkspacePath());
        Assert.Null(identityProvider.GetBearerToken());
    }

    [Fact]
    public async Task AddWebServices_KeepsDispatcherLogsVisible_WithinScope_WithoutCrossScopeLeakage()
    {
        using var rootProvider = CreateProvider();
        using var scopeA = rootProvider.CreateScope();
        using var scopeB = rootProvider.CreateScope();

        var targetA = scopeA.ServiceProvider.GetRequiredService<WebCommandTarget>();
        var dispatcherA = scopeA.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherB = scopeB.ServiceProvider.GetRequiredService<Dispatcher>();
        var logsViewModelA = scopeA.ServiceProvider.GetRequiredService<DispatcherLogsViewModel>();
        var logsViewModelB = scopeB.ServiceProvider.GetRequiredService<DispatcherLogsViewModel>();
        var invoked = false;

        var beforeA = dispatcherA.RecentDispatches.Count;
        var beforeB = dispatcherB.RecentDispatches.Count;

        await targetA.RunAsync(() => invoked = true);
        await logsViewModelA.LoadAsync();
        await logsViewModelB.LoadAsync();

        Assert.True(invoked);
        Assert.True(dispatcherA.RecentDispatches.Count > beforeA);
        Assert.Equal(beforeB, dispatcherB.RecentDispatches.Count);
        Assert.NotEmpty(logsViewModelA.Result);
        Assert.Empty(logsViewModelB.Result);
        Assert.All(logsViewModelA.Result, static record => Assert.False(string.IsNullOrWhiteSpace(record.OperationName)));
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
