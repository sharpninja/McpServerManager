using System;
using System.Reflection;
using McpServer.Client;
using McpServer.Cqrs;
using McpServer.UI.Core.Auth;
using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.Services.Infrastructure;
using McpServer.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.UI.Core.Hosting;

public static class McpHostBuilderExtensions
{
    public static IServiceCollection AddMcpHost(this IServiceCollection services, Action<McpHostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new McpHostOptions();
        configure(options);

        services.AddSingleton(sp => AppLogService.Instance.ConfigureProviders(sp.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>()));
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(sp => sp.GetRequiredService<AppLogService>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(AppLogger<>));
        services.AddSingleton<McpServiceFactory>();

        RegisterHostIdentityProvider(services, options);
        RegisterApiClients(services, options);
        RegisterInfrastructureServices(services, options);
        RegisterCommandTarget(services, options);

        if (HasConnectionBootstrap(options))
        {
            RegisterForLifetime<IMcpHostContext, AvaloniaMcpContext>(services, options.Lifetime, static sp =>
                new AvaloniaMcpContext(
                    sp.GetRequiredService<McpServerClient>(),
                    sp.GetRequiredService<WorkspaceContextViewModel>(),
                    sp.GetService<IHostIdentityProvider>()));
        }

        services.AddDispatcherForHostLifetime(options.Lifetime);
        if (options.Lifetime == McpHostLifetimeStrategy.Singleton)
            services.AddCqrsLoggerProvider();

        if (options.AdditionalHandlerAssemblies is { Length: > 0 })
            services.AddCqrsHandlers(options.AdditionalHandlerAssemblies);

        services.AddUiCore(options.AdditionalHandlerAssemblies ?? Array.Empty<Assembly>());

        if (options.WorkspaceContext is not null)
            services.AddSingleton(options.WorkspaceContext);

        return services;
    }

