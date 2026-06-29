using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace McpServerManager.Web.Hybrid.Services;

internal sealed class HybridAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(AnonymousState);
}
