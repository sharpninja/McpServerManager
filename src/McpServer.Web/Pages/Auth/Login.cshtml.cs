using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServerManager.Web.Pages.Auth;

/// <summary>
/// Non-interactive Razor Page that issues an OIDC challenge redirect.
/// Must be a Razor Page (not a Blazor component) because <c>HttpContext.ChallengeAsync</c> requires
/// a real HTTP context outside of a Blazor SignalR circuit.
/// </summary>
public sealed class LoginModel : PageModel
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IAuthenticationSchemeProvider schemeProvider,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        ILogger<LoginModel> logger)
    {
        _schemeProvider = schemeProvider;
        _oidcOptions = oidcOptions;
        _logger = logger;
    }

    /// <summary>
    /// Issues an OpenID Connect challenge when configured, redirecting the browser to the OIDC provider's authorization endpoint.
    /// Before issuing the challenge, verifies that the OIDC discovery endpoint is reachable so that
    /// a downed identity provider surfaces a friendly redirect instead of an unhandled exception.
    /// </summary>
    /// <param name="returnUrl">Optional local URL to return to after login. Non-local URLs are ignored.</param>
    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        var oidcScheme = await _schemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme).ConfigureAwait(true);
        if (oidcScheme is null)
        {
            _logger.LogWarning("Login requested but OpenID Connect authentication is not configured.");
            return Redirect("/access-denied");
        }

        // Pre-flight: verify the OIDC discovery endpoint is reachable before issuing the challenge.
        // Challenge() returns a deferred ChallengeResult; if the provider is unreachable the exception
        // fires deep inside the middleware pipeline where we can't catch it from the page handler.
        var options = _oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        if (options.ConfigurationManager is not null)
        {
            try
            {
                await options.ConfigurationManager.GetConfigurationAsync(HttpContext.RequestAborted).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OIDC discovery endpoint is unreachable. The identity provider at {Authority} may be down.", options.Authority);
                return Redirect("/access-denied");
            }
        }

        var redirectUri = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/";

        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