    private static void RegisterApiClients(IServiceCollection services, McpHostOptions options)
    {
        if (HasExplicitApiClients(options))
        {
            RegisterForLifetime<ITodoApiClient>(services, options.Lifetime, options.TodoClient, options.TodoClientFactory, static (sp, client) => client);
            RegisterForLifetime<IWorkspaceApiClient>(services, options.Lifetime, options.WorkspaceClient, options.WorkspaceClientFactory, static (sp, client) => client);
            RegisterForLifetime<IVoiceApiClient>(services, options.Lifetime, options.VoiceClient, options.VoiceClientFactory, static (sp, client) => client);
            RegisterForLifetime<ISessionLogApiClient>(services, options.Lifetime, options.SessionLogClient, options.SessionLogClientFactory, static (sp, client) => client);
            RegisterForLifetime<IEventStreamApiClient>(services, options.Lifetime, options.EventStreamClient, options.EventStreamClientFactory, static (sp, client) => client);
            RegisterForLifetime<IHealthApiClient>(services, options.Lifetime, options.HealthClient, options.HealthClientFactory, static (sp, client) => client);
            return;
        }

        if (!HasConnectionBootstrap(options))
            return;

        var normalizedBaseUrl = options.McpBaseUrl is null
            ? throw new InvalidOperationException("McpBaseUrl is required when bootstrapping MCP services.")
            : McpServerRestClientFactory.NormalizeBaseUrl(options.McpBaseUrl.ToString());

        RegisterForLifetime<McpServerClient>(services, options.Lifetime, sp =>
            CreateSessionClient(options, sp.GetRequiredService<IHostIdentityProvider>(), promptClient: false));
        RegisterForLifetime<PromptMcpServerClient>(services, options.Lifetime, sp =>
            new PromptMcpServerClient(CreateSessionClient(options, sp.GetRequiredService<IHostIdentityProvider>(), promptClient: true)));

        RegisterForLifetime<McpSessionLogService>(services, options.Lifetime, static sp =>
        {
            var client = sp.GetRequiredService<McpServerClient>();
            return new McpSessionLogService(client);
        });

        RegisterForLifetime<McpTodoService>(services, options.Lifetime, static sp =>
        {
            var client = sp.GetRequiredService<McpServerClient>();
            var promptClient = sp.GetRequiredService<PromptMcpServerClient>().Client;
            return new McpTodoService(client, promptClient);
        });

        RegisterForLifetime<McpWorkspaceService>(services, options.Lifetime, sp =>
        {
            var client = sp.GetRequiredService<McpServerClient>();
            return new McpWorkspaceService(client, new Uri(normalizedBaseUrl, UriKind.Absolute));
        });

        RegisterForLifetime<McpVoiceConversationService>(services, options.Lifetime, sp =>
        {
            var identityProvider = sp.GetRequiredService<IHostIdentityProvider>();
            var service = new McpVoiceConversationService(normalizedBaseUrl, identityProvider.GetApiKey(), identityProvider.GetBearerToken())
            {
                ResolveBaseUrl = () => normalizedBaseUrl,
                ResolveApiKey = identityProvider.GetApiKey,
                ResolveBearerToken = identityProvider.GetBearerToken,
                ResolveWorkspacePath = identityProvider.GetWorkspacePath,
                WorkspacePath = identityProvider.GetWorkspacePath(),
            };
            return service;
        });

        RegisterForLifetime<McpAgentEventStreamService>(services, options.Lifetime, sp =>
        {
            var identityProvider = sp.GetRequiredService<IHostIdentityProvider>();
            return AgentEventStreamFactory.Create(
                normalizedBaseUrl,
                apiKey: identityProvider.GetApiKey(),
                bearerToken: identityProvider.GetBearerToken(),
                resolveBaseUrl: () => normalizedBaseUrl,
                resolveBearerToken: identityProvider.GetBearerToken,
                resolveApiKey: identityProvider.GetApiKey,
                resolveWorkspacePath: identityProvider.GetWorkspacePath);
        });

        RegisterForLifetime<UiCoreSessionLogApiClientAdapter>(services, options.Lifetime, static sp => new UiCoreSessionLogApiClientAdapter(sp.GetRequiredService<McpSessionLogService>()));
        RegisterForLifetime<UiCoreTodoApiClientAdapter>(services, options.Lifetime, static sp => new UiCoreTodoApiClientAdapter(sp.GetRequiredService<McpTodoService>()));
        RegisterForLifetime<UiCoreWorkspaceApiClientAdapter>(services, options.Lifetime, static sp => new UiCoreWorkspaceApiClientAdapter(sp.GetRequiredService<McpWorkspaceService>()));
        RegisterForLifetime<UiCoreVoiceApiClientAdapter>(services, options.Lifetime, static sp => new UiCoreVoiceApiClientAdapter(sp.GetRequiredService<McpVoiceConversationService>()));
        RegisterForLifetime<UiCoreEventStreamApiClientAdapter>(services, options.Lifetime, static sp => new UiCoreEventStreamApiClientAdapter(sp.GetRequiredService<McpAgentEventStreamService>()));
        RegisterForLifetime<IVoiceConversationService, McpVoiceConversationService>(services, options.Lifetime, static sp => sp.GetRequiredService<McpVoiceConversationService>());
        RegisterForLifetime<ITodoApiClient, UiCoreTodoApiClientAdapter>(services, options.Lifetime, static sp => sp.GetRequiredService<UiCoreTodoApiClientAdapter>());
        RegisterForLifetime<IWorkspaceApiClient, UiCoreWorkspaceApiClientAdapter>(services, options.Lifetime, static sp => sp.GetRequiredService<UiCoreWorkspaceApiClientAdapter>());
        RegisterForLifetime<ISessionLogApiClient, UiCoreSessionLogApiClientAdapter>(services, options.Lifetime, static sp => sp.GetRequiredService<UiCoreSessionLogApiClientAdapter>());
        RegisterForLifetime<IVoiceApiClient, UiCoreVoiceApiClientAdapter>(services, options.Lifetime, static sp => sp.GetRequiredService<UiCoreVoiceApiClientAdapter>());
        RegisterForLifetime<IEventStreamApiClient, UiCoreEventStreamApiClientAdapter>(services, options.Lifetime, static sp => sp.GetRequiredService<UiCoreEventStreamApiClientAdapter>());
    }

