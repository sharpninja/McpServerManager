using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handler for OIDC logout via CQRS.
/// </summary>
internal sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, bool>
{
    private readonly IConnectionAuthService _authService;

    public LogoutCommandHandler(IConnectionAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<Result<bool>> HandleAsync(LogoutCommand command, CallContext context)
    {
        var success = await _authService.TryLogoutAsync(
            command.McpBaseUrl,
            command.Authority,
            command.ClientId,
            command.AccessToken,
            context.CancellationToken).ConfigureAwait(true);
        return Result<bool>.Success(success);
    }
}

/// <summary>
/// Handler for fetching MCP API key (with optional bearer) via CQRS.
/// </summary>
internal sealed class FetchMcpApiKeyCommandHandler : ICommandHandler<FetchMcpApiKeyCommand, ConnectionApiKeyFetchResult>
{
    private readonly IConnectionAuthService _authService;

    public FetchMcpApiKeyCommandHandler(IConnectionAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<Result<ConnectionApiKeyFetchResult>> HandleAsync(FetchMcpApiKeyCommand command, CallContext context)
    {
        var result = await _authService.TryFetchMcpApiKeyAsync(
            command.McpBaseUrl,
            command.OidcBearerToken,
            context.CancellationToken).ConfigureAwait(true);
        return Result<ConnectionApiKeyFetchResult>.Success(result);
    }
}

/// <summary>
/// Handler for starting device authorization via CQRS.
/// </summary>
internal sealed class StartDeviceAuthorizationCommandHandler : ICommandHandler<StartDeviceAuthorizationCommand, ConnectionDeviceAuthorizationPrompt>
{
    private readonly IConnectionAuthService _authService;

    public StartDeviceAuthorizationCommandHandler(IConnectionAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<Result<ConnectionDeviceAuthorizationPrompt>> HandleAsync(StartDeviceAuthorizationCommand command, CallContext context)
    {
        // Map snapshot to service config if needed; for now pass through adapter logic inside service
        var prompt = await _authService.StartDeviceAuthorizationAsync(
            ToConfig(command.AuthConfig),
            command.McpBaseUrl,
            context.CancellationToken).ConfigureAwait(true);
        return Result<ConnectionDeviceAuthorizationPrompt>.Success(prompt);
    }

    private static McpServerManager.UI.Core.Services.ConnectionAuthConfig ToConfig(AuthConfigSnapshot s) => new()
    {
        Enabled = s.Enabled,
        Authority = s.Authority,
        ClientId = s.ClientId,
        Scopes = s.Scopes,
        DeviceAuthorizationEndpoint = s.DeviceAuthorizationEndpoint,
        TokenEndpoint = s.TokenEndpoint
    };
}

/// <summary>
/// Handler for polling device token via CQRS.
/// </summary>
internal sealed class PollForAccessTokenCommandHandler : ICommandHandler<PollForAccessTokenCommand, ConnectionDeviceTokenResult>
{
    private readonly IConnectionAuthService _authService;

    public PollForAccessTokenCommandHandler(IConnectionAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<Result<ConnectionDeviceTokenResult>> HandleAsync(PollForAccessTokenCommand command, CallContext context)
    {
        var token = await _authService.PollForAccessTokenAsync(
            ToConfig(command.AuthConfig),
            command.Prompt,
            command.McpBaseUrl,
            null,
            context.CancellationToken).ConfigureAwait(true);
        return Result<ConnectionDeviceTokenResult>.Success(token);
    }

    private static McpServerManager.UI.Core.Services.ConnectionAuthConfig ToConfig(AuthConfigSnapshot s) => new()
    {
        Enabled = s.Enabled,
        Authority = s.Authority,
        ClientId = s.ClientId,
        Scopes = s.Scopes,
        DeviceAuthorizationEndpoint = s.DeviceAuthorizationEndpoint,
        TokenEndpoint = s.TokenEndpoint
    };
}

/// <summary>
/// Handler for health probe query via CQRS.
/// </summary>
internal sealed class ProbeHealthAndResolveUrlQueryHandler : IQueryHandler<ProbeHealthAndResolveUrlQuery, string>
{
    private readonly IConnectionAuthService _authService;

    public ProbeHealthAndResolveUrlQueryHandler(IConnectionAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<Result<string>> HandleAsync(ProbeHealthAndResolveUrlQuery query, CallContext context)
    {
        var url = await _authService.ProbeHealthAndResolveUrlAsync(query.Url, context.CancellationToken).ConfigureAwait(true);
        return Result<string>.Success(url);
    }
}
