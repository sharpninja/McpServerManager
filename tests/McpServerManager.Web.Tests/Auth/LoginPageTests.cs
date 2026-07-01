using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using McpServerManager.UI.Core.Auth;
using McpServerManager.UI.Core.Services;
using McpServerManager.Web.Tests.TestInfrastructure;
using Xunit;

namespace McpServerManager.Web.Tests.Auth;

/// <summary>
/// Integration tests for the <c>/login</c> Razor Page.
/// </summary>
public sealed partial class LoginPageTests
{
    [Fact]
    public async Task GetLogin_Unauthenticated_StartsDeviceFlow()
    {
        var loginService = new DeviceLoginServiceStub();
        using var factory = WebTestFactory.Create(deviceLoginService: loginService);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ABCD-EFGH", body);
        Assert.Contains("http://localhost:7147/device?userCode=ABCD-EFGH", body);
        Assert.Equal("http://localhost:7147", loginService.StartedBaseUrl);
    }

    [Fact]
    public async Task GetLogin_WithReturnUrl_PreservesLocalReturnUrlForCompletion()
    {
        using var factory = WebTestFactory.Create(deviceLoginService: new DeviceLoginServiceStub());
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/login?returnUrl=%2Ftodos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var completeUrl = ExtractCompleteUrl(body);
        Assert.Contains("returnUrl=%2Ftodos", completeUrl);
    }

    [Fact]
    public async Task GetLogin_WithNonLocalReturnUrl_FallsBackToRoot()
    {
        using var factory = WebTestFactory.Create(deviceLoginService: new DeviceLoginServiceStub());
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/login?returnUrl=https%3A%2F%2Fevil.example.com%2F");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var completeUrl = ExtractCompleteUrl(body);
        Assert.DoesNotContain("evil.example.com", completeUrl);
        Assert.Contains("returnUrl=%2F", completeUrl);
    }

    [Fact]
    public async Task GetComplete_AfterDeviceApproval_SignsInAndRedirects()
    {
        var loginService = new DeviceLoginServiceStub
        {
            CompleteResult = new DeviceAuthorizationLoginResult
            {
                AccessToken = CreateJwt("web-user", "admin"),
                ExpiresInSeconds = 3600,
                TokenType = "Bearer"
            }
        };
        var tokenCache = new WorkspaceTokenCacheStub();
        using var factory = WebTestFactory.Create(
            deviceLoginService: loginService,
            workspaceTokenCache: tokenCache,
            workspacePath: @"E:\repo");
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var startResponse = await client.GetAsync("/login?returnUrl=%2Ftodos");
        var startBody = await startResponse.Content.ReadAsStringAsync();
        var completeUrl = ExtractCompleteUrl(startBody);

        var response = await client.GetAsync(completeUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/todos", response.Headers.Location?.ToString());
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, static cookie => cookie.Contains("McpServerManager.Web.Auth", StringComparison.Ordinal));
        Assert.NotNull(loginService.CompletedLoginStart);
        Assert.Equal(@"E:\repo", tokenCache.SavedWorkspacePath);
        Assert.Equal(loginService.CompleteResult.AccessToken, tokenCache.SavedToken?.AccessToken);
        Assert.Equal("http://localhost:7147", tokenCache.SavedToken?.Authority);
        Assert.Equal("http://localhost:7147/connect/token", tokenCache.SavedToken?.TokenEndpoint);
        Assert.Equal("mcp-director", tokenCache.SavedToken?.ClientId);
    }

