using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Web.Pages.Auth;

/// <summary>
/// Non-interactive Razor Page that signs the user out of both the local cookie session and the OIDC provider.
/// Must be a Razor Page (not a Blazor component) because <c>HttpContext.SignOutAsync</c> requires
/// a real HTTP context outside of a Blazor SignalR circuit.
/// </summary>
public sealed class LogoutModel : PageModel
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IWorkspaceAuthTokenCache _tokenCache;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogoutModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutModel"/> class.
    /// </summary>
    /// <param name="schemeProvider">Authentication scheme provider used to verify OIDC availability.</param>
    /// <param name="tokenCache">Workspace token cache to clear for the active workspace.</param>
    /// <param name="workspaceContext">Active workspace context.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger.</param>
    public LogoutModel(
        IAuthenticationSchemeProvider schemeProvider,
        IWorkspaceAuthTokenCache tokenCache,
        WorkspaceContextViewModel workspaceContext,
        IConfiguration configuration,
        ILogger<LogoutModel> logger)
    {
        _schemeProvider = schemeProvider;
        _tokenCache = tokenCache;
        _workspaceContext = workspaceContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Signs the user out of the cookie scheme, then issues a SignOut to the OIDC provider
    /// which triggers the provider's end_session redirect back to the app root when OIDC is configured.
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        _tokenCache.Clear(ResolveWorkspacePath());

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(true);

        var oidcScheme = await _schemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme).ConfigureAwait(true);
        if (oidcScheme is null)
        {
            _logger.LogInformation("Logout requested while OpenID Connect authentication is disabled; completed cookie sign-out only.");
            return Redirect("/");
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private string? ResolveWorkspacePath()
        => Normalize(_workspaceContext.ActiveWorkspacePath)
           ?? Normalize(_configuration["McpServer:WorkspacePath"]);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