    private static void RegisterInfrastructureServices(IServiceCollection services, McpHostOptions options)
    {
        RegisterForLifetime<IFileSystemService>(services, options.Lifetime, options.FileSystemService, static (sp, service) => service, static () => new FileSystemService());
        RegisterForLifetime<IProcessLauncherService>(services, options.Lifetime, options.ProcessLauncherService, static (sp, service) => service, static () => new ProcessLauncherService());
        RegisterForLifetime<ITimerService>(services, options.Lifetime, options.TimerService, static (sp, service) => service, static () => new NoOpTimerService());
        RegisterForLifetime<IJsonParsingService>(services, options.Lifetime, options.JsonParsingService, static (sp, service) => service, static () => new JsonParsingService());
        RegisterForLifetime<IFileSystemWatcherService>(services, options.Lifetime, options.FileSystemWatcherService, static (sp, service) => service, static () => new FileSystemWatcherService());
        RegisterForLifetime<IClipboardService>(services, options.Lifetime, options.ClipboardService, static (sp, service) => service, static () => new NoOpClipboardService());
        RegisterForLifetime<IAppLogService>(services, options.Lifetime, static sp => sp.GetRequiredService<AppLogService>());
        RegisterForLifetime<ISpeechFilterService>(services, options.Lifetime, options.SpeechFilterService, static (sp, service) => service, static () => new NoOpSpeechFilterService());
        RegisterForLifetime<IUiDispatcherService>(services, options.Lifetime, options.UiDispatcherService, static (sp, service) => service, static () => new ImmediateUiDispatcherService());
        RegisterForLifetime<IConnectionAuthService>(services, options.Lifetime, options.ConnectionAuthService, static (sp, service) => service, static () => new NoOpConnectionAuthService());
    }

    private static void RegisterHostIdentityProvider(IServiceCollection services, McpHostOptions options)
    {
        Func<IHostIdentityProvider>? defaultFactory = HasConnectionBootstrap(options)
            ? () => new BootstrapHostIdentityProvider(options.ApiKey, options.BearerToken, options.ResolveWorkspacePath)
            : null;

        RegisterForLifetime<IHostIdentityProvider>(
            services,
            options.Lifetime,
            options.HostIdentityProvider,
            options.HostIdentityProviderFactory,
            static (sp, provider) => provider,
            defaultFactory);
    }

    private static void RegisterCommandTarget(IServiceCollection services, McpHostOptions options)
    {
        if (options.CommandTarget is null && options.CommandTargetFactory is null)
            return;

        RegisterForLifetime<ICommandTarget>(services, options.Lifetime, options.CommandTarget, options.CommandTargetFactory, static (sp, target) => target);
        RegisterForLifetime<INavigationTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<IRequestDetailsTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<IPreviewTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<IArchiveTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<ISessionDataTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<IClipboardTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<IConfigTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<IUiDispatchTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
        RegisterForLifetime<ITodoCopilotTarget, ICommandTarget>(services, options.Lifetime, static sp => sp.GetRequiredService<ICommandTarget>());
    }

    private static bool HasExplicitApiClients(McpHostOptions options)
        => options.TodoClient is not null
        || options.TodoClientFactory is not null
        || options.WorkspaceClient is not null
        || options.WorkspaceClientFactory is not null
        || options.VoiceClient is not null
        || options.VoiceClientFactory is not null
        || options.SessionLogClient is not null
        || options.SessionLogClientFactory is not null
        || options.EventStreamClient is not null
        || options.EventStreamClientFactory is not null
        || options.HealthClient is not null
        || options.HealthClientFactory is not null;

    private static bool HasConnectionBootstrap(McpHostOptions options)
        => options.McpBaseUrl is not null || options.ClientFactoryOverride is not null || options.PromptClientFactoryOverride is not null;

