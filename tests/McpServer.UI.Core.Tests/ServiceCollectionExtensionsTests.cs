using McpServer.Cqrs.Mvvm;
using McpServer.Cqrs;
using McpServer.UI.Core.Hosting;
using McpServer.UI.Core.Tests.TestInfrastructure;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServer.UI.Core.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUiCore_RegistersVoiceViewModelAlias()
    {
        using var sp = UiCoreTestHost.Create();
        var registry = sp.GetRequiredService<IViewModelRegistry>();

        Assert.True(registry.ViewModels.ContainsKey("voice-session"));
        Assert.Equal(typeof(VoiceViewModel), registry.ViewModels["voice-session"]);
    }

    [Fact]
    public void AddUiCore_ResolvesVoiceViewModelAsTransient()
    {
        using var sp = UiCoreTestHost.Create();

        var first = sp.GetRequiredService<VoiceViewModel>();
        var second = sp.GetRequiredService<VoiceViewModel>();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddUiCore_ResolvesBackendConnectionMonitor_WithDefaultHealthClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddUiCore();

        using var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<McpServer.UI.Core.Services.BackendConnectionMonitor>();

        Assert.NotNull(monitor);
    }

    [Fact]
    public void AddDispatcherForHostLifetime_Singleton_RegistersSharedDispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddDispatcherForHostLifetime(McpHostLifetimeStrategy.Singleton);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var rootDispatcher = provider.GetRequiredService<Dispatcher>();
        var scopedDispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherAlias = provider.GetRequiredService<IDispatcher>();

        Assert.Same(rootDispatcher, scopedDispatcher);
        Assert.Same(rootDispatcher, dispatcherAlias);
    }

    [Fact]
    public void AddDispatcherForHostLifetime_Scoped_RegistersDispatcherPerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddDispatcherForHostLifetime(McpHostLifetimeStrategy.Scoped);

        using var provider = services.BuildServiceProvider();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var dispatcherA1 = scopeA.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherA2 = scopeA.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherB = scopeB.ServiceProvider.GetRequiredService<Dispatcher>();
        var dispatcherAlias = scopeA.ServiceProvider.GetRequiredService<IDispatcher>();

        Assert.Same(dispatcherA1, dispatcherA2);
        Assert.NotSame(dispatcherA1, dispatcherB);
        Assert.Same(dispatcherA1, dispatcherAlias);
    }
}
