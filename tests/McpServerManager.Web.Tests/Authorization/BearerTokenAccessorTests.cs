using System.Security.Claims;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.Web.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace McpServerManager.Web.Tests.Authorization;

/// <summary>
/// Unit tests for <see cref="BearerTokenAccessor"/>.
/// </summary>
public sealed class BearerTokenAccessorTests
{
    private static IHttpContextAccessor BuildAccessor(HttpContext? context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return accessor;
    }

    private static DefaultHttpContext BuildAuthenticatedContext(string? accessToken)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test");
        context.User = new ClaimsPrincipal(identity);

        // Wire up IAuthenticationService so HttpContext.GetTokenAsync can resolve it.
        var props = new AuthenticationProperties();
        if (accessToken is not null)
            props.StoreTokens([new AuthenticationToken { Name = "access_token", Value = accessToken }]);

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(context.User, props, "Test"));

        var authService = Substitute.For<IAuthenticationService>();
        authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(authResult);

        var services = new ServiceCollection();
        services.AddSingleton(authService);
        context.RequestServices = services.BuildServiceProvider();

        return context;
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullHttpContext_ReturnsNull()
    {
        var accessor = new BearerTokenAccessor(BuildAccessor(null));
        var token = await accessor.GetAccessTokenAsync();
        Assert.Null(token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UnauthenticatedUser_ReturnsNull()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };
        var accessor = new BearerTokenAccessor(BuildAccessor(context));
        var token = await accessor.GetAccessTokenAsync();
        Assert.Null(token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AuthenticatedUserWithSavedToken_ReturnsToken()
    {
        var context = BuildAuthenticatedContext("test-access-token");
        var accessor = new BearerTokenAccessor(BuildAccessor(context));

        var token = await accessor.GetAccessTokenAsync();

        Assert.Equal("test-access-token", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AuthenticatedUserWithNoSavedToken_ReturnsNull()
    {
        var context = BuildAuthenticatedContext(accessToken: null);
        var accessor = new BearerTokenAccessor(BuildAccessor(context));

        var token = await accessor.GetAccessTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NullHttpContext_UsesWorkspaceCachedToken()
    {
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = @"E:\repo" };
        var tokenCache = Substitute.For<IWorkspaceAuthTokenCache>();
        tokenCache
            .TryReadValid(@"E:\repo")
            .Returns(new WorkspaceAuthToken
            {
                AccessToken = "cached-access-token",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
            });
        var accessor = new BearerTokenAccessor(BuildAccessor(null), tokenCache, workspaceContext);

        var token = await accessor.GetAccessTokenAsync();

        Assert.Equal("cached-access-token", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AuthenticatedUserWithNoSavedToken_FallsBackToWorkspaceCachedToken()
    {
        var context = BuildAuthenticatedContext(accessToken: null);
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = @"E:\repo" };
        var tokenCache = Substitute.For<IWorkspaceAuthTokenCache>();
        tokenCache
            .TryReadValid(@"E:\repo")
            .Returns(new WorkspaceAuthToken
            {
                AccessToken = "cached-access-token",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
            });
        var accessor = new BearerTokenAccessor(BuildAccessor(context), tokenCache, workspaceContext);

        var token = await accessor.GetAccessTokenAsync();

        Assert.Equal("cached-access-token", token);
    }
}