    private static McpServerClient CreateSessionClient(McpHostOptions options, IHostIdentityProvider identityProvider, bool promptClient)
    {
        var factory = promptClient ? options.PromptClientFactoryOverride : options.ClientFactoryOverride;
        if (factory is not null)
            return factory();

        var baseUrl = options.McpBaseUrl is null
            ? throw new InvalidOperationException("McpBaseUrl is required when bootstrapping MCP services.")
            : options.McpBaseUrl.ToString();

        var timeout = promptClient ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(300);
        return McpServerRestClientFactory.Create(
            baseUrl,
            timeout,
            apiKey: identityProvider.GetApiKey(),
            workspaceRootPath: identityProvider.GetWorkspacePath(),
            bearerToken: identityProvider.GetBearerToken());
    }

    private static void RegisterForLifetime<TService>(
        IServiceCollection services,
        McpHostLifetimeStrategy lifetime,
        Func<IServiceProvider, TService> factory)
        where TService : class
    {
        switch (lifetime)
        {
            case McpHostLifetimeStrategy.Singleton:
                services.AddSingleton(factory);
                break;
            case McpHostLifetimeStrategy.Scoped:
                services.AddScoped(factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static void RegisterForLifetime<TService, TImplementation>(
        IServiceCollection services,
        McpHostLifetimeStrategy lifetime,
        Func<IServiceProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService
    {
        switch (lifetime)
        {
            case McpHostLifetimeStrategy.Singleton:
                services.AddSingleton<TService>(factory);
                break;
            case McpHostLifetimeStrategy.Scoped:
                services.AddScoped<TService>(factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static void RegisterForLifetime<TService, TState>(
        IServiceCollection services,
        McpHostLifetimeStrategy lifetime,
        Func<IServiceProvider, TState, TService> factory,
        TState state)
        where TService : class
    {
        switch (lifetime)
        {
            case McpHostLifetimeStrategy.Singleton:
                services.AddSingleton(sp => factory(sp, state));
                break;
            case McpHostLifetimeStrategy.Scoped:
                services.AddScoped(sp => factory(sp, state));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static void RegisterForLifetime<TService>(
        IServiceCollection services,
        McpHostLifetimeStrategy lifetime,
        TService? configuredInstance,
        Func<IServiceProvider, TService>? configuredRegistrationFactory,
        Func<IServiceProvider, TService, TService> configuredInstanceFactory,
        Func<TService>? defaultFactory = null)
        where TService : class
    {
        if (configuredRegistrationFactory is not null)
        {
            RegisterForLifetime(services, lifetime, configuredRegistrationFactory);
            return;
        }

        if (configuredInstance is not null)
        {
            RegisterForLifetime(services, lifetime, static (sp, state) => state.Factory(sp, state.Instance), new ConfiguredRegistration<TService>(configuredInstance, configuredInstanceFactory));
            return;
        }

        if (defaultFactory is null)
            return;

        RegisterForLifetime(services, lifetime, static (sp, factory) => factory(), defaultFactory);
    }

    private static void RegisterForLifetime<TService>(
        IServiceCollection services,
        McpHostLifetimeStrategy lifetime,
        TService? configuredInstance,
        Func<IServiceProvider, TService, TService> configuredInstanceFactory,
        Func<TService>? defaultFactory = null)
        where TService : class
        => RegisterForLifetime(
            services,
            lifetime,
            configuredInstance,
            configuredRegistrationFactory: null,
            configuredInstanceFactory,
            defaultFactory);

    private sealed record ConfiguredRegistration<TService>(TService Instance, Func<IServiceProvider, TService, TService> Factory)
        where TService : class;

    private sealed class PromptMcpServerClient
    {
        public PromptMcpServerClient(McpServerClient client)
        {
            Client = client;
        }

        public McpServerClient Client { get; }
    }

    private sealed class BootstrapHostIdentityProvider : IHostIdentityProvider
    {
        private readonly string? _apiKey;
        private readonly string? _bearerToken;
        private readonly Func<string?>? _resolveWorkspacePath;

        public BootstrapHostIdentityProvider(string? apiKey, string? bearerToken, Func<string?>? resolveWorkspacePath)
        {
            _apiKey = apiKey;
            _bearerToken = bearerToken;
            _resolveWorkspacePath = resolveWorkspacePath;
        }

        public string? GetApiKey() => _apiKey;

        public string? GetBearerToken() => _bearerToken;

        public string? GetWorkspacePath() => _resolveWorkspacePath?.Invoke();
    }
}