    [Fact]
    public async Task GetLogin_WithCachedWorkspaceToken_SignsInAndRedirectsWithoutStartingDeviceFlow()
    {
        var cachedToken = new WorkspaceAuthToken
        {
            AccessToken = CreateJwt("cached-user", "admin"),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            Authority = "http://localhost:7147",
            TokenEndpoint = "http://localhost:7147/connect/token",
            ClientId = "mcp-director",
            TokenType = "Bearer"
        };
        var tokenCache = new WorkspaceTokenCacheStub { ReadToken = cachedToken };
        var loginService = new DeviceLoginServiceStub();
        using var factory = WebTestFactory.Create(
            deviceLoginService: loginService,
            workspaceTokenCache: tokenCache,
            workspacePath: @"E:\repo");
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/login?returnUrl=%2Ftodos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/todos", response.Headers.Location?.ToString());
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, static cookie => cookie.Contains("McpServerManager.Web.Auth", StringComparison.Ordinal));
        Assert.Null(loginService.StartedBaseUrl);
        Assert.Equal(@"E:\repo", tokenCache.LastReadWorkspacePath);
    }

    private static string ExtractCompleteUrl(string body)
    {
        var decoded = WebUtility.HtmlDecode(body);
        var match = ContinueLinkRegex().Match(decoded);
        Assert.True(match.Success, "Expected login page to render a continue link.");
        return match.Groups["url"].Value;
    }

    private static string CreateJwt(string preferredUsername, params string[] roles)
    {
        static string Encode(object value)
        {
            var json = JsonSerializer.Serialize(value);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        var payload = new
        {
            sub = "subject-1",
            preferred_username = preferredUsername,
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            realm_access = new { roles }
        };
        return $"{Encode(new { alg = "none", typ = "JWT" })}.{Encode(payload)}.";
    }

    [GeneratedRegex("id=\"continue-link\"\\s+href=\"(?<url>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ContinueLinkRegex();

    private sealed class DeviceLoginServiceStub : IDeviceAuthorizationLoginService
    {
        public string? StartedBaseUrl { get; private set; }

        public DeviceAuthorizationLoginStart? CompletedLoginStart { get; private set; }

        public DeviceAuthorizationLoginResult CompleteResult { get; set; } = new()
        {
            AccessToken = CreateJwt("web-user", "admin"),
            ExpiresInSeconds = 3600,
            TokenType = "Bearer"
        };

        public Task<DeviceAuthorizationLoginStart> StartAsync(
            string mcpBaseUrl,
            CancellationToken cancellationToken = default)
        {
            StartedBaseUrl = mcpBaseUrl;
            var prompt = new ConnectionDeviceAuthorizationPrompt
            {
                DeviceCode = "device-code",
                UserCode = "ABCD-EFGH",
                VerificationUri = "http://localhost:7147/device",
                VerificationUriComplete = "http://localhost:7147/device?userCode=ABCD-EFGH",
                ExpiresInSeconds = 600,
                PollIntervalSeconds = 1
            };
            return Task.FromResult(new DeviceAuthorizationLoginStart
            {
                AuthConfig = new ConnectionAuthConfig
                {
                    Enabled = true,
                    Authority = "http://localhost:7147",
                    ClientId = "mcp-director",
                    DeviceAuthorizationEndpoint = "http://localhost:7147/connect/deviceauthorization",
                    TokenEndpoint = "http://localhost:7147/connect/token"
                },
                Prompt = prompt,
                VerificationUrl = prompt.VerificationUriComplete!,
                BrowserOpened = true
            });
        }

        public Task<DeviceAuthorizationLoginResult> CompleteAsync(
            DeviceAuthorizationLoginStart loginStart,
            string mcpBaseUrl,
            Action<string>? statusCallback = null,
            CancellationToken cancellationToken = default)
        {
            CompletedLoginStart = loginStart;
            return Task.FromResult(CompleteResult);
        }
    }

    private sealed class WorkspaceTokenCacheStub : IWorkspaceAuthTokenCache
    {
        public WorkspaceAuthToken? ReadToken { get; init; }

        public string? LastReadWorkspacePath { get; private set; }

        public string? SavedWorkspacePath { get; private set; }

        public WorkspaceAuthToken? SavedToken { get; private set; }

        public string? ClearedWorkspacePath { get; private set; }

        public WorkspaceAuthToken? TryReadValid(string? workspacePath)
        {
            LastReadWorkspacePath = workspacePath;
            return ReadToken;
        }

        public void Save(string? workspacePath, WorkspaceAuthToken token)
        {
            SavedWorkspacePath = workspacePath;
            SavedToken = token;
        }

        public void Clear(string? workspacePath)
            => ClearedWorkspacePath = workspacePath;

        public string? TryGetCachePath(string? workspacePath)
            => null;
    }
}
