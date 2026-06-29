using System.Text;
using System.Text.Json;
using McpServerManager.UI.Core.Auth;
using Xunit;

namespace McpServerManager.UI.Core.Tests;

public sealed class WorkspaceAuthTokenCacheTests
{
    [Fact]
    public void TryReadValid_LoadsValidWorkspaceToken()
    {
        using var workspace = TestWorkspace.Create();
        var cache = new FileWorkspaceAuthTokenCache();
        var token = new WorkspaceAuthToken
        {
            AccessToken = CreateJwt(DateTimeOffset.UtcNow.AddHours(1)),
            RefreshToken = "refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            Authority = "http://localhost:7147",
            TokenEndpoint = "http://localhost:7147/connect/token",
            ClientId = "mcp-web"
        };

        cache.Save(workspace.Path, token);

        var loaded = cache.TryReadValid(workspace.Path);

        Assert.NotNull(loaded);
        Assert.Equal(token.AccessToken, loaded.AccessToken);
        Assert.Equal(token.RefreshToken, loaded.RefreshToken);
        Assert.Equal(token.Authority, loaded.Authority);
    }

    [Fact]
    public void TryReadValid_ExpiredWorkspaceTokenDeletesCacheFile()
    {
        using var workspace = TestWorkspace.Create();
        var cache = new FileWorkspaceAuthTokenCache();
        cache.Save(workspace.Path, new WorkspaceAuthToken
        {
            AccessToken = CreateJwt(DateTimeOffset.UtcNow.AddMinutes(-5)),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        var cachePath = cache.GetCachePath(workspace.Path);

        var loaded = cache.TryReadValid(workspace.Path);

        Assert.Null(loaded);
        Assert.False(File.Exists(cachePath));
    }

    [Fact]
    public void TryReadValid_NoWorkspacePath_ReturnsNull()
    {
        var cache = new FileWorkspaceAuthTokenCache();

        var loaded = cache.TryReadValid(null);

        Assert.Null(loaded);
    }

    [Fact]
    public void FromAccessToken_UsesJwtExpirationWhenPresent()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(42);
        var accessToken = CreateJwt(expiresAt);

        var token = WorkspaceAuthToken.FromAccessToken(
            accessToken,
            expiresInSeconds: 3600,
            refreshToken: "refresh-token",
            authority: "http://localhost:7147",
            tokenEndpoint: "http://localhost:7147/connect/token",
            clientId: "mcp-web");

        Assert.Equal(accessToken, token.AccessToken);
        Assert.Equal("refresh-token", token.RefreshToken);
        Assert.Equal("http://localhost:7147", token.Authority);
        Assert.Equal("http://localhost:7147/connect/token", token.TokenEndpoint);
        Assert.Equal("mcp-web", token.ClientId);
        Assert.InRange(token.ExpiresAtUtc, expiresAt.AddSeconds(-1), expiresAt.AddSeconds(1));
    }

    private static string CreateJwt(DateTimeOffset expiresAt)
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
            exp = expiresAt.ToUnixTimeSeconds()
        };

        return $"{Encode(new { alg = "none", typ = "JWT" })}.{Encode(payload)}.";
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestWorkspace Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "mcpserver-manager-auth-cache-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TestWorkspace(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
