using McpServer.Cqrs;
using McpServer.Director.Auth;
using McpServer.Director.Helpers;
using McpServer.UI.Core.Auth;
using McpServer.UI.Core.Authorization;
using McpServer.UI.Core.Hosting;
using McpServer.UI.Core.Navigation;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Director.Tests;

public sealed class DirectorServiceRegistrationTests
{
    [Fact]
    public void DirectorHost_UsesSharedHostComposition_AndPreservesDirectorOverrides()
    {
        using var provider = DirectorHost.CreateProvider();
        var directorContext = provider.GetRequiredService<DirectorMcpContext>();

        Assert.Same(directorContext, provider.GetRequiredService<DirectorMcpContext>());
        Assert.Same(directorContext, provider.GetRequiredService<IMcpHostContext>());
        Assert.IsType<TuiUiDispatcherService>(provider.GetRequiredService<IUiDispatcherService>());
        Assert.IsType<DirectorRoleContext>(provider.GetRequiredService<IRoleContext>());
        Assert.IsType<DirectorAuthorizationPolicyService>(provider.GetRequiredService<IAuthorizationPolicyService>());
        Assert.IsType<DirectorTabRegistry>(provider.GetRequiredService<ITabRegistry>());
        Assert.IsType<BrowserLauncher>(provider.GetRequiredService<IBrowserLauncher>());
        Assert.IsType<HealthApiClientAdapter>(provider.GetRequiredService<IHealthApiClient>());
        Assert.IsType<SessionLogApiClientAdapter>(provider.GetRequiredService<ISessionLogApiClient>());
        Assert.IsType<WorkspaceApiClientAdapter>(provider.GetRequiredService<IWorkspaceApiClient>());
        Assert.IsType<TodoApiClientAdapter>(provider.GetRequiredService<ITodoApiClient>());
        Assert.IsType<VoiceApiClientAdapter>(provider.GetRequiredService<IVoiceApiClient>());
        Assert.IsType<EventStreamApiClientAdapter>(provider.GetRequiredService<IEventStreamApiClient>());
        Assert.IsType<RepoApiClientAdapter>(provider.GetRequiredService<IRepoApiClient>());
        Assert.IsType<WorkspaceContextViewModel>(provider.GetRequiredService<WorkspaceContextViewModel>());
        Assert.IsType<Dispatcher>(provider.GetRequiredService<Dispatcher>());
    }

    [Fact]
    public void DirectorHost_CreateProvider_AppliesAdditionalConfigBeforeBuild()
    {
        using var provider = DirectorHost.CreateProvider(additionalConfig: (services, _) =>
        {
            services.AddSingleton<TestMarker>();
        });

        Assert.IsType<TestMarker>(provider.GetRequiredService<TestMarker>());
    }

    [Fact]
    public void DirectorHost_RegistersHostIdentityProvider()
    {
        using var provider = DirectorHost.CreateProvider();

        var identityProvider = provider.GetRequiredService<IHostIdentityProvider>();

        Assert.IsType<DirectorHostIdentityProvider>(identityProvider);
        Assert.Equal(provider.GetRequiredService<DirectorMcpContext>().ActiveWorkspacePath, identityProvider.GetWorkspacePath());
    }

    private sealed class TestMarker;
}
