using System;
using McpServer.Cqrs;
using Avalonia.Controls.ApplicationLifetimes;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.Core.Commands;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CoreConnectionAuthServiceAdapter = McpServerManager.Core.Services.ConnectionAuthServiceAdapter;
using CoreConnectionViewModel = McpServerManager.Core.ViewModels.ConnectionViewModel;
using CoreMainWindowViewModel = McpServerManager.Core.ViewModels.MainWindowViewModel;
using CoreSpeechFilterServiceAdapter = McpServerManager.Core.Services.SpeechFilterServiceAdapter;

namespace McpServerManager.Desktop.Services;

internal static class DesktopAppServiceFactory
{
    public static ServiceProvider BuildConnectionProvider(McpServerManager.UI.Core.Services.IUiDispatcherService uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);

        var services = new ServiceCollection();
        AddCoreLogging(services);
        services.AddSingleton(uiDispatcher);
        services.AddCqrsDispatcher();
        services.AddCqrsLoggerProvider();
        services.AddCqrsHandlers(typeof(NavigateBackCommand).Assembly);
        services.AddCqrsHandlers(typeof(McpServerManager.UI.Core.Commands.InvokeUiActionHandler).Assembly);
        services.AddSingleton<McpServerManager.UI.Core.Services.IConnectionAuthService>(_ => new CoreConnectionAuthServiceAdapter());
        services.AddSingleton<CoreConnectionViewModel>();
        return services.BuildServiceProvider();
    }

    public static DesktopMainWindowSession CreateMainWindowSession(
        IClassicDesktopStyleApplicationLifetime desktop,
        McpServerManager.UI.Core.Services.IUiDispatcherService uiDispatcher,
        string mcpBaseUrl,
        string? mcpApiKey,
        string? bearerToken)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(uiDispatcher);

        var services = new ServiceCollection();
        var clipboardService = new DesktopClipboardService(desktop);
        var workspaceContext = new WorkspaceContextViewModel();
        var hostIdentityProvider = new AvaloniaHostIdentityProvider(
            mcpApiKey,
            bearerToken,
            () => workspaceContext.ActiveWorkspacePath);
        var commandTargetAccessor = new DeferredCommandTargetAccessor();

        services.AddMcpHost(options =>
        {
            options.Lifetime = McpHostLifetimeStrategy.Singleton;
            options.McpBaseUrl = new Uri(mcpBaseUrl, UriKind.Absolute);
            options.ApiKey = mcpApiKey;
            options.BearerToken = bearerToken;
            options.ClipboardService = clipboardService;
            options.UiDispatcherService = uiDispatcher;
            options.ConnectionAuthService = new CoreConnectionAuthServiceAdapter();
            options.SpeechFilterService = new CoreSpeechFilterServiceAdapter();
            options.WorkspaceContext = workspaceContext;
            options.HostIdentityProvider = hostIdentityProvider;
            options.AdditionalHandlerAssemblies = new[] { typeof(NavigateBackCommand).Assembly };
        });

        services.AddSingleton(commandTargetAccessor);
        services.AddSingleton<McpServerManager.Core.Services.IClipboardService>(clipboardService);
        services.AddSingleton<McpServerManager.UI.Core.Services.IClipboardService>(sp => sp.GetRequiredService<McpServerManager.Core.Services.IClipboardService>());
        services.AddSingleton<McpServerManager.Core.Services.ISystemNotificationService, DesktopSystemNotificationService>();
        RegisterChatServices(services);
        RegisterUiCoreCommandTargets(services);
        RegisterCoreCommandTargets(services);

        var provider = services.BuildServiceProvider();
        try
        {
            var runtime = new UiCoreHostRuntime(
                provider,
                provider.GetRequiredService<WorkspaceContextViewModel>());

            var hostServices = new MainWindowHostServices(
                mcpBaseUrl,
                mcpApiKey,
                bearerToken,
                hostIdentityProvider,
                provider.GetRequiredService<IMcpHostContext>(),
                provider.GetRequiredService<McpServer.Client.McpServerClient>(),
                provider.GetRequiredService<McpTodoService>(),
                provider.GetRequiredService<McpWorkspaceService>(),
                provider.GetRequiredService<McpVoiceConversationService>(),
                provider.GetRequiredService<McpSessionLogService>(),
                provider.GetRequiredService<McpAgentEventStreamService>(),
                runtime);

            var viewModel = ActivatorUtilities.CreateInstance<CoreMainWindowViewModel>(provider, hostServices);
            commandTargetAccessor.Attach(viewModel);
            return new DesktopMainWindowSession(provider, viewModel);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    private static void AddCoreLogging(IServiceCollection services)
    {
        services.AddSingleton(sp => AppLogService.Instance.ConfigureProviders(sp.GetServices<ILoggerProvider>()));
        services.AddSingleton<ILoggerFactory>(sp => sp.GetRequiredService<AppLogService>());
        services.AddSingleton(typeof(ILogger<>), typeof(AppLogger<>));
    }

    private static void RegisterUiCoreCommandTargets(IServiceCollection services)
    {
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.ICommandTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.INavigationTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.IRequestDetailsTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.IPreviewTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.IArchiveTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.ISessionDataTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.IClipboardTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.IConfigTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.IUiDispatchTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
        services.TryAddSingleton<McpServerManager.UI.Core.Commands.ITodoCopilotTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireUiCoreTarget());
    }

    private static void RegisterCoreCommandTargets(IServiceCollection services)
    {
        services.TryAddSingleton<McpServerManager.Core.Commands.ICommandTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.INavigationTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.IRequestDetailsTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.IPreviewTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.IArchiveTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.ISessionDataTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.IClipboardTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.IConfigTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.IUiDispatchTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
        services.TryAddSingleton<McpServerManager.Core.Commands.ITodoCopilotTarget>(sp => sp.GetRequiredService<DeferredCommandTargetAccessor>().RequireCoreTarget());
    }

    private static void RegisterChatServices(IServiceCollection services)
    {
        services.AddSingleton<McpServerManager.Core.Services.ILogAgentService, McpServerManager.Core.Services.OllamaLogAgentService>();
        services.AddSingleton<McpServerManager.Core.Services.IChatPromptTemplateService, McpServerManager.Core.Services.LocalChatPromptTemplateService>();
        services.AddSingleton<McpServerManager.Core.Services.IChatConfigFilesService, McpServerManager.Core.Services.LocalChatConfigFilesService>();
        services.AddSingleton<McpServerManager.Core.Services.IChatModelDiscoveryService, McpServerManager.Core.Services.OllamaChatModelDiscoveryService>();
        services.AddSingleton<McpServerManager.Core.Services.IChatSendOrchestrationService>(
            sp => new McpServerManager.Core.Services.LogAgentChatSendOrchestrationService(
                sp.GetRequiredService<McpServerManager.Core.Services.ILogAgentService>()));
        services.AddSingleton<McpServerManager.Core.Services.IChatWindowViewModelFactory, McpServerManager.Core.Services.ChatWindowViewModelFactory>();
    }
}

internal sealed class DeferredCommandTargetAccessor
{
    private readonly object _sync = new();
    private CoreMainWindowViewModel? _target;

    public void Attach(CoreMainWindowViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        lock (_sync)
        {
            _target = target;
        }
    }

    public McpServerManager.UI.Core.Commands.ICommandTarget RequireUiCoreTarget()
    {
        lock (_sync)
        {
            return _target ?? throw new InvalidOperationException("The Desktop main-window command target has not been attached yet.");
        }
    }

    public McpServerManager.Core.Commands.ICommandTarget RequireCoreTarget()
    {
        lock (_sync)
        {
            return _target ?? throw new InvalidOperationException("The Desktop main-window command target has not been attached yet.");
        }
    }
}

internal sealed class DesktopMainWindowSession : IDisposable
{
    public DesktopMainWindowSession(ServiceProvider services, CoreMainWindowViewModel viewModel)
    {
        Services = services;
        ViewModel = viewModel;
    }

    public ServiceProvider Services { get; }

    public CoreMainWindowViewModel ViewModel { get; }

    public void Dispose() => Services.Dispose();
}
