using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests;

public sealed class DeviceAuthorizationLoginServiceTests
{
    [Fact]
    public async Task StartAsync_UsesConnectionAuthAndOpensVerificationUrl()
    {
        var authService = Substitute.For<IConnectionAuthService>();
        var launcher = new CapturingProcessLauncher();
        var sut = new DeviceAuthorizationLoginService(
            authService,
            launcher,
            Substitute.For<ILogger<DeviceAuthorizationLoginService>>());
        var authConfig = CreateConfig();
        var prompt = CreatePrompt(verificationUriComplete: "http://localhost:7147/device?userCode=ABCD-EFGH");
        authService.TryGetAuthConfigAsync("http://localhost:7147", Arg.Any<CancellationToken>())
            .Returns(authConfig);
        authService.IsEnabled(authConfig).Returns(true);
        authService.StartDeviceAuthorizationAsync(authConfig, "http://localhost:7147", Arg.Any<CancellationToken>())
            .Returns(prompt);

        var result = await sut.StartAsync("http://localhost:7147");

        Assert.Equal(authConfig, result.AuthConfig);
        Assert.Equal(prompt, result.Prompt);
        Assert.Equal("http://localhost:7147/device?userCode=ABCD-EFGH", result.VerificationUrl);
        Assert.True(result.BrowserOpened);
        Assert.Equal(result.VerificationUrl, launcher.OpenedUrl);
    }

    [Fact]
    public async Task CompleteAsync_PollsForAccessToken()
    {
        var authService = Substitute.For<IConnectionAuthService>();
        var sut = new DeviceAuthorizationLoginService(
            authService,
            new CapturingProcessLauncher(),
            Substitute.For<ILogger<DeviceAuthorizationLoginService>>());
        var authConfig = CreateConfig();
        var prompt = CreatePrompt();
        var loginStart = new DeviceAuthorizationLoginStart
        {
            AuthConfig = authConfig,
            Prompt = prompt,
            VerificationUrl = prompt.VerificationUri
        };
        authService.PollForAccessTokenAsync(
                authConfig,
                prompt,
                "http://localhost:7147",
                Arg.Any<Action<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ConnectionDeviceTokenResult
            {
                AccessToken = "access-token",
                ExpiresInSeconds = 3600,
                TokenType = "Bearer"
            });

        var result = await sut.CompleteAsync(loginStart, "http://localhost:7147");

        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal(3600, result.ExpiresInSeconds);
        Assert.Equal("Bearer", result.TokenType);
    }

    private static ConnectionAuthConfig CreateConfig()
        => new()
        {
            Enabled = true,
            Authority = "http://localhost:7147",
            ClientId = "mcp-director",
            Scopes = "openid profile",
            DeviceAuthorizationEndpoint = "http://localhost:7147/connect/deviceauthorization",
            TokenEndpoint = "http://localhost:7147/connect/token"
        };

    private static ConnectionDeviceAuthorizationPrompt CreatePrompt(string? verificationUriComplete = null)
        => new()
        {
            DeviceCode = "device-code",
            UserCode = "ABCD-EFGH",
            VerificationUri = "http://localhost:7147/device",
            VerificationUriComplete = verificationUriComplete,
            ExpiresInSeconds = 600,
            PollIntervalSeconds = 5
        };

    private sealed class CapturingProcessLauncher : IProcessLauncherService
    {
        public string? OpenedUrl { get; private set; }

        public void OpenWithDefaultApp(string pathOrUrl)
        {
            OpenedUrl = pathOrUrl;
        }

        public Task<ProcessResult> RunAsync(
            string fileName,
            string arguments,
            string? workingDirectory = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public void ShellExecute(string command, string arguments)
            => throw new NotSupportedException();
    }
}
