using System.Security.Claims;
using System.Text.Json;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace McpServerManager.Web.Pages.Auth;

/// <summary>
/// Razor Page that starts the MCP device authorization flow and signs the browser in locally.
/// </summary>
public sealed class LoginModel : PageModel
{
    private static readonly TimeSpan LoginCacheDuration = TimeSpan.FromMinutes(15);

    private readonly IDeviceAuthorizationLoginService _deviceLoginService;
    private readonly IMemoryCache _memoryCache;
    private readonly IWorkspaceAuthTokenCache _tokenCache;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginModel"/> class.
    /// </summary>
    public LoginModel(
        IDeviceAuthorizationLoginService deviceLoginService,
        IMemoryCache memoryCache,
        IWorkspaceAuthTokenCache tokenCache,
        WorkspaceContextViewModel workspaceContext,
        IConfiguration configuration,
        ILogger<LoginModel> logger)
    {
        _deviceLoginService = deviceLoginService;
        _memoryCache = memoryCache;
        _tokenCache = tokenCache;
        _workspaceContext = workspaceContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>User code displayed as a browser-open fallback.</summary>
    public string? UserCode { get; private set; }

    /// <summary>Verification URL displayed as a browser-open fallback.</summary>
    public string? VerificationUrl { get; private set; }

    /// <summary>URL that completes polling and signs in the browser.</summary>
    public string? CompleteUrl { get; private set; }

    /// <summary>True when the host reported that the verification URL was opened automatically.</summary>
    public bool BrowserOpened { get; private set; }

    /// <summary>Error message to display when login cannot start.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Starts device authorization and renders a local waiting page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        var redirectUri = SanitizeReturnUrl(returnUrl);
        var mcpBaseUrl = GetMcpServerBaseUrl();

        try
        {
            var cachedResult = await TrySignInFromWorkspaceTokenAsync(redirectUri).ConfigureAwait(true);
            if (cachedResult is not null)
                return cachedResult;

            var loginStart = await _deviceLoginService
                .StartAsync(mcpBaseUrl, HttpContext.RequestAborted)
                .ConfigureAwait(true);
            var sessionId = Guid.NewGuid().ToString("N");
            _memoryCache.Set(
                BuildCacheKey(sessionId),
                loginStart,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = LoginCacheDuration
                });

            UserCode = loginStart.Prompt.UserCode;
            VerificationUrl = loginStart.VerificationUrl;
            BrowserOpened = loginStart.BrowserOpened;
            CompleteUrl = Url.Page(
                "/Auth/Login",
                "Complete",
                new { sessionId, returnUrl = redirectUri });
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MCP device authorization login.");
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    /// <summary>
    /// Polls for device authorization completion, stores the access token in the auth cookie, and redirects locally.
    /// </summary>
    public async Task<IActionResult> OnGetCompleteAsync(string sessionId, string? returnUrl)
    {
        var redirectUri = SanitizeReturnUrl(returnUrl);
        if (string.IsNullOrWhiteSpace(sessionId) ||
            !_memoryCache.TryGetValue<DeviceAuthorizationLoginStart>(BuildCacheKey(sessionId), out var loginStart) ||
            loginStart is null)
        {
            _logger.LogWarning("Device authorization completion requested with an invalid or expired session id.");
            return RedirectToPage("/Auth/Login", new { returnUrl = redirectUri });
        }

        try
        {
            var result = await _deviceLoginService
                .CompleteAsync(loginStart, GetMcpServerBaseUrl(), cancellationToken: HttpContext.RequestAborted)
                .ConfigureAwait(true);
            _memoryCache.Remove(BuildCacheKey(sessionId));

            var token = WorkspaceAuthToken.FromAccessToken(
                result.AccessToken,
                result.ExpiresInSeconds,
                authority: loginStart.AuthConfig.Authority,
                tokenEndpoint: loginStart.AuthConfig.TokenEndpoint,
                clientId: loginStart.AuthConfig.ClientId,
                tokenType: result.TokenType);
            _tokenCache.Save(ResolveWorkspacePath(), token);

            await SignInWithTokenAsync(
                    result.AccessToken,
                    result.TokenType,
                    redirectUri,
                    ResolveCookieExpiration(result.AccessToken, result.ExpiresInSeconds))
                .ConfigureAwait(true);
            return LocalRedirect(redirectUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP device authorization login failed while polling for completion.");
            ErrorMessage = ex.Message;
            UserCode = loginStart.Prompt.UserCode;
            VerificationUrl = loginStart.VerificationUrl;
            BrowserOpened = loginStart.BrowserOpened;
            CompleteUrl = Url.Page(
                "/Auth/Login",
                "Complete",
                new { sessionId, returnUrl = redirectUri });
            return Page();
        }
    }

    private string GetMcpServerBaseUrl()
        => _configuration["McpServer:BaseUrl"] ?? "http://localhost:7147";

    private async Task<IActionResult?> TrySignInFromWorkspaceTokenAsync(string redirectUri)
    {
        var token = _tokenCache.TryReadValid(ResolveWorkspacePath());
        if (token is null)
            return null;

        await SignInWithTokenAsync(
                token.AccessToken,
                token.TokenType,
                redirectUri,
                token.ExpiresAtUtc)
            .ConfigureAwait(true);
        return LocalRedirect(redirectUri);
    }

    private async Task SignInWithTokenAsync(
        string accessToken,
        string? tokenType,
        string redirectUri,
        DateTimeOffset expiresUtc)
    {
        var principal = BuildPrincipal(accessToken);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            RedirectUri = redirectUri,
            ExpiresUtc = expiresUtc
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "token_type", Value = string.IsNullOrWhiteSpace(tokenType) ? "Bearer" : tokenType }
        ]);

