using System;
using System.Reflection;
using McpServer.Client;
using McpServer.UI.Core.Auth;
using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Services;
using McpServer.UI.Core.ViewModels;

namespace McpServer.UI.Core.Hosting;

public sealed class McpHostOptions
{
    public McpHostLifetimeStrategy Lifetime { get; set; } = McpHostLifetimeStrategy.Singleton;

    public WorkspaceContextViewModel? WorkspaceContext { get; set; }

    public Assembly[]? AdditionalHandlerAssemblies { get; set; }

    public ICommandTarget? CommandTarget { get; set; }

    public Func<IServiceProvider, ICommandTarget>? CommandTargetFactory { get; set; }

    public IHostIdentityProvider? HostIdentityProvider { get; set; }

    public Func<IServiceProvider, IHostIdentityProvider>? HostIdentityProviderFactory { get; set; }

    public ITodoApiClient? TodoClient { get; set; }

    public Func<IServiceProvider, ITodoApiClient>? TodoClientFactory { get; set; }

    public IWorkspaceApiClient? WorkspaceClient { get; set; }

    public Func<IServiceProvider, IWorkspaceApiClient>? WorkspaceClientFactory { get; set; }

    public IVoiceApiClient? VoiceClient { get; set; }

    public Func<IServiceProvider, IVoiceApiClient>? VoiceClientFactory { get; set; }

    public ISessionLogApiClient? SessionLogClient { get; set; }

    public Func<IServiceProvider, ISessionLogApiClient>? SessionLogClientFactory { get; set; }

    public IEventStreamApiClient? EventStreamClient { get; set; }

    public Func<IServiceProvider, IEventStreamApiClient>? EventStreamClientFactory { get; set; }

    public IHealthApiClient? HealthClient { get; set; }

    public Func<IServiceProvider, IHealthApiClient>? HealthClientFactory { get; set; }

    public Uri? McpBaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string? BearerToken { get; set; }

    public Func<string?>? ResolveWorkspacePath { get; set; }

    public Func<McpServerClient>? ClientFactoryOverride { get; set; }

    public Func<McpServerClient>? PromptClientFactoryOverride { get; set; }

    public IFileSystemService? FileSystemService { get; set; }

    public IProcessLauncherService? ProcessLauncherService { get; set; }

    public ITimerService? TimerService { get; set; }

    public IJsonParsingService? JsonParsingService { get; set; }

    public IFileSystemWatcherService? FileSystemWatcherService { get; set; }

    public IClipboardService? ClipboardService { get; set; }

    public IUiDispatcherService? UiDispatcherService { get; set; }

    public IConnectionAuthService? ConnectionAuthService { get; set; }

    public ISpeechFilterService? SpeechFilterService { get; set; }
}
