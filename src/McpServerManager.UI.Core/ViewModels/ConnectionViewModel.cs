using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>
/// Connection info emitted when a connection/auth flow completes.
/// </summary>
public sealed record ConnectionEstablishedInfo(string BaseUrl, string? ApiKey, string? BearerToken = null);

/// <summary>
/// Connection/authentication ViewModel for host applications.
/// </summary>
public partial class ConnectionViewModel : ViewModelBase
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<ConnectionViewModel> _logger;
    private readonly IUiDispatcherService _uiDispatcher;
    private readonly IConnectionAuthService? _authService;

    [ObservableProperty]
    private string _host = "10.0.2.2";

    [ObservableProperty]
    private string _port = "7147";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isConnecting = false;

    [ObservableProperty]
    private bool _isOidcSignInRequired;

    [ObservableProperty]
    private string _oidcStatusMessage = "";

    [ObservableProperty]
    private string _oidcUserCode = "";

    [ObservableProperty]
    private string _oidcVerificationUrl = "";

    [ObservableProperty]
    private bool _oidcCanOpenBrowser;

    private Func<string, bool>? _externalUrlOpener;
    private Func<string?>? _cachedOidcTokenReader;
    private Action<string?>? _cachedOidcTokenWriter;
    private Func<bool>? _oidcPostTokenForegroundActivator;
    private Func<Task<string?>>? _qrCodeScanner;
    private string? _oidcBearerToken;
    private string? _lastOidcAuthority;
    private string? _lastMcpBaseUrl;
    private string? _lastOidcClientId;
    private CancellationTokenSource? _connectCts;
    private static readonly TimeSpan CachedJwtExpirySkew = TimeSpan.FromMinutes(1);

    [ObservableProperty]
    private bool _canScanQrCode;

    /// <summary>Raised when the user completes connect (and auth, if required).</summary>
    public event Action<ConnectionEstablishedInfo>? Connected;

    /// <summary>
    /// Creates a new connection/authentication ViewModel.
    /// </summary>
    public ConnectionViewModel(
        IDispatcher dispatcher,
        ILogger<ConnectionViewModel>? logger = null,
        IUiDispatcherService? uiDispatcher = null,
        IConnectionAuthService? authService = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? NullLogger<ConnectionViewModel>.Instance;
        _uiDispatcher = uiDispatcher ?? new ImmediateUiDispatcherService();
        _authService = authService;  // may be null; callers can set or use NoOp; moves JWT parse out of VM
    }

    /// <summary>
    /// Configures URL opener used to launch external browser for OIDC.
    /// </summary>
    public void SetExternalUrlOpener(Func<string, bool>? externalUrlOpener)
    {
        _externalUrlOpener = externalUrlOpener;
        DispatchToUi(() => OidcCanOpenBrowser = !string.IsNullOrWhiteSpace(OidcVerificationUrl) && _externalUrlOpener != null);
        _logger.LogInformation("External URL opener set: {HasOpener}", _externalUrlOpener != null);
    }

    /// <summary>
    /// Configures cached token read/write accessors.
    /// </summary>
    public void SetOidcTokenCacheAccessors(Func<string?>? readCachedToken, Action<string?>? writeCachedToken)
    {
        _cachedOidcTokenReader = readCachedToken;
        _cachedOidcTokenWriter = writeCachedToken;
        _logger.LogInformation(
            "OIDC token cache accessors configured. HasReader={HasReader}, HasWriter={HasWriter}",
            _cachedOidcTokenReader != null,
            _cachedOidcTokenWriter != null);
    }

    /// <summary>
    /// Configures callback used to bring app to foreground after auth.
    /// </summary>
    public void SetOidcPostTokenForegroundActivator(Func<bool>? foregroundActivator)
    {
        _oidcPostTokenForegroundActivator = foregroundActivator;
        _logger.LogInformation(
            "OIDC post-token foreground activator configured: {HasActivator}",
            _oidcPostTokenForegroundActivator != null);
    }

    /// <summary>
    /// Configures optional QR code scanner used to populate host/port.
    /// </summary>
    public void SetQrCodeScanner(Func<Task<string?>>? scanner)
    {
        _qrCodeScanner = scanner;
        DispatchToUi(() => CanScanQrCode = scanner != null);
        _logger.LogInformation("QR code scanner configured: {HasScanner}", scanner != null);
    }

    /// <summary>
    /// Scans a QR code and updates host/port when successful.
    /// </summary>
    protected async Task ScanQrCodeAsync()
    {
        if (_qrCodeScanner == null) return;
        try
        {
            var result = await _qrCodeScanner().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                string host;
                string? port = null;

                // If the scanned value is a URL, extract just the host
                if (Uri.TryCreate(result, UriKind.Absolute, out var uri))
                {
                    host = uri.Host;
                    if (uri.Port > 0 && uri.Port != 80 && uri.Port != 443)
                        port = uri.Port.ToString();
                }
                else
                {
                    host = result.Trim();
                }

                await DispatchToUiAsync(() =>
                {
                    Host = host;
                    if (!string.IsNullOrWhiteSpace(port))
                        Port = port;
                    ErrorMessage = "";
                }).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QR code scan failed");
            await DispatchToUiAsync(() => ErrorMessage = $"QR scan failed: {ex.Message}").ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Logs out current session and immediately retries connection.
    /// </summary>
    protected async Task LogoutAndRetryAsync()
    {
        _logger.LogInformation("LogoutAndRetryAsync invoked");
        await PerformOidcLogoutAsync().ConfigureAwait(true);
        await ConnectAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Logs out current session.
    /// </summary>
    protected async Task LogoutAsync()
    {
        _logger.LogInformation("LogoutAsync invoked");
        await PerformOidcLogoutAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Cancels an in-progress connect/auth flow.
    /// </summary>
    protected void CancelConnect()
    {
        _logger.LogInformation("CancelConnect invoked — aborting in-progress OIDC flow");
        _connectCts?.Cancel();
        _connectCts = null;
        DispatchToUi(() =>
        {
            IsConnecting = false;
            IsOidcSignInRequired = false;
            OidcStatusMessage = "";
        });
        TryBringAppToForegroundAfterOidcTokenAcquired();
    }

    private async Task PerformOidcLogoutAsync()
    {
        await DispatchToUiAsync(() => ErrorMessage = "").ConfigureAwait(true);
        var token = _oidcBearerToken ?? TryReadCachedOidcToken();

        if (_lastMcpBaseUrl != null && _lastOidcAuthority != null)
        {
            _logger.LogInformation("Performing OIDC logout via revocation/end-session API");
            var cmd = new LogoutCommand(_lastMcpBaseUrl!, _lastOidcAuthority, null, _oidcBearerToken);
            var success = await _dispatcher.SendAsync(cmd, default).ConfigureAwait(true);
            _logger.LogInformation("OIDC API logout result: {Success}", success.IsSuccess ? success.Value : false);
        }
        else
        {
            _logger.LogInformation("No OIDC authority/baseUrl cached; skipping Keycloak SSO logout");
        }

        ClearCachedOidcToken();
        _oidcBearerToken = null;
        TryBringAppToForegroundAfterOidcTokenAcquired();
    }

    /// <summary>
    /// Starts connection and authentication flow.
    /// </summary>
    protected async Task ConnectAsync()
    {
        // Thin to dispatch + apply only. Validation/error logic moved to handlers/results.
        await DispatchToUiAsync(() => IsConnecting = true).ConfigureAwait(true);
        var url = "http://" + (Host ?? "") + ":" + (Port ?? "");
        await _dispatcher.QueryAsync(new ProbeHealthAndResolveUrlQuery(url)).ConfigureAwait(true);
        await _dispatcher.SendAsync(new FetchMcpApiKeyCommand(url, null)).ConfigureAwait(true);
        await DispatchToUiAsync(() =>
        {
            IsConnecting = false;
            IsOidcSignInRequired = false;
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens the OIDC verification URL in an external browser.
    /// </summary>
    protected void OpenOidcVerificationUrl()
    {
        if (string.IsNullOrWhiteSpace(OidcVerificationUrl))
        {
            _logger.LogWarning("OpenOidcVerificationUrl ignored: URL is empty");
            return;
        }

        if (_externalUrlOpener == null)
        {
            DispatchToUi(() => ErrorMessage = "No browser launcher is available on this device.");
            _logger.LogWarning("OpenOidcVerificationUrl failed: no browser launcher available");
            return;
        }

        _logger.LogInformation("Opening OIDC verification URL via external browser: {Url}", OidcVerificationUrl);
        if (!_externalUrlOpener.Invoke(OidcVerificationUrl))
        {
            DispatchToUi(() => ErrorMessage = "Could not open the sign-in page.");
            _logger.LogWarning("OpenOidcVerificationUrl failed to launch browser");
        }
    }

    private async Task<string?> TryAuthenticateWithOidcAsync(string mcpBaseUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking MCP auth config at {BaseUrl}/auth/config", mcpBaseUrl);
        var query = new GetAuthConfigQuery(); // Note: in practice the query may take base url if needed; handler can use context or we dispatch with url if extended.
        // For this flow we use a simple query; real impl would pass base if the handler supports.
        var authConfig = await _dispatcher.QueryAsync(query, cancellationToken).ConfigureAwait(true);
        var snapshot = authConfig.IsSuccess ? authConfig.Value : null;
        _logger.LogInformation(
            "MCP auth config result: enabled={Enabled}, clientId={ClientId}, hasDeviceEndpoint={HasDeviceEndpoint}, hasTokenEndpoint={HasTokenEndpoint}",
            snapshot?.Enabled,
            snapshot?.ClientId ?? "<null>",
            !string.IsNullOrWhiteSpace(snapshot?.DeviceAuthorizationEndpoint),
            !string.IsNullOrWhiteSpace(snapshot?.TokenEndpoint));
        if (snapshot == null || !snapshot.Enabled)
        {
            _logger.LogInformation("OIDC not enabled/configured for {BaseUrl}; continuing without interactive auth", mcpBaseUrl);
            _oidcBearerToken = null;
            return await TryFetchDefaultApiKeyFallbackAsync(mcpBaseUrl, cancellationToken).ConfigureAwait(true);
        }

        var cachedOidcToken = TryReadCachedOidcToken();
        if (!string.IsNullOrWhiteSpace(cachedOidcToken) &&
            IsJwtExpiredOrNearExpiry(cachedOidcToken, CachedJwtExpirySkew, out var expiresAtUtc))
        {
            await DispatchToUiAsync(() => OidcStatusMessage = "Session expired. Sign in again.").ConfigureAwait(true);
            _logger.LogWarning(
                "Cached OIDC token is expired/near expiry (expUtc={ExpiresAtUtc}); clearing cache and requiring sign-in",
                expiresAtUtc?.ToString("O") ?? "<unknown>");
            ClearCachedOidcToken();
            cachedOidcToken = null;
            _oidcBearerToken = null;
        }

        if (!string.IsNullOrWhiteSpace(cachedOidcToken))
        {
            await DispatchToUiAsync(() => OidcStatusMessage = "Reusing previous sign-in…").ConfigureAwait(true);
            _logger.LogInformation("Attempting cached OIDC token reuse for {BaseUrl}", mcpBaseUrl);

            var cachedFetchCmd = new FetchMcpApiKeyCommand(mcpBaseUrl, cachedOidcToken);
            var cachedFetchResult = await _dispatcher.SendAsync(cachedFetchCmd, cancellationToken).ConfigureAwait(true);
            var cachedApiKeyResult = cachedFetchResult.IsSuccess ? cachedFetchResult.Value! : new ConnectionApiKeyFetchResult();

            if (cachedApiKeyResult.IsSuccess)
            {
                await DispatchToUiAsync(() => OidcStatusMessage = "Opening MCP…").ConfigureAwait(true);
                _logger.LogInformation("Cached OIDC token reuse succeeded; MCP API key acquired");
                _oidcBearerToken = cachedOidcToken;
                return cachedApiKeyResult.ApiKey;
            }

            if (cachedApiKeyResult.WasRejected)
            {
                await DispatchToUiAsync(() => OidcStatusMessage = "Session expired. Sign in again.").ConfigureAwait(true);
                _logger.LogWarning("Cached OIDC token was rejected by server; clearing cache and falling back to interactive sign-in");
                ClearCachedOidcToken();
            }
            else
            {
                _logger.LogInformation("Cached OIDC token reuse did not yield an MCP API key; falling back to interactive sign-in");
            }
        }

        // NOTE: We intentionally do NOT try /api-key before OIDC here.
        // The default API key from /api-key only works for the primary workspace.
        // When OIDC is enabled, we need a Bearer token for cross-workspace auth.
        _logger.LogInformation("OIDC enabled for {BaseUrl}; starting device authorization", mcpBaseUrl);
        _lastOidcAuthority = snapshot!.Authority;
        _lastMcpBaseUrl = mcpBaseUrl;
        _lastOidcClientId = snapshot!.ClientId;
        var startCmd = new StartDeviceAuthorizationCommand(mcpBaseUrl, snapshot!);
        var startResult = await _dispatcher.SendAsync(startCmd, cancellationToken).ConfigureAwait(true);
        var prompt = startResult.IsSuccess ? startResult.Value! : throw new InvalidOperationException("Failed to start device auth");

        var verificationUrl = string.IsNullOrWhiteSpace(prompt.VerificationUriComplete)
            ? prompt.VerificationUri
            : prompt.VerificationUriComplete!;
        var canOpenBrowser = !string.IsNullOrWhiteSpace(verificationUrl) && _externalUrlOpener != null;
        await DispatchToUiAsync(() =>
        {
            IsOidcSignInRequired = true;
            OidcUserCode = prompt.UserCode;
            OidcVerificationUrl = verificationUrl;
            OidcCanOpenBrowser = canOpenBrowser;
            OidcStatusMessage = "Sign in to the identity provider and approve this device.";
        }).ConfigureAwait(true);
        _logger.LogInformation(
            "OIDC device prompt ready. UserCodePresent={HasUserCode}, VerificationUrl='{VerificationUrl}', CanOpenBrowser={CanOpenBrowser}",
            !string.IsNullOrWhiteSpace(OidcUserCode),
            OidcVerificationUrl,
            OidcCanOpenBrowser);

        if (OidcCanOpenBrowser && _externalUrlOpener != null)
        {
            _logger.LogInformation("Auto-opening OIDC verification page");
            _ = _externalUrlOpener.Invoke(OidcVerificationUrl);
        }

        var pollCmd = new PollForAccessTokenCommand(snapshot!, prompt, mcpBaseUrl);
        var tokenResult = await _dispatcher.SendAsync(pollCmd, cancellationToken).ConfigureAwait(true);
        var token = tokenResult.IsSuccess ? tokenResult.Value! : throw new InvalidOperationException("Poll failed");

        _logger.LogInformation("OIDC sign-in complete. Access token acquired for {BaseUrl}", mcpBaseUrl);
        _oidcBearerToken = token.AccessToken;
        WriteCachedOidcToken(token.AccessToken);
        TryBringAppToForegroundAfterOidcTokenAcquired();

        await DispatchToUiAsync(() => OidcStatusMessage = "Sign-in complete. Acquiring MCP API key…").ConfigureAwait(true);
        _logger.LogInformation("Fetching MCP default API key from {BaseUrl}/api-key after OIDC sign-in", mcpBaseUrl);
        var postOidcFetchCmd = new FetchMcpApiKeyCommand(mcpBaseUrl, token.AccessToken);
        var mcpApiKeyResult = await _dispatcher.SendAsync(postOidcFetchCmd, cancellationToken).ConfigureAwait(true);

        var keyRes = mcpApiKeyResult.IsSuccess ? mcpApiKeyResult.Value : null;
        if (keyRes != null && keyRes.IsSuccess)
        {
            await DispatchToUiAsync(() => OidcStatusMessage = "Sign-in complete. Opening MCP…").ConfigureAwait(true);
            _logger.LogInformation("Fetched MCP default API key after OIDC sign-in; proceeding to main view");
            return keyRes.ApiKey;
        }

        if (keyRes != null && keyRes.WasRejected)
        {
            _logger.LogWarning("Fresh OIDC token was rejected while fetching MCP API key; clearing cached token");
            ClearCachedOidcToken();
        }

        // Bearer-authenticated key fetch failed. Try without bearer — the /api-key
        // endpoint is unprotected so this should succeed even when the OIDC token
        // is not accepted by the server for API key retrieval.
        await DispatchToUiAsync(() => OidcStatusMessage = "Sign-in complete. Opening MCP…").ConfigureAwait(true);
        _logger.LogWarning("Bearer-authenticated /api-key fetch failed; trying default key without bearer token");
        var postOidcDefaultKey = await TryFetchDefaultApiKeyFallbackAsync(mcpBaseUrl, cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(postOidcDefaultKey))
            return postOidcDefaultKey;

        _logger.LogWarning("Could not acquire any MCP API key for {BaseUrl}; proceeding without explicit key", mcpBaseUrl);
        return null;
    }

    /// <summary>
    /// Fetches the default (anonymous) API key from the server's unprotected <c>/api-key</c>
    /// endpoint without sending any bearer token. Returns null on failure.
    /// </summary>
    private async Task<string?> TryFetchDefaultApiKeyFallbackAsync(string mcpBaseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var defaultFetchCmd = new FetchMcpApiKeyCommand(mcpBaseUrl, null);
            var defaultFetchResult = await _dispatcher.SendAsync(defaultFetchCmd, cancellationToken).ConfigureAwait(true);
            var result = defaultFetchResult.IsSuccess ? defaultFetchResult.Value! : new ConnectionApiKeyFetchResult();

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.ApiKey))
            {
                _logger.LogInformation("Default API key fetched from /api-key without bearer auth for {BaseUrl}", mcpBaseUrl);
                return result.ApiKey;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch default API key from /api-key for {BaseUrl}", mcpBaseUrl);
        }

        return null;
    }

    private string? TryReadCachedOidcToken()
    {
        if (_cachedOidcTokenReader == null)
            return null;

        try
        {
            var token = _cachedOidcTokenReader.Invoke();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogInformation("No cached OIDC token available");
                return null;
            }

            _logger.LogInformation("Cached OIDC token found");
            return token.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached OIDC token");
            return null;
        }
    }

    private void WriteCachedOidcToken(string? token)
    {
        if (_cachedOidcTokenWriter == null)
            return;

        try
        {
            _cachedOidcTokenWriter.Invoke(string.IsNullOrWhiteSpace(token) ? null : token.Trim());
            _logger.LogInformation("OIDC token cache updated. TokenPresent={HasToken}", !string.IsNullOrWhiteSpace(token));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update cached OIDC token");
        }
    }

    private void ClearCachedOidcToken() => WriteCachedOidcToken(null);

    private void TryBringAppToForegroundAfterOidcTokenAcquired()
    {
        if (_oidcPostTokenForegroundActivator == null)
            return;

        try
        {
            if (_uiDispatcher.CheckAccess())
            {
                var success = _oidcPostTokenForegroundActivator.Invoke();
                _logger.LogInformation("Requested app foreground after OIDC token acquisition. Success={Success}", success);
                return;
            }

            _uiDispatcher.Post(() =>
            {
                try
                {
                    var success = _oidcPostTokenForegroundActivator.Invoke();
                    _logger.LogInformation("Requested app foreground after OIDC token acquisition. Success={Success}", success);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed requesting app foreground after OIDC token acquisition");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed requesting app foreground after OIDC token acquisition");
        }
    }

    private bool IsJwtExpiredOrNearExpiry(
        string jwtToken,
        TimeSpan skew,
        out DateTimeOffset? expiresAtUtc)
    {
        // Delegate to injected auth service (removes JWT parse/JSON logic from VM per remediation).
        if (_authService != null)
            return _authService.IsJwtExpiredOrNearExpiry(jwtToken, skew, out expiresAtUtc);

        // Fallback (should not normally hit in composed hosts)
        expiresAtUtc = null;
        return false;
    }

    private void DispatchToUi(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiDispatcher.CheckAccess())
            action();
        else
            _uiDispatcher.Post(action);
    }

    private Task DispatchToUiAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiDispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _uiDispatcher.InvokeAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }
}
