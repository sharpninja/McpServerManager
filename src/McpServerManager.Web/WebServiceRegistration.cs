using McpServer.Cqrs;
using McpServerManager.UI.Core;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Commands;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.Services;
using McpServerManager.Web.Adapters;
using McpServerManager.Web.Authorization;
using McpServerManager.Web.Services;
using McpServerManager.Web.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace McpServerManager.Web;

internal static class WebServiceRegistration
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        // IHttpContextAccessor required by WebRoleContext and BearerTokenAccessor.
        services.AddHttpContextAccessor();

        // Register WebRoleContext BEFORE AddUiCore so it wins over the AllowAllRoleContext TryAdd fallback.
        services.TryAddSingleton<IRoleContext, WebRoleContext>();

        // BearerTokenAccessor reads the OIDC access_token from the current HttpContext for API forwarding.
        services.AddScoped<BearerTokenAccessor>();
        services.TryAddScoped<WebCommandTarget>();

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Scoped;
            options.AdditionalHandlerAssemblies = [typeof(WebServiceRegistration).Assembly];
            options.CommandTargetFactory = static sp => sp.GetRequiredService<WebCommandTarget>();
            options.HostIdentityProviderFactory = static sp => ActivatorUtilities.CreateInstance<WebHostIdentityProvider>(sp);
            options.HealthClientFactory = static sp => ActivatorUtilities.CreateInstance<HealthApiClientAdapter>(sp);
            options.SessionLogClientFactory = static sp => ActivatorUtilities.CreateInstance<SessionLogApiClientAdapter>(sp);
            options.WorkspaceClientFactory = static sp => ActivatorUtilities.CreateInstance<WorkspaceApiClientAdapter>(sp);
            options.TodoClientFactory = static sp => ActivatorUtilities.CreateInstance<TodoApiClientAdapter>(sp);
            options.VoiceClientFactory = static sp => ActivatorUtilities.CreateInstance<VoiceApiClientAdapter>(sp);
            options.EventStreamClientFactory = static sp => ActivatorUtilities.CreateInstance<EventStreamApiClientAdapter>(sp);
        });

        services.RemoveAll<IUiDispatcherService>();
        services.AddScoped<IUiDispatcherService, BlazorUiDispatcherService>();

        services.RemoveAll<WorkspaceAutoSelector>();
        services.AddScoped<WorkspaceAutoSelector>();

        services.RemoveAll<BackendConnectionMonitor>();
        services.AddScoped<BackendConnectionMonitor>();

        services.AddScoped<WebMcpContext>();
        services.AddScoped<IMcpHostContext>(sp => sp.GetRequiredService<WebMcpContext>());
        services.AddScoped<ITemplateApiClient, TemplateApiClientAdapter>();
        services.AddScoped<IContextApiClient, ContextApiClientAdapter>();
        services.AddScoped<IRepoApiClient, RepoApiClientAdapter>();
        services.AddScoped<IVoiceApiClient, VoiceApiClientAdapter>();
        services.RemoveAll<IVoiceConversationService>();
        services.AddScoped<IVoiceConversationService, WebVoiceConversationService>();
        services.AddScoped<IAuthConfigApiClient, AuthConfigApiClientAdapter>();
        services.AddScoped<IConfigurationApiClient, ConfigurationApiClientAdapter>();
        services.AddScoped<IAgentApiClient, AgentApiClientAdapter>();
        services.AddScoped<ISseSubscriptionService, SseSubscriptionService>();
        services.AddScoped<WebVoiceConversationViewModel>();

        return services;
    }
}
