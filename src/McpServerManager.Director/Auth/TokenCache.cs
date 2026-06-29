using McpServerManager.UI.Core.Auth;

namespace McpServerManager.Director.Auth;

/// <summary>
/// Cached OAuth token data used by legacy Director auth call sites.
/// </summary>
internal sealed class CachedToken : WorkspaceAuthToken
{
    /// <summary>Whether the access token is expired.</summary>
    public new bool IsExpired => IsExpired(DateTimeOffset.UtcNow);
}

/// <summary>
/// Director compatibility wrapper over the shared workspace token cache.
/// </summary>
internal static class TokenCache
{
    private static readonly FileWorkspaceAuthTokenCache s_cache = new();

    /// <summary>Loads a valid cached token, or returns null if none exists. Expired tokens are deleted.</summary>
    public static CachedToken? Load(string? workspacePath = null)
    {
        var token = s_cache.TryReadValid(workspacePath);
        return token is null ? null : ToCachedToken(token);
    }

    /// <summary>Saves a token to the workspace cache.</summary>
    public static void Save(CachedToken token, string? workspacePath = null)
    {
        s_cache.Save(workspacePath, token);
    }

    /// <summary>Deletes the cached token file.</summary>
    public static void Clear(string? workspacePath = null)
    {
        s_cache.Clear(workspacePath);
    }

    /// <summary>Returns the cache file path for display purposes.</summary>
    public static string GetCachePath(string? workspacePath = null)
        => s_cache.TryGetCachePath(workspacePath) ?? "(no active workspace token cache)";

    private static CachedToken ToCachedToken(WorkspaceAuthToken token)
        => new()
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            Authority = token.Authority,
            TokenEndpoint = token.TokenEndpoint,
            ClientId = token.ClientId,
            TokenType = token.TokenType
        };
}
