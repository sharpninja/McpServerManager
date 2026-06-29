using System.Text.Json;

namespace McpServerManager.UI.Core.Auth;

/// <summary>
/// Identity-server token data cached for a single MCP workspace.
/// </summary>
public class WorkspaceAuthToken
{
    /// <summary>JWT access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Refresh token returned by the identity server, when available.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the access token expires.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Identity authority that issued the token.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Token endpoint used to mint the token.</summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>OIDC client id used to mint the token.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Token type returned by the identity server.</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Returns true when the access token is expired, including a small clock-skew buffer.</summary>
    public bool IsExpired(DateTimeOffset utcNow)
        => utcNow >= ExpiresAtUtc.AddSeconds(-30);

    /// <summary>
    /// Creates a cache token from an identity-server access token and optional token response metadata.
    /// </summary>
    public static WorkspaceAuthToken FromAccessToken(
        string accessToken,
        int? expiresInSeconds = null,
        string? refreshToken = null,
        string? authority = null,
        string? tokenEndpoint = null,
        string? clientId = null,
        string? tokenType = null,
        DateTimeOffset? utcNow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var expiresAt = TryGetJwtExpiration(accessToken, out var jwtExpiresAt)
            ? jwtExpiresAt
            : now.AddSeconds(expiresInSeconds is > 0 ? expiresInSeconds.Value : 3600);

        return new WorkspaceAuthToken
        {
            AccessToken = accessToken.Trim(),
            RefreshToken = Normalize(refreshToken) ?? string.Empty,
            ExpiresAtUtc = expiresAt,
            Authority = Normalize(authority) ?? string.Empty,
            TokenEndpoint = Normalize(tokenEndpoint) ?? string.Empty,
            ClientId = Normalize(clientId) ?? string.Empty,
            TokenType = Normalize(tokenType) ?? "Bearer"
        };
    }

    private static bool TryGetJwtExpiration(string accessToken, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
                return false;

            var normalized = parts[1].Replace('-', '+').Replace('_', '/');
            normalized = (normalized.Length % 4) switch
            {
                2 => normalized + "==",
                3 => normalized + "=",
                _ => normalized
            };

            using var document = JsonDocument.Parse(Convert.FromBase64String(normalized));
            if (!document.RootElement.TryGetProperty("exp", out var exp) ||
                !exp.TryGetInt64(out var expSeconds))
            {
                return false;
            }

            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
