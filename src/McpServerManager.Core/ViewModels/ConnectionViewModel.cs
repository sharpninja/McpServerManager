using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.ViewModels;

public sealed record ConnectionEstablishedInfo(string BaseUrl, string? ApiKey, string? BearerToken = null);

public partial class ConnectionViewModel : ViewModelBase
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("ConnectionViewModel");

    [ObservableProperty]
    private string _host = "10.0.2.2";

    [ObservableProperty]
    private string _port = "7147";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isConnecting;

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

    [ObservableProperty]
    private bool _canScanQrCode;

    /// <summary>Raised when the user completes connect (and auth, if required).</summary>
    public event Action<ConnectionEstablishedInfo>? Connected;

    public void SetExternalUrlOpener(Func<string, bool>? externalUrlOpener)
    {
        _externalUrlOpener = externalUrlOpener;
        OidcCanOpenBrowser = !string.IsNullOrWhiteSpace(OidcVerificationUrl) && _externalUrlOpener != null;
        _logger.LogInformation("External URL opener set: {HasOpener}", _externalUrlOpener != null);
    }

    public void SetOidcTokenCacheAccessors(Func<string?>? readCachedToken, Action<string?>? writeCachedToken)
    {
        _cachedOidcTokenReader = readCachedToken;
        _cachedOidcTokenWriter = writeCachedToken;
        _logger.LogInformation(
            "OIDC token cache accessors configured. HasReader={HasReader}, HasWriter={HasWriter}",
            _cachedOidcTokenReader != null,
            _cachedOidcTokenWriter != null);
    }

    public void SetOidcPostTokenForegroundActivator(Func<bool>? foregroundActivator)
    {
        _oidcPostTokenForegroundActivator = foregroundActivator;
        _logger.LogInformation(
            "OIDC post-token foreground activator configured: {HasActivator}",
            _oidcPostTokenForegroundActivator != null);
    }

    public void SetQrCodeScanner(Func<Task<string?>>? scanner)
    {
        _qrCodeScanner = scanner;
        CanScanQrCode = scanner != null;
        _logger.LogInformation("QR code scanner configured: {HasScanner}", scanner != null);
    }

    [RelayCommand]
    private async Task ScanQrCodeAsync()
    {
        if (_qrCodeScanner == null) return;
        try
        {
            var result = await _qrCodeScanner().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                // If the scanned value is a URL, extract just the host
                if (Uri.TryCreate(result, UriKind.Absolute, out var uri))
                {
                    Host = uri.Host;
                    if (uri.Port > 0 && uri.Port != 80 && uri.Port != 443)
                        Port = uri.Port.ToString();
                }
                else
                {
                    Host = result.Trim();
                }
                ErrorMessage = "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QR code scan failed");
            ErrorMessage = $"QR scan failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        _logger.LogInformation("ConnectAsync invoked. Host='{Host}', Port='{Port}', IsConnecting={IsConnecting}", Host, Port, IsConnecting);

        ErrorMessage = "";
        OidcStatusMessage = "";
        OidcUserCode = "";
        OidcVerificationUrl = "";
        OidcCanOpenBrowser = false;
        IsOidcSignInRequired = false;

        if (string.IsNullOrWhiteSpace(Host))
        {
            ErrorMessage = "Host is required.";
            _logger.LogWarning("ConnectAsync validation failed: host missing");
            return;
        }

        if (string.IsNullOrWhiteSpace(Port) || !int.TryParse(Port.Trim(), out var portNumber) || portNumber < 1 || portNumber > 65535)
        {
            ErrorMessage = "Port must be between 1 and 65535.";
            _logger.LogWarning("ConnectAsync validation failed: invalid port '{Port}'", Port);
            return;
        }

        var url = $"http://{Host.Trim()}:{portNumber}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            ErrorMessage = "Invalid host or port.";
            _logger.LogWarning("ConnectAsync validation failed: invalid URL '{Url}'", url);
            return;
        }

        if (IsConnecting)
        {
            _logger.LogInformation("ConnectAsync ignored because connect is already in progress");
            return;
        }

        IsConnecting = true;
        _logger.LogInformation("ConnectAsync started for {Url}", url);

        try
        {
            // Verify the server is reachable. Use a non-redirecting probe so we can
            // detect HTTP→HTTPS upgrades (e.g. ngrok free tier) and use the final
            // scheme. .NET HttpClient strips the Authorization header on scheme-change
            // redirects, which breaks Bearer auth if we keep using the HTTP URL.
            using var probeHandler = new HttpClientHandler { AllowAutoRedirect = false };
            using var probe = new HttpClient(probeHandler) { Timeout = TimeSpan.FromSeconds(5) };
            try
            {
                using var healthResponse = await probe.GetAsync($"{url}/health").ConfigureAwait(true);
                if ((int)healthResponse.StatusCode is >= 301 and <= 308
                    && healthResponse.Headers.Location is { } redirectLocation)
                {
                    var redirected = redirectLocation.IsAbsoluteUri ? redirectLocation : new Uri(new Uri(url), redirectLocation);
                    var upgradedUrl = $"{redirected.Scheme}://{redirected.Host}:{redirected.Port}";
                    _logger.LogInformation(
                        "ConnectAsync: health probe redirected {OriginalUrl} → {UpgradedUrl}; adopting upgraded scheme",
                        url, upgradedUrl);
                    url = upgradedUrl;
                }
            }
            catch (Exception probeEx)
            {
                throw new InvalidOperationException($"Server unreachable at {url}: {probeEx.Message}", probeEx);
            }

            var authToken = await TryAuthenticateWithOidcAsync(url).ConfigureAwait(true);
            _logger.LogInformation("ConnectAsync auth stage complete for {Url}. TokenPresent={HasToken}, BearerTokenPresent={HasBearer}", url, !string.IsNullOrWhiteSpace(authToken), !string.IsNullOrWhiteSpace(_oidcBearerToken));
            Connected?.Invoke(new ConnectionEstablishedInfo(url, authToken, _oidcBearerToken));
            _logger.LogInformation("ConnectAsync raised Connected event for {Url}", url);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsConnecting = false;
            _logger.LogError(ex, "ConnectAsync failed for {Url}", url);
        }
    }

    [RelayCommand]
    private void OpenOidcVerificationUrl()
    {
        if (string.IsNullOrWhiteSpace(OidcVerificationUrl))
        {
            _logger.LogWarning("OpenOidcVerificationUrl ignored: URL is empty");
            return;
        }

        if (_externalUrlOpener == null)
        {
            ErrorMessage = "No browser launcher is available on this device.";
            _logger.LogWarning("OpenOidcVerificationUrl failed: no browser launcher available");
            return;
        }

        _logger.LogInformation("Opening OIDC verification URL via external browser: {Url}", OidcVerificationUrl);
        if (!_externalUrlOpener.Invoke(OidcVerificationUrl))
        {
            ErrorMessage = "Could not open the sign-in page.";
            _logger.LogWarning("OpenOidcVerificationUrl failed to launch browser");
        }
    }

    private async Task<string?> TryAuthenticateWithOidcAsync(string mcpBaseUrl)
    {
        _logger.LogInformation("Checking MCP auth config at {BaseUrl}/auth/config", mcpBaseUrl);
        var authConfig = await McpOidcAuthService.TryGetAuthConfigAsync(mcpBaseUrl).ConfigureAwait(true);
        _logger.LogInformation(
            "MCP auth config result: enabled={Enabled}, clientId={ClientId}, hasDeviceEndpoint={HasDeviceEndpoint}, hasTokenEndpoint={HasTokenEndpoint}",
            authConfig?.Enabled,
            authConfig?.ClientId ?? "<null>",
            !string.IsNullOrWhiteSpace(authConfig?.DeviceAuthorizationEndpoint),
            !string.IsNullOrWhiteSpace(authConfig?.TokenEndpoint));
        if (!McpOidcAuthService.IsEnabled(authConfig))
        {
            _logger.LogInformation("OIDC not enabled/configured for {BaseUrl}; continuing without interactive auth", mcpBaseUrl);
            _oidcBearerToken = null;
            return await TryFetchDefaultApiKeyFallbackAsync(mcpBaseUrl).ConfigureAwait(true);
        }

        var cachedOidcToken = TryReadCachedOidcToken();
        if (!string.IsNullOrWhiteSpace(cachedOidcToken))
        {
            OidcStatusMessage = "Reusing previous sign-in…";
            _logger.LogInformation("Attempting cached OIDC token reuse for {BaseUrl}", mcpBaseUrl);

            var cachedApiKeyResult = await McpOidcAuthService
                .TryFetchMcpApiKeyAsync(mcpBaseUrl, cachedOidcToken)
                .ConfigureAwait(true);

            if (cachedApiKeyResult.IsSuccess)
            {
                OidcStatusMessage = "Opening MCP…";
                _logger.LogInformation("Cached OIDC token reuse succeeded; MCP API key acquired");
                _oidcBearerToken = cachedOidcToken;
                return cachedApiKeyResult.ApiKey;
            }

            if (cachedApiKeyResult.WasRejected)
            {
                OidcStatusMessage = "Session expired. Sign in again.";
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
        var prompt = await McpOidcAuthService
            .StartDeviceAuthorizationAsync(authConfig!, mcpBaseUrl)
            .ConfigureAwait(true);

        IsOidcSignInRequired = true;
        OidcUserCode = prompt.UserCode;
        OidcVerificationUrl = string.IsNullOrWhiteSpace(prompt.VerificationUriComplete)
            ? prompt.VerificationUri
            : prompt.VerificationUriComplete!;
        OidcCanOpenBrowser = !string.IsNullOrWhiteSpace(OidcVerificationUrl) && _externalUrlOpener != null;
        OidcStatusMessage = "Sign in to the identity provider and approve this device.";
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

        var token = await McpOidcAuthService
            .PollForAccessTokenAsync(
                authConfig!,
                prompt,
                mcpBaseUrl,
                status =>
                {
                    OidcStatusMessage = status;
                    _logger.LogInformation("OIDC status update: {Status}", status);
                })
            .ConfigureAwait(true);

        _logger.LogInformation("OIDC sign-in complete. Access token acquired for {BaseUrl}", mcpBaseUrl);
        _oidcBearerToken = token.AccessToken;
        WriteCachedOidcToken(token.AccessToken);
        TryBringAppToForegroundAfterOidcTokenAcquired();

        OidcStatusMessage = "Sign-in complete. Acquiring MCP API key…";
        _logger.LogInformation("Fetching MCP default API key from {BaseUrl}/api-key after OIDC sign-in", mcpBaseUrl);
        var mcpApiKeyResult = await McpOidcAuthService
            .TryFetchMcpApiKeyAsync(mcpBaseUrl, token.AccessToken)
            .ConfigureAwait(true);

        if (mcpApiKeyResult.IsSuccess)
        {
            OidcStatusMessage = "Sign-in complete. Opening MCP…";
            _logger.LogInformation("Fetched MCP default API key after OIDC sign-in; proceeding to main view");
            return mcpApiKeyResult.ApiKey;
        }

        if (mcpApiKeyResult.WasRejected)
        {
            _logger.LogWarning("Fresh OIDC token was rejected while fetching MCP API key; clearing cached token");
            ClearCachedOidcToken();
        }

        // Bearer-authenticated key fetch failed. Try without bearer — the /api-key
        // endpoint is unprotected so this should succeed even when the OIDC token
        // is not accepted by the server for API key retrieval.
        OidcStatusMessage = "Sign-in complete. Opening MCP…";
        _logger.LogWarning("Bearer-authenticated /api-key fetch failed; trying default key without bearer token");
        var postOidcDefaultKey = await TryFetchDefaultApiKeyFallbackAsync(mcpBaseUrl).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(postOidcDefaultKey))
            return postOidcDefaultKey;

        _logger.LogWarning("Could not acquire any MCP API key for {BaseUrl}; proceeding without explicit key", mcpBaseUrl);
        return null;
    }

    /// <summary>
    /// Fetches the default (anonymous) API key from the server's unprotected <c>/api-key</c>
    /// endpoint without sending any bearer token. Returns null on failure.
    /// </summary>
    private async Task<string?> TryFetchDefaultApiKeyFallbackAsync(string mcpBaseUrl)
    {
        try
        {
            var result = await McpOidcAuthService
                .TryFetchMcpApiKeyAsync(mcpBaseUrl, bearerAccessToken: null)
                .ConfigureAwait(true);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.ApiKey))
            {
                _logger.LogInformation("Default API key fetched from /api-key without bearer auth for {BaseUrl}", mcpBaseUrl);
                return result.ApiKey;
            }
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
            var success = _oidcPostTokenForegroundActivator.Invoke();
            _logger.LogInformation("Requested app foreground after OIDC token acquisition. Success={Success}", success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed requesting app foreground after OIDC token acquisition");
        }
    }
}
