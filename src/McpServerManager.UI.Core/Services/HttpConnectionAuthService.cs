using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// HTTP-backed implementation of MCP connection authentication operations.
/// </summary>
public sealed class HttpConnectionAuthService : IConnectionAuthService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpConnectionAuthService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpConnectionAuthService"/> class.
    /// </summary>
    public HttpConnectionAuthService(ILogger<HttpConnectionAuthService> logger)
        : this(new HttpClient { Timeout = TimeSpan.FromMinutes(30) }, logger)
    {
    }

    internal HttpConnectionAuthService(HttpClient httpClient, ILogger<HttpConnectionAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ProbeHealthAndResolveUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException($"MCP base URL is not valid: {url}");

        using var response = await _httpClient.GetAsync(new Uri(baseUri, "/health"), cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return baseUri.GetLeftPart(UriPartial.Authority);
    }

    /// <inheritdoc />
    public async Task<ConnectionAuthConfig?> TryGetAuthConfigAsync(
        string mcpBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildUri(mcpBaseUrl, "/auth/config", out var uri))
            return null;

        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET /auth/config returned HTTP {StatusCode}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(true);
            var dto = await JsonSerializer.DeserializeAsync<AuthConfigDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(true);
            return dto is null
                ? null
                : new ConnectionAuthConfig
                {
                    Enabled = dto.Enabled,
                    Authority = Normalize(dto.Authority),
                    ClientId = Normalize(dto.ClientId),
                    Scopes = Normalize(dto.Scopes),
                    DeviceAuthorizationEndpoint = Normalize(dto.DeviceAuthorizationEndpoint),
                    TokenEndpoint = Normalize(dto.TokenEndpoint)
                };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GET /auth/config failed for {McpBaseUrl}", mcpBaseUrl);
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsEnabled(ConnectionAuthConfig? config)
        => config is
        {
            Enabled: true,
            ClientId.Length: > 0,
            DeviceAuthorizationEndpoint.Length: > 0,
            TokenEndpoint.Length: > 0
        };

    /// <inheritdoc />
    public Task<bool> TryLogoutAsync(
        string mcpBaseUrl,
        string? authority,
        string? clientId,
        string? accessToken,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc />
    public async Task<ConnectionDeviceAuthorizationPrompt> StartDeviceAuthorizationAsync(
        ConnectionAuthConfig authConfig,
        string mcpBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(authConfig))
            throw new InvalidOperationException("OIDC is not enabled or auth config is incomplete.");

        var pairs = new List<KeyValuePair<string, string>>
        {
            new("client_id", authConfig.ClientId!.Trim())
        };
        if (!string.IsNullOrWhiteSpace(authConfig.Scopes))
            pairs.Add(new("scope", authConfig.Scopes!.Trim()));

        using var request = new HttpRequestMessage(HttpMethod.Post, authConfig.DeviceAuthorizationEndpoint)
        {
            Content = new FormUrlEncodedContent(pairs)
        };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OIDC device authorization failed: HTTP {(int)response.StatusCode}. {TryExtractOAuthError(body) ?? body}");
        }

        var dto = JsonSerializer.Deserialize<DeviceAuthorizationDto>(body, JsonOptions)
                  ?? throw new InvalidOperationException("OIDC device authorization returned an empty response.");
        if (string.IsNullOrWhiteSpace(dto.DeviceCode) ||
            string.IsNullOrWhiteSpace(dto.UserCode) ||
            string.IsNullOrWhiteSpace(dto.VerificationUri))
        {
            throw new InvalidOperationException("OIDC device authorization response is missing required fields.");
        }

        return new ConnectionDeviceAuthorizationPrompt
        {
            DeviceCode = dto.DeviceCode.Trim(),
            UserCode = dto.UserCode.Trim(),
            VerificationUri = dto.VerificationUri.Trim(),
            VerificationUriComplete = Normalize(dto.VerificationUriComplete),
            ExpiresInSeconds = dto.ExpiresIn is > 0 ? dto.ExpiresIn.Value : 600,
            PollIntervalSeconds = dto.Interval is > 0 ? dto.Interval.Value : 5
        };
    }

    /// <inheritdoc />
    public async Task<ConnectionDeviceTokenResult> PollForAccessTokenAsync(
        ConnectionAuthConfig authConfig,
        ConnectionDeviceAuthorizationPrompt prompt,
        string mcpBaseUrl,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(authConfig))
            throw new InvalidOperationException("OIDC is not enabled or auth config is incomplete.");

        var pollDelay = TimeSpan.FromSeconds(Math.Clamp(prompt.PollIntervalSeconds, 1, 30));
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(30, prompt.ExpiresInSeconds));
        statusCallback?.Invoke("Waiting for sign-in approval...");

        while (DateTime.UtcNow < expiresAtUtc)
        {
            await Task.Delay(pollDelay, cancellationToken).ConfigureAwait(true);

            var pairs = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                new("client_id", authConfig.ClientId!.Trim()),
                new("device_code", prompt.DeviceCode)
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, authConfig.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(pairs)
            };
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(true);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
            if (response.IsSuccessStatusCode)
            {
                var dto = JsonSerializer.Deserialize<DeviceTokenDto>(body, JsonOptions)
                          ?? throw new InvalidOperationException("OIDC token endpoint returned an empty response.");
                if (string.IsNullOrWhiteSpace(dto.AccessToken))
                    throw new InvalidOperationException("OIDC token endpoint response did not include an access token.");

                return new ConnectionDeviceTokenResult
                {
                    AccessToken = dto.AccessToken.Trim(),
                    ExpiresInSeconds = dto.ExpiresIn,
                    TokenType = Normalize(dto.TokenType)
                };
            }

            var error = TryExtractOAuthErrorCode(body);
            if (string.Equals(error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                statusCallback?.Invoke("Open the sign-in page and approve this device.");
                continue;
            }

            if (string.Equals(error, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                pollDelay += TimeSpan.FromSeconds(5);
                statusCallback?.Invoke("Sign-in provider asked to slow down polling...");
                continue;
            }

            if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OIDC sign-in was denied.");

            if (string.Equals(error, "expired_token", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OIDC device code expired. Start sign-in again.");

            throw new InvalidOperationException(
                $"OIDC token polling failed: HTTP {(int)response.StatusCode}. {TryExtractOAuthError(body) ?? body}");
        }

        throw new InvalidOperationException("OIDC sign-in timed out before authorization completed.");
    }

    /// <inheritdoc />
    public async Task<ConnectionApiKeyFetchResult> TryFetchMcpApiKeyAsync(
        string mcpBaseUrl,
        string? bearerAccessToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildUri(mcpBaseUrl, "/api-key", out var uri))
            return new ConnectionApiKeyFetchResult();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");
            if (!string.IsNullOrWhiteSpace(bearerAccessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken.Trim());

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(true);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new ConnectionApiKeyFetchResult { WasRejected = true };

            if (!response.IsSuccessStatusCode)
                return new ConnectionApiKeyFetchResult();

            return new ConnectionApiKeyFetchResult { ApiKey = TryExtractApiKey(body) };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GET /api-key failed for {McpBaseUrl}", mcpBaseUrl);
            return new ConnectionApiKeyFetchResult();
        }
    }

    /// <inheritdoc />
    public bool IsJwtExpiredOrNearExpiry(string jwtToken, TimeSpan skew, out DateTimeOffset? expiresAtUtc)
    {
        expiresAtUtc = TryGetJwtExpiry(jwtToken);
        return expiresAtUtc is not { } expiry || expiry <= DateTimeOffset.UtcNow.Add(skew);
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();

    private static bool TryBuildUri(string mcpBaseUrl, string path, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(mcpBaseUrl, UriKind.Absolute, out var baseUri))
            return false;

        uri = new Uri(baseUri, path);
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TryExtractOAuthErrorCode(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            return TryGetProperty(doc.RootElement, "error", out var error) ? error.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractOAuthError(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            if (TryGetProperty(doc.RootElement, "error_description", out var description))
                return description.GetString();
            if (TryGetProperty(doc.RootElement, "error", out var error))
                return error.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? TryExtractApiKey(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            return TryGetProperty(doc.RootElement, "apiKey", out var apiKey)
                ? apiKey.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? TryGetJwtExpiry(string jwtToken)
    {
        try
        {
            var parts = jwtToken.Split('.');
            if (parts.Length < 2)
                return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = (payload.Length % 4) switch
            {
                2 => payload + "==",
                3 => payload + "=",
                _ => payload
            };
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return TryGetProperty(doc.RootElement, "exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed class AuthConfigDto
    {
        public bool Enabled { get; init; }

        public string? Authority { get; init; }

        public string? ClientId { get; init; }

        public string? Scopes { get; init; }

        public string? DeviceAuthorizationEndpoint { get; init; }

        public string? TokenEndpoint { get; init; }
    }

    private sealed class DeviceAuthorizationDto
    {
        [JsonPropertyName("device_code")]
        public string? DeviceCode { get; init; }

        [JsonPropertyName("user_code")]
        public string? UserCode { get; init; }

        [JsonPropertyName("verification_uri")]
        public string? VerificationUri { get; init; }

        [JsonPropertyName("verification_uri_complete")]
        public string? VerificationUriComplete { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }

        [JsonPropertyName("interval")]
        public int? Interval { get; init; }
    }

    private sealed class DeviceTokenDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}
