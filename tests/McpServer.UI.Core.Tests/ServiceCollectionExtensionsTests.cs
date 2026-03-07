using McpServer.Cqrs.Mvvm;
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
}
