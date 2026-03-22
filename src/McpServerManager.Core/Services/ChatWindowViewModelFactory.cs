using System;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Models;
using McpServerManager.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerManager.Core.Services;

public interface IChatWindowViewModelFactory
{
    ChatWindowViewModelSession Create(Func<string> getContext);
}

/// <summary>Compliant composition factory for chat ViewModels. Builds a DI container with chat handlers and Dispatcher.</summary>
public sealed class ChatWindowViewModelFactory : IChatWindowViewModelFactory, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceProvider? _ownedServiceProvider;
    private readonly Func<string?> _readSelectedModel;
    private readonly Action<string?> _writeSelectedModel;

    public ChatWindowViewModelFactory(
        IServiceProvider? serviceProvider = null,
        Func<ILogAgentService>? agentServiceFactory = null,
        Func<string?>? readSelectedModel = null,
        Action<string?>? writeSelectedModel = null)
    {
        _ownedServiceProvider = serviceProvider as ServiceProvider;
        if (serviceProvider is null)
        {
            _ownedServiceProvider = BuildProvider(agentServiceFactory?.Invoke() ?? new OllamaLogAgentService());
            _serviceProvider = _ownedServiceProvider;
        }
        else
        {
            _serviceProvider = serviceProvider;
        }

        _readSelectedModel = readSelectedModel ?? AgentConfigIo.GetModelFromConfig;
        _writeSelectedModel = writeSelectedModel ?? AgentConfigIo.SetModelInConfig;
    }

    public ChatWindowViewModelSession Create(Func<string> getContext)
    {
        ArgumentNullException.ThrowIfNull(getContext);

        var scope = _serviceProvider.CreateScope();
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<McpServer.Cqrs.Dispatcher>();
            var viewModel = new ChatWindowViewModel(
            dispatcher,
            getContext,
            _readSelectedModel(),
            _writeSelectedModel);

            return new ChatWindowViewModelSession(scope, viewModel);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public void Dispose() => _ownedServiceProvider?.Dispose();

    public static McpServer.Cqrs.Dispatcher CreateFallbackDispatcher(ILogAgentService agentService)
        => BuildProvider(agentService).GetRequiredService<McpServer.Cqrs.Dispatcher>();

    private static ServiceProvider BuildProvider(ILogAgentService agentService)
    {
        if (agentService == null) throw new ArgumentNullException(nameof(agentService));

        var services = new ServiceCollection();
        services.AddSingleton(sp => AppLogService.Instance.ConfigureProviders(sp.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>()));
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(sp => sp.GetRequiredService<AppLogService>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(AppLogger<>));

        // Register chat-specific services for handler DI
        services.AddSingleton<IChatPromptTemplateService>(new LocalChatPromptTemplateService());
        services.AddSingleton<IChatConfigFilesService>(new LocalChatConfigFilesService());
        services.AddSingleton<IChatModelDiscoveryService>(new OllamaChatModelDiscoveryService());
        services.AddSingleton<IChatSendOrchestrationService>(new LogAgentChatSendOrchestrationService(agentService));
            services.AddSingleton<McpServer.UI.Core.Services.IUiDispatcherService, McpServer.UI.Core.Services.ImmediateUiDispatcherService>();

        services.AddCqrsDispatcher();
        services.AddCqrsLoggerProvider();
        services.AddCqrsHandlers(typeof(ChatOpenAgentConfigCommand).Assembly);
        services.AddCqrsHandlers(typeof(McpServer.UI.Core.Commands.InvokeUiActionHandler).Assembly);

        return services.BuildServiceProvider();
    }
}

public sealed class ChatWindowViewModelSession : IDisposable
{
    private readonly IServiceScope _scope;

    public ChatWindowViewModelSession(IServiceScope scope, ChatWindowViewModel viewModel)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public ChatWindowViewModel ViewModel { get; }

    public void Dispose() => _scope.Dispose();
}
