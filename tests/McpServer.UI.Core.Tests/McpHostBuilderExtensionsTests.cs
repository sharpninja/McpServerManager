using System;
using McpServer.Client;
using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Auth;
using McpServer.UI.Core.Hosting;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace McpServer.UI.Core.Tests;

public sealed class McpHostBuilderExtensionsTests
{
    [Fact]
    public void AddMcpHost_RegistersExplicitClients_AndOptionalCommandTargetAliases()
    {
        var services = new ServiceCollection();
        var commandTarget = Substitute.For<ICommandTarget>();
        var todoClient = Substitute.For<ITodoApiClient>();
        var workspaceClient = Substitute.For<IWorkspaceApiClient>();

        services.AddMcpHost(options =>
        {
            options.CommandTarget = commandTarget;
            options.TodoClient = todoClient;
            options.WorkspaceClient = workspaceClient;
        });

        using var provider = services.BuildServiceProvider();

        Assert.Same(todoClient, provider.GetRequiredService<ITodoApiClient>());
        Assert.Same(workspaceClient, provider.GetRequiredService<IWorkspaceApiClient>());
        Assert.Same(commandTarget, provider.GetRequiredService<ICommandTarget>());
        Assert.Same(commandTarget, provider.GetRequiredService<INavigationTarget>());
        Assert.Same(commandTarget, provider.GetRequiredService<ITodoCopilotTarget>());
        Assert.IsType<WorkspaceContextViewModel>(provider.GetRequiredService<WorkspaceContextViewModel>());
    }

