using Microsoft.AspNetCore.Authentication;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.ViewModels;

namespace McpServerManager.Web.Authorization;

/// <summary>
/// Retrieves the OIDC access token saved in the current user's authentication session.
/// Used by <see cref="WebMcpContext"/> to forward the user's bearer token to the McpServer API
/// instead of using a static API key when the user is authenticated.
/// </summary>
internal sealed class BearerTokenAccessor
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IWorkspaceAuthTokenCache? _workspaceTokenCache;
    private readonly WorkspaceContextViewModel? _workspaceContext;

    /// <summary>Initializes a new <see cref="BearerTokenAccessor"/>.</summary>
    /// <param name="accessor">ASP.NET Core HTTP context accessor.</param>
    public BearerTokenAccessor(
        IHttpContextAccessor accessor,
        IWorkspaceAuthTokenCache? workspaceTokenCache = null,
        WorkspaceContextViewModel? workspaceContext = null)
    {
        _accessor = accessor;
        _workspaceTokenCache = workspaceTokenCache;
        _workspaceContext = workspaceContext;
    }

    /// <summary>
    /// Returns the OIDC <c>access_token</c> saved in the session, or <c>null</c> if the user
    /// is not authenticated or tokens were not saved (<c>options.SaveTokens</c> must be <c>true</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _accessor.HttpContext;
        if (httpContext is not null && httpContext.User.Identity?.IsAuthenticated == true)
            return await httpContext.GetTokenAsync("access_token").ConfigureAwait(true);

        return _workspaceTokenCache
            ?.TryReadValid(_workspaceContext?.ActiveWorkspacePath)
            ?.AccessToken;
    }
}
