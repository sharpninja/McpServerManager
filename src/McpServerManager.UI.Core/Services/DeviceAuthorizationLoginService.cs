using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Starts and completes the MCP OIDC device authorization login flow.
/// </summary>
public interface IDeviceAuthorizationLoginService
{
    /// <summary>
    /// Starts device authorization, opens the verification URL when possible, and returns the login prompt.
    /// </summary>
    Task<DeviceAuthorizationLoginStart> StartAsync(string mcpBaseUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the identity provider until the device authorization flow yields an access token.
    /// </summary>
    Task<DeviceAuthorizationLoginResult> CompleteAsync(
        DeviceAuthorizationLoginStart loginStart,
        string mcpBaseUrl,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Device authorization login prompt and state required to complete polling.
/// </summary>
public sealed class DeviceAuthorizationLoginStart
{
    /// <summary>Auth configuration used for this login attempt.</summary>
    public required ConnectionAuthConfig AuthConfig { get; init; }

    /// <summary>Device authorization prompt returned by the identity provider.</summary>
    public required ConnectionDeviceAuthorizationPrompt Prompt { get; init; }

    /// <summary>Verification URL to show or open for the user.</summary>
    public required string VerificationUrl { get; init; }

    /// <summary>True when the service successfully asked the host OS to open the verification URL.</summary>
    public bool BrowserOpened { get; init; }
}

/// <summary>
/// Access token returned after device authorization completes.
/// </summary>
public sealed class DeviceAuthorizationLoginResult
{
    /// <summary>Bearer access token.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Token lifetime in seconds when provided by the identity provider.</summary>
    public int? ExpiresInSeconds { get; init; }

    /// <summary>Token type when provided by the identity provider.</summary>
    public string? TokenType { get; init; }
}

/// <summary>
/// Shared implementation of the Director-style device authorization login flow.
/// </summary>
public sealed class DeviceAuthorizationLoginService : IDeviceAuthorizationLoginService
{
    private readonly IConnectionAuthService _connectionAuthService;
    private readonly IProcessLauncherService _processLauncher;
    private readonly ILogger<DeviceAuthorizationLoginService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthorizationLoginService"/> class.
    /// </summary>
    public DeviceAuthorizationLoginService(
        IConnectionAuthService connectionAuthService,
        IProcessLauncherService processLauncher,
        ILogger<DeviceAuthorizationLoginService> logger)
    {
        _connectionAuthService = connectionAuthService;
        _processLauncher = processLauncher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeviceAuthorizationLoginStart> StartAsync(
        string mcpBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var authConfig = await _connectionAuthService
            .TryGetAuthConfigAsync(mcpBaseUrl, cancellationToken)
            .ConfigureAwait(true);
        if (!_connectionAuthService.IsEnabled(authConfig))
        {
            throw new InvalidOperationException("MCP auth config does not enable device authorization.");
        }

        var prompt = await _connectionAuthService
            .StartDeviceAuthorizationAsync(authConfig!, mcpBaseUrl, cancellationToken)
            .ConfigureAwait(true);
        var verificationUrl = string.IsNullOrWhiteSpace(prompt.VerificationUriComplete)
            ? prompt.VerificationUri
            : prompt.VerificationUriComplete!;

        var browserOpened = TryOpenVerificationUrl(verificationUrl);
        return new DeviceAuthorizationLoginStart
        {
            AuthConfig = authConfig!,
            Prompt = prompt,
            VerificationUrl = verificationUrl,
            BrowserOpened = browserOpened
        };
    }

    /// <inheritdoc />
    public async Task<DeviceAuthorizationLoginResult> CompleteAsync(
        DeviceAuthorizationLoginStart loginStart,
        string mcpBaseUrl,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loginStart);

        var token = await _connectionAuthService
            .PollForAccessTokenAsync(
                loginStart.AuthConfig,
                loginStart.Prompt,
                mcpBaseUrl,
                statusCallback,
                cancellationToken)
            .ConfigureAwait(true);

        return new DeviceAuthorizationLoginResult
        {
            AccessToken = token.AccessToken,
            ExpiresInSeconds = token.ExpiresInSeconds,
            TokenType = token.TokenType
        };
    }

    private bool TryOpenVerificationUrl(string verificationUrl)
    {
        if (!Uri.TryCreate(verificationUrl, UriKind.Absolute, out _))
        {
            _logger.LogWarning("Skipping browser launch because verification URL is not absolute: {VerificationUrl}", verificationUrl);
            return false;
        }

        try
        {
            _processLauncher.OpenWithDefaultApp(verificationUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open OIDC verification URL: {VerificationUrl}", verificationUrl);
            return false;
        }
    }
}
