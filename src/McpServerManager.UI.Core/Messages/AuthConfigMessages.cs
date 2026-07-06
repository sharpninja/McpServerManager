using McpServer.Cqrs;

namespace McpServerManager.UI.Core.Messages;

/// <summary>Query for <c>/auth/config</c>.</summary>
public sealed record GetAuthConfigQuery : IQuery<AuthConfigSnapshot>;

/// <summary>Public auth configuration snapshot.</summary>
public sealed record AuthConfigSnapshot(
    bool Enabled,
    string? Authority,
    string? ClientId,
    string? Scopes,
    string? DeviceAuthorizationEndpoint,
    string? TokenEndpoint,
    DateTimeOffset RetrievedAt);

/// <summary>
/// CQRS command to logout / revoke OIDC session.
/// </summary>
public sealed record LogoutCommand(
    string McpBaseUrl,
    string? Authority,
    string? ClientId,
    string? AccessToken) : ICommand<bool>;

/// <summary>
/// CQRS command to fetch MCP API key using optional bearer.
/// </summary>
public sealed record FetchMcpApiKeyCommand(
    string McpBaseUrl,
    string? OidcBearerToken) : ICommand<McpServerManager.UI.Core.Services.ConnectionApiKeyFetchResult>;

/// <summary>
/// CQRS command to start device authorization flow.
/// </summary>
public sealed record StartDeviceAuthorizationCommand(
    string McpBaseUrl,
    AuthConfigSnapshot AuthConfig) : ICommand<McpServerManager.UI.Core.Services.ConnectionDeviceAuthorizationPrompt>;

/// <summary>
/// CQRS command to poll for device access token.
/// </summary>
public sealed record PollForAccessTokenCommand(
    AuthConfigSnapshot AuthConfig,
    McpServerManager.UI.Core.Services.ConnectionDeviceAuthorizationPrompt Prompt,
    string McpBaseUrl) : ICommand<McpServerManager.UI.Core.Services.ConnectionDeviceTokenResult>;

/// <summary>
/// Query to probe health and resolve the effective MCP base URL.
/// </summary>
public sealed record ProbeHealthAndResolveUrlQuery(string Url) : IQuery<string>;
