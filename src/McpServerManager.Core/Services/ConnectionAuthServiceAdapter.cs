using System;
using System.Threading;
using System.Threading.Tasks;
using McpServer.UI.Core.Services;

namespace McpServerManager.Core.Services;

/// <summary>
/// Adapts core OIDC/probe services to UI.Core connection auth abstractions.
/// </summary>
public sealed class ConnectionAuthServiceAdapter : IConnectionAuthService
{
    /// <inheritdoc />
    public Task<string> ProbeHealthAndResolveUrlAsync(string url, CancellationToken cancellationToken = default)
        => ConnectionProbeHelper.ProbeHealthAndResolveUrlAsync(url, cancellationToken);

    /// <inheritdoc />
    public async Task<ConnectionAuthConfig?> TryGetAuthConfigAsync(string mcpBaseUrl, CancellationToken cancellationToken = default)
    {
        var response = await McpOidcAuthService.TryGetAuthConfigAsync(mcpBaseUrl, cancellationToken).ConfigureAwait(true);
        return response == null
            ? null
            : new ConnectionAuthConfig
            {
                Enabled = response.Enabled,
                Authority = response.Authority,
                ClientId = response.ClientId,
                Scopes = response.Scopes,
                DeviceAuthorizationEndpoint = response.DeviceAuthorizationEndpoint,
                TokenEndpoint = response.TokenEndpoint
            };
    }

    /// <inheritdoc />
    public bool IsEnabled(ConnectionAuthConfig? config)
        => McpOidcAuthService.IsEnabled(ToCoreConfig(config));

    /// <inheritdoc />
    public Task<bool> TryLogoutAsync(
        string mcpBaseUrl,
        string? authority,
        string? clientId,
        string? accessToken,
        CancellationToken cancellationToken = default)
        => McpOidcAuthService.TryLogoutAsync(mcpBaseUrl, authority, clientId, accessToken, cancellationToken);

    /// <inheritdoc />
    public async Task<ConnectionDeviceAuthorizationPrompt> StartDeviceAuthorizationAsync(
        ConnectionAuthConfig authConfig,
        string mcpBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var prompt = await McpOidcAuthService.StartDeviceAuthorizationAsync(
                ToCoreConfig(authConfig)!,
                mcpBaseUrl,
                cancellationToken)
            .ConfigureAwait(true);
        return new ConnectionDeviceAuthorizationPrompt
        {
            DeviceCode = prompt.DeviceCode,
            UserCode = prompt.UserCode,
            VerificationUri = prompt.VerificationUri,
            VerificationUriComplete = prompt.VerificationUriComplete,
            ExpiresInSeconds = prompt.ExpiresInSeconds,
            PollIntervalSeconds = prompt.PollIntervalSeconds
        };
    }

    /// <inheritdoc />
    public async Task<ConnectionDeviceTokenResult> PollForAccessTokenAsync(
        ConnectionAuthConfig authConfig,
        ConnectionDeviceAuthorizationPrompt prompt,
        string mcpBaseUrl,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        var token = await McpOidcAuthService.PollForAccessTokenAsync(
                ToCoreConfig(authConfig)!,
                new OidcDeviceAuthorizationPrompt
                {
                    DeviceCode = prompt.DeviceCode,
                    UserCode = prompt.UserCode,
                    VerificationUri = prompt.VerificationUri,
                    VerificationUriComplete = prompt.VerificationUriComplete,
                    ExpiresInSeconds = prompt.ExpiresInSeconds,
                    PollIntervalSeconds = prompt.PollIntervalSeconds
                },
                mcpBaseUrl,
                statusCallback,
                cancellationToken)
            .ConfigureAwait(true);
        return new ConnectionDeviceTokenResult
        {
            AccessToken = token.AccessToken,
            ExpiresInSeconds = token.ExpiresInSeconds,
            TokenType = token.TokenType
        };
    }

    /// <inheritdoc />
    public async Task<ConnectionApiKeyFetchResult> TryFetchMcpApiKeyAsync(
        string mcpBaseUrl,
        string? bearerAccessToken,
        CancellationToken cancellationToken = default)
    {
        var result = await McpOidcAuthService
            .TryFetchMcpApiKeyAsync(mcpBaseUrl, bearerAccessToken, cancellationToken)
            .ConfigureAwait(true);
        return new ConnectionApiKeyFetchResult
        {
            ApiKey = result.ApiKey,
            WasRejected = result.WasRejected
        };
    }

    /// <inheritdoc />
    public bool IsJwtExpiredOrNearExpiry(string jwtToken, TimeSpan skew, out DateTimeOffset? expiresAtUtc)
        => ConnectionProbeHelper.IsJwtExpiredOrNearExpiry(jwtToken, skew, DecodeJwtBase64Url, out expiresAtUtc);

    private static McpAuthConfigResponse? ToCoreConfig(ConnectionAuthConfig? config)
        => config == null
            ? null
            : new McpAuthConfigResponse
            {
                Enabled = config.Enabled,
                Authority = config.Authority,
                ClientId = config.ClientId,
                Scopes = config.Scopes,
                DeviceAuthorizationEndpoint = config.DeviceAuthorizationEndpoint,
                TokenEndpoint = config.TokenEndpoint
            };

    private static byte[] DecodeJwtBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };

        return Convert.FromBase64String(normalized);
    }
}