    [Fact]
    public void AddMcpHost_RegistersScopedFactories_ForExplicitClients_AndCommandTargets()
    {
        var services = new ServiceCollection();

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Scoped;
            options.CommandTargetFactory = static _ => Substitute.For<ICommandTarget>();
            options.TodoClientFactory = static _ => Substitute.For<ITodoApiClient>();
            options.WorkspaceClientFactory = static _ => Substitute.For<IWorkspaceApiClient>();
        });

        using var provider = services.BuildServiceProvider();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var commandTargetA1 = scopeA.ServiceProvider.GetRequiredService<ICommandTarget>();
        var commandTargetA2 = scopeA.ServiceProvider.GetRequiredService<INavigationTarget>();
        var commandTargetB = scopeB.ServiceProvider.GetRequiredService<ICommandTarget>();
        var todoClientA = scopeA.ServiceProvider.GetRequiredService<ITodoApiClient>();
        var todoClientB = scopeB.ServiceProvider.GetRequiredService<ITodoApiClient>();
        var workspaceClientA = scopeA.ServiceProvider.GetRequiredService<IWorkspaceApiClient>();
        var workspaceClientB = scopeB.ServiceProvider.GetRequiredService<IWorkspaceApiClient>();

        Assert.Same(commandTargetA1, commandTargetA2);
        Assert.NotSame(commandTargetA1, commandTargetB);
        Assert.NotSame(todoClientA, todoClientB);
        Assert.NotSame(workspaceClientA, workspaceClientB);
    }

    [Fact]
    public void AddMcpHost_BootstrapMode_RegistersScopedServices_PerScope()
    {
        var services = new ServiceCollection();
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = @"E:\repo" };

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Scoped;
            options.McpBaseUrl = new Uri("http://localhost:7147");
            options.ApiKey = "test-api-key";
            options.ResolveWorkspacePath = () => @"E:\repo";
            options.WorkspaceContext = workspaceContext;
            options.ClientFactoryOverride = CreateTestClient;
            options.PromptClientFactoryOverride = CreateTestClient;
        });

        using var provider = services.BuildServiceProvider();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var dispatcherA1 = scopeA.ServiceProvider.GetRequiredService<McpServer.Cqrs.Dispatcher>();
        var dispatcherA2 = scopeA.ServiceProvider.GetRequiredService<McpServer.Cqrs.Dispatcher>();
        var dispatcherB = scopeB.ServiceProvider.GetRequiredService<McpServer.Cqrs.Dispatcher>();
        var todoService = scopeA.ServiceProvider.GetRequiredService<McpTodoService>();
        var workspaceService = scopeA.ServiceProvider.GetRequiredService<McpWorkspaceService>();
        var sessionLogService = scopeA.ServiceProvider.GetRequiredService<McpSessionLogService>();
        var voiceService = scopeA.ServiceProvider.GetRequiredService<McpVoiceConversationService>();
        var eventStreamService = scopeA.ServiceProvider.GetRequiredService<McpAgentEventStreamService>();

        Assert.Same(dispatcherA1, dispatcherA2);
        Assert.NotSame(dispatcherA1, dispatcherB);
        Assert.NotNull(todoService);
        Assert.NotNull(workspaceService);
        Assert.NotNull(sessionLogService);
        Assert.NotNull(voiceService);
        Assert.NotNull(eventStreamService);
        Assert.Same(workspaceContext, scopeA.ServiceProvider.GetRequiredService<WorkspaceContextViewModel>());
        Assert.Null(scopeA.ServiceProvider.GetService<ICommandTarget>());
        Assert.NotNull(scopeA.ServiceProvider.GetRequiredService<ITodoApiClient>());
        Assert.NotNull(scopeA.ServiceProvider.GetRequiredService<IWorkspaceApiClient>());
        Assert.NotNull(scopeA.ServiceProvider.GetRequiredService<ISessionLogApiClient>());
        Assert.NotNull(scopeA.ServiceProvider.GetRequiredService<IVoiceApiClient>());
        Assert.NotNull(scopeA.ServiceProvider.GetRequiredService<IEventStreamApiClient>());
        Assert.Same(voiceService, scopeA.ServiceProvider.GetRequiredService<IVoiceConversationService>());
        Assert.Equal(@"E:\repo", voiceService.ResolveWorkspacePath?.Invoke());
        Assert.Equal(@"E:\repo", eventStreamService.ResolveWorkspacePath?.Invoke());
    }

    [Fact]
    public async Task AddMcpHost_BootstrapMode_RegistersHostContext_AndKeepsWorkspaceStateAligned()
    {
        var services = new ServiceCollection();
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = @"E:\repo" };

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Scoped;
            options.McpBaseUrl = new Uri("http://localhost:7147");
            options.ApiKey = "test-api-key";
            options.ResolveWorkspacePath = () => workspaceContext.ActiveWorkspacePath;
            options.WorkspaceContext = workspaceContext;
            options.ClientFactoryOverride = CreateTestClient;
            options.PromptClientFactoryOverride = CreateTestClient;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var hostContext = scope.ServiceProvider.GetRequiredService<IMcpHostContext>();
        var sessionClient = scope.ServiceProvider.GetRequiredService<McpServerClient>();

        Assert.Equal(@"E:\repo", hostContext.ActiveWorkspacePath);
        Assert.Same(sessionClient, await hostContext.GetRequiredControlApiClientAsync());
        Assert.Same(sessionClient, await hostContext.GetRequiredActiveWorkspaceApiClientAsync());

        hostContext.TrySetActiveWorkspace(@"E:\repo-two");

        Assert.Equal(@"E:\repo-two", workspaceContext.ActiveWorkspacePath);
        Assert.Equal(@"E:\repo-two", sessionClient.WorkspacePath);
    }

    [Fact]
    public async Task AddMcpHost_BootstrapMode_UsesHostIdentityProvider_ForDynamicCredentials()
    {
        var services = new ServiceCollection();
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = @"E:\repo" };
        var identityProvider = new AvaloniaHostIdentityProvider(
            apiKey: "initial-key",
            bearerToken: null,
            resolveWorkspacePath: () => workspaceContext.ActiveWorkspacePath);

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Scoped;
            options.McpBaseUrl = new Uri("http://localhost:7147");
            options.WorkspaceContext = workspaceContext;
            options.HostIdentityProvider = identityProvider;
            options.ClientFactoryOverride = CreateTestClient;
            options.PromptClientFactoryOverride = CreateTestClient;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        identityProvider.UpdateApiKey("refreshed-key");
        identityProvider.UpdateBearerToken("bearer-token");
        identityProvider.UpdateWorkspacePathResolver(() => @"E:\repo-two");

        var hostContext = (AvaloniaMcpContext)scope.ServiceProvider.GetRequiredService<IMcpHostContext>();
        var voiceService = scope.ServiceProvider.GetRequiredService<McpVoiceConversationService>();
        var eventStreamService = scope.ServiceProvider.GetRequiredService<McpAgentEventStreamService>();
        var client = await hostContext.GetRequiredActiveWorkspaceApiClientAsync();

        Assert.Equal("bearer-token", client.BearerToken);
        Assert.Equal(string.Empty, client.ApiKey);
        Assert.Equal(@"E:\repo-two", client.WorkspacePath);
        Assert.Equal("refreshed-key", voiceService.ResolveApiKey?.Invoke());
        Assert.Equal("bearer-token", voiceService.ResolveBearerToken?.Invoke());
        Assert.Equal(@"E:\repo-two", eventStreamService.ResolveWorkspacePath?.Invoke());
    }

    private static McpServerClient CreateTestClient()
    {
        return McpServerClientFactory.Create(new McpServerClientOptions
        {
            BaseUrl = new Uri("http://localhost:7147"),
            ApiKey = "test-api-key",
            Timeout = TimeSpan.FromSeconds(1),
            WorkspacePath = @"E:\repo",
        });
    }
}
