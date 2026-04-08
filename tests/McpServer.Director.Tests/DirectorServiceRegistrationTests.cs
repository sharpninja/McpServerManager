using McpServer.Cqrs;
using McpServerManager.Director.Auth;
using McpServerManager.Director.Helpers;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.Navigation;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Director.Tests;

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
