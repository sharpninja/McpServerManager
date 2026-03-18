using McpServer.Cqrs;
using McpServer.UI.Core;
using McpServer.UI.Core.Authorization;
using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Services;
using McpServer.Web.Adapters;
using McpServer.Web.Authorization;
using McpServer.Web.Services;
using McpServer.Web.ViewModels;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace McpServer.Web;

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

        // Avoid registering Dispatcher as an ILoggerProvider (AddCqrs) to prevent startup DI/logger cycles.
        // Web command dispatch needs the current scope so scoped CQRS behaviors can resolve correctly.
        services.AddScoped<Dispatcher>();
        services.AddUiCore(typeof(WebServiceRegistration).Assembly);

        services.TryAddScoped<WebCommandTarget>();
        services.TryAddScoped<ICommandTarget>(sp => sp.GetRequiredService<WebCommandTarget>());
        services.TryAddScoped<INavigationTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<IRequestDetailsTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<IPreviewTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<IArchiveTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<ISessionDataTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<IClipboardTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<IConfigTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<IUiDispatchTarget>(sp => sp.GetRequiredService<ICommandTarget>());
        services.TryAddScoped<ITodoCopilotTarget>(sp => sp.GetRequiredService<ICommandTarget>());

        services.RemoveAll<WorkspaceAutoSelector>();
        services.AddScoped<WorkspaceAutoSelector>();

        services.RemoveAll<BackendConnectionMonitor>();
        services.AddScoped<BackendConnectionMonitor>();

        services.AddScoped<WebMcpContext>();
        services.AddScoped<ITodoApiClient, TodoApiClientAdapter>();
        services.AddScoped<IWorkspaceApiClient, WorkspaceApiClientAdapter>();
        services.AddScoped<ISessionLogApiClient, SessionLogApiClientAdapter>();
        services.AddScoped<IHealthApiClient, HealthApiClientAdapter>();
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
