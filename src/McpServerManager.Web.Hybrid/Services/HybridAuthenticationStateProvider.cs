using System.Security.Claims;
using System.Text.Json;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.AspNetCore.Components.Authorization;

namespace McpServerManager.Web.Hybrid.Services;

internal sealed class HybridAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly IWorkspaceAuthTokenCache _tokenCache;
    private readonly WorkspaceContextViewModel _workspaceContext;

    public HybridAuthenticationStateProvider(
        IWorkspaceAuthTokenCache tokenCache,
        WorkspaceContextViewModel workspaceContext)
    {
        _tokenCache = tokenCache;
        _workspaceContext = workspaceContext;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(CreateAuthenticationState());

    public void NotifyTokenChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private AuthenticationState CreateAuthenticationState()
    {
        var token = _tokenCache.TryReadValid(_workspaceContext.ActiveWorkspacePath);
        if (token is null)
            return AnonymousState;

        return new AuthenticationState(CreatePrincipal(token.AccessToken));
    }

    private static ClaimsPrincipal CreatePrincipal(string accessToken)
    {
        var claims = ReadJwtClaims(accessToken);
        var name = claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, ClaimTypes.Name, StringComparison.Ordinal) ||
                string.Equals(claim.Type, "name", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "preferred_username", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "email", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "sub", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (!claims.Any(claim => string.Equals(claim.Type, ClaimTypes.Name, StringComparison.Ordinal)))
            claims.Add(new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? "Workspace user" : name));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static List<Claim> ReadJwtClaims(string accessToken)
    {
        var claims = new List<Claim>();
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
                return claims;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = (payload.Length % 4) switch
            {
                2 => payload + "==",
                3 => payload + "=",
                _ => payload
            };

            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            foreach (var property in document.RootElement.EnumerateObject())
            {
                AddClaim(claims, property.Name, property.Value);
            }
        }
        catch
        {
            // Non-JWT bearer tokens are still valid for API forwarding; they just cannot provide display claims.
        }

        return claims;
    }

    private static void AddClaim(List<Claim> claims, string type, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AddClaimValue(claims, type, value.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                AddClaimValue(claims, type, value.GetRawText());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        AddClaimValue(claims, type, item.GetString() ?? string.Empty);
                }

                break;
            case JsonValueKind.Object:
                AddRoleClaimsFromObject(claims, type, value);
                break;
        }
    }

    private static void AddRoleClaimsFromObject(List<Claim> claims, string type, JsonElement value)
    {
        if (string.Equals(type, "realm_access", StringComparison.OrdinalIgnoreCase) &&
            value.TryGetProperty("roles", out var realmRoles))
        {
            AddRoleClaims(claims, realmRoles);
            return;
        }

        if (!string.Equals(type, "resource_access", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var client in value.EnumerateObject())
        {
            if (client.Value.ValueKind == JsonValueKind.Object &&
                client.Value.TryGetProperty("roles", out var clientRoles))
            {
                AddRoleClaims(claims, clientRoles);
            }
        }
    }

    private static void AddRoleClaims(List<Claim> claims, JsonElement roles)
    {
        if (roles.ValueKind != JsonValueKind.Array)
            return;

        foreach (var role in roles.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String)
                AddClaimValue(claims, "role", role.GetString() ?? string.Empty);
        }
    }

    private static void AddClaimValue(List<Claim> claims, string type, string value)
    {
        claims.Add(new Claim(type, value));
        if (IsJwtRoleClaim(type))
            claims.Add(new Claim(ClaimTypes.Role, value));
    }

    private static bool IsJwtRoleClaim(string type)
        => string.Equals(type, "role", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, "roles", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, "realm_roles", StringComparison.OrdinalIgnoreCase);
}