        await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                properties)
            .ConfigureAwait(true);
    }

    private string? ResolveWorkspacePath()
        => Normalize(_workspaceContext.ActiveWorkspacePath)
           ?? Normalize(_configuration["McpServer:WorkspacePath"]);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string SanitizeReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/";

    private static string BuildCacheKey(string sessionId)
        => $"mcp-web-device-login:{sessionId}";

    private static ClaimsPrincipal BuildPrincipal(string accessToken)
    {
        var claims = ExtractClaims(accessToken).ToList();
        if (claims.All(static claim => claim.Type != ClaimTypes.Name))
        {
            var name = claims.FirstOrDefault(static claim =>
                    claim.Type is "preferred_username" or "name" or "sub")
                ?.Value;
            claims.Add(new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? "mcp-user" : name));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, "role");
        return new ClaimsPrincipal(identity);
    }

    private static DateTimeOffset ResolveCookieExpiration(string accessToken, int? expiresInSeconds)
    {
        if (TryGetJwtPayload(accessToken, out var payload) &&
            payload.TryGetProperty("exp", out var exp) &&
            exp.TryGetInt64(out var expSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(expSeconds);
        }

        return DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds is > 0 ? expiresInSeconds.Value : 3600);
    }

    private static IEnumerable<Claim> ExtractClaims(string accessToken)
    {
        if (!TryGetJwtPayload(accessToken, out var payload))
        {
            yield return new Claim("access_token_present", "true");
            yield break;
        }

        foreach (var property in payload.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    yield return new Claim(property.Name, value);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            yield return new Claim(property.Name, value);
                    }
                }
            }
        }

        if (payload.TryGetProperty("realm_access", out var realmAccess) &&
            realmAccess.ValueKind == JsonValueKind.Object &&
            realmAccess.TryGetProperty("roles", out var realmRoles) &&
            realmRoles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in ExtractStringArray(realmRoles))
                yield return new Claim("role", role);
        }

        if (payload.TryGetProperty("resource_access", out var resourceAccess) &&
            resourceAccess.ValueKind == JsonValueKind.Object)
        {
            foreach (var client in resourceAccess.EnumerateObject())
            {
                if (client.Value.ValueKind != JsonValueKind.Object ||
                    !client.Value.TryGetProperty("roles", out var clientRoles) ||
                    clientRoles.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var role in ExtractStringArray(clientRoles))
                    yield return new Claim("role", role);
            }
        }
    }

    private static IEnumerable<string> ExtractStringArray(JsonElement element)
    {
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
            }
        }
    }

    private static bool TryGetJwtPayload(string accessToken, out JsonElement payload)
    {
        payload = default;
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
            payload = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
