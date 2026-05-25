using McpServer.Client;
using McpServer.Cqrs;
using McpServerManager.Director.Auth;
using McpServerManager.Director.Helpers;
using McpServerManager.UI.Core;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Navigation;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Director;

/// <summary>
/// TR-MCP-DRY-001: Shared DI registration for all Director entry points.
/// The provider itself is now owned by <see cref="DirectorHost"/>.
/// </summary>
internal static class DirectorServiceRegistration
{
    /// <summary>
    /// Configures all Director services: logging, CQRS, UI Core, authorization, HTTP clients, and API adapters.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="workspace">Optional workspace path override.</param>
    /// <returns>
    /// The <see cref="DirectorMcpContext"/> created during registration so callers can
    /// layer entry-point-specific services on top of the shared Director graph before build.
    /// </returns>
    public static DirectorMcpContext Configure(IServiceCollection services, string? workspace = null)
    {
        var activeWorkspaceClient = McpHttpClient.FromMarkerOnly(workspace);
        activeWorkspaceClient?.TrySetCachedBearerToken();

        var controlClient = McpHttpClient.FromDefaultUrlOrMarker(workspace);
        controlClient?.TrySetCachedBearerToken();

        var directorContext = new DirectorMcpContext(controlClient, activeWorkspaceClient);

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Singleton;
            options.HostIdentityProvider = new DirectorHostIdentityProvider(directorContext);
            options.UiDispatcherService = new TuiUiDispatcherService();
            options.HealthClient = new HealthApiClientAdapter(directorContext.ControlClient);
            options.SessionLogClient = new SessionLogApiClientAdapter(directorContext);
            options.WorkspaceClient = new WorkspaceApiClientAdapter(directorContext);
            options.TodoClient = new TodoApiClientAdapter(directorContext);
            options.VoiceClient = new VoiceApiClientAdapter(directorContext);
            options.EventStreamClient = new EventStreamApiClientAdapter(directorContext);
        });

        services.AddDirectorLogging();
        services.AddSingleton<IBrowserLauncher, BrowserLauncher>();

        // Override default permissive auth with Director-specific implementations.
        services.RemoveAll<IRoleContext>();
        services.RemoveAll<IAuthorizationPolicyService>();
        services.AddSingleton<IRoleContext, DirectorRoleContext>();
        services.AddSingleton<IAuthorizationPolicyService, DirectorAuthorizationPolicyService>();
        services.AddSingleton<ITabRegistry, DirectorTabRegistry>();

        services.AddSingleton(directorContext);
        services.AddSingleton<IMcpHostContext>(directorContext);
        if (controlClient is not null)
            services.AddSingleton(controlClient);

        // Register Director-only API client adapters on top of the shared host graph.
        services.AddSingleton<IRepoApiClient>(_ => new RepoApiClientAdapter(directorContext));
        services.AddSingleton<IContextApiClient>(_ => new ContextApiClientAdapter(directorContext));
        services.AddSingleton<IAuthConfigApiClient>(_ => new AuthConfigApiClientAdapter(directorContext));
        services.AddSingleton<IDiagnosticApiClient>(_ => new DiagnosticApiClientAdapter(directorContext));
        services.AddSingleton<ITunnelApiClient>(_ => new TunnelApiClientAdapter(directorContext));
        services.AddSingleton<ITemplateApiClient>(_ => new TemplateApiClientAdapter(directorContext));
        services.AddSingleton<IAgentApiClient>(_ => new AgentApiClientAdapter(directorContext));
        services.AddSingleton<IAgentPoolApiClient>(_ => new AgentPoolApiClientAdapter(directorContext));
        services.AddSingleton<IToolRegistryApiClient>(_ => new ToolRegistryApiClientAdapter(directorContext));
        services.AddSingleton<IGitHubApiClient>(_ => new GitHubApiClientAdapter(directorContext));
        services.AddSingleton<IRequirementsApiClient>(_ => new RequirementsApiClientAdapter(directorContext));
        services.AddSingleton<IConfigurationApiClient>(_ => new ConfigurationApiClientAdapter(directorContext));

        return directorContext;
    }
}
