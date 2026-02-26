using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Core.Services;

public sealed class McpAuthConfigResponse
{
    public bool Enabled { get; init; }
    public string? Authority { get; init; }
    public string? ClientId { get; init; }
    public string? Scopes { get; init; }
    public string? DeviceAuthorizationEndpoint { get; init; }
    public string? TokenEndpoint { get; init; }
}

public sealed class OidcDeviceAuthorizationPrompt
{
    public required string DeviceCode { get; init; }
    public required string UserCode { get; init; }
    public required string VerificationUri { get; init; }
    public string? VerificationUriComplete { get; init; }
    public int ExpiresInSeconds { get; init; }
    public int PollIntervalSeconds { get; init; }
}

public sealed class OidcDeviceTokenResult
{
    public required string AccessToken { get; init; }
    public int? ExpiresInSeconds { get; init; }
    public string? TokenType { get; init; }
}

public sealed class McpApiKeyFetchResult
{
    public string? ApiKey { get; init; }
    public bool WasRejected { get; init; }
    public bool IsSuccess => !string.IsNullOrWhiteSpace(ApiKey);
}

internal static class McpOidcAuthService
{
    private static readonly ILogger s_logger = AppLogService.Instance.CreateLogger("McpOidcAuthService");
    // Keycloak/device-code flows can take a long time if the user needs to create an account first.
    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<McpAuthConfigResponse?> TryGetAuthConfigAsync(
        string mcpBaseUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUri = new Uri(McpServerRestClientFactory.NormalizeBaseUrl(mcpBaseUrl), UriKind.Absolute);
            var uri = new Uri(baseUri, "/auth/config");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await s_http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                s_logger.LogWarning("GET /auth/config returned HTTP {StatusCode} ({ReasonPhrase}) at {Uri}", (int)response.StatusCode, response.ReasonPhrase, uri);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = TryParseAuthConfig(body);
            s_logger.LogInformation(
                "GET /auth/config success at {Uri}. enabled={Enabled}, clientId={ClientId}, hasDeviceEndpoint={HasDeviceEndpoint}, hasTokenEndpoint={HasTokenEndpoint}",
                uri,
                parsed?.Enabled,
                parsed?.ClientId ?? "<null>",
                !string.IsNullOrWhiteSpace(parsed?.DeviceAuthorizationEndpoint),
                !string.IsNullOrWhiteSpace(parsed?.TokenEndpoint));
            return parsed;
        }
        catch (Exception ex)
        {
            s_logger.LogWarning(ex, "GET /auth/config failed for {BaseUrl}", mcpBaseUrl);
            return null;
        }
    }

    public static bool IsEnabled(McpAuthConfigResponse? config)
        => config is not null
           && config.Enabled
           && !string.IsNullOrWhiteSpace(config.ClientId)
           && !string.IsNullOrWhiteSpace(config.DeviceAuthorizationEndpoint)
           && !string.IsNullOrWhiteSpace(config.TokenEndpoint);

    public static async Task<OidcDeviceAuthorizationPrompt> StartDeviceAuthorizationAsync(
        McpAuthConfigResponse config,
        string mcpBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(config))
            throw new InvalidOperationException("OIDC is not enabled or auth config is incomplete.");

        var endpoint = RewriteLoopbackEndpointForAndroidEmulator(config.DeviceAuthorizationEndpoint!, mcpBaseUrl);
        s_logger.LogInformation(
            "Starting OIDC device authorization. baseUrl={BaseUrl}, authority={Authority}, endpoint={DeviceAuthorizationEndpoint}, clientId={ClientId}",
            mcpBaseUrl,
            config.Authority ?? "<null>",
            endpoint,
            config.ClientId);
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("client_id", config.ClientId!.Trim())
        };

        if (!string.IsNullOrWhiteSpace(config.Scopes))
            pairs.Add(new("scope", config.Scopes!.Trim()));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(pairs)
        };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await s_http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            s_logger.LogWarning(
                "OIDC device authorization failed. HTTP {StatusCode}, endpoint={Endpoint}, error={Error}",
                (int)response.StatusCode,
                endpoint,
                TryExtractOAuthError(body) ?? body);
            throw new InvalidOperationException($"OIDC device authorization failed: HTTP {(int)response.StatusCode}. {TryExtractOAuthError(body) ?? body}");
        }

        var dto = JsonSerializer.Deserialize<DeviceAuthorizationResponse>(body, s_json)
                  ?? throw new InvalidOperationException("OIDC device authorization returned an empty response.");

        if (string.IsNullOrWhiteSpace(dto.DeviceCode) ||
            string.IsNullOrWhiteSpace(dto.UserCode) ||
            string.IsNullOrWhiteSpace(dto.VerificationUri))
        {
            throw new InvalidOperationException("OIDC device authorization response is missing required fields.");
        }

        var prompt = new OidcDeviceAuthorizationPrompt
        {
            DeviceCode = dto.DeviceCode.Trim(),
            UserCode = dto.UserCode.Trim(),
            VerificationUri = RewriteLoopbackEndpointForAndroidEmulator(dto.VerificationUri.Trim(), mcpBaseUrl),
            VerificationUriComplete = string.IsNullOrWhiteSpace(dto.VerificationUriComplete)
                ? null
                : RewriteLoopbackEndpointForAndroidEmulator(dto.VerificationUriComplete.Trim(), mcpBaseUrl),
            ExpiresInSeconds = dto.ExpiresIn is > 0 ? dto.ExpiresIn.Value : 600,
            PollIntervalSeconds = dto.Interval is > 0 ? dto.Interval.Value : 5
        };
        s_logger.LogInformation(
            "OIDC device authorization prompt received. verificationUri={VerificationUri}, hasVerificationUriComplete={HasVerificationUriComplete}, expiresInSec={ExpiresInSeconds}, pollIntervalSec={PollIntervalSeconds}",
            prompt.VerificationUri,
            !string.IsNullOrWhiteSpace(prompt.VerificationUriComplete),
            prompt.ExpiresInSeconds,
            prompt.PollIntervalSeconds);
        return prompt;
    }

    public static async Task<OidcDeviceTokenResult> PollForAccessTokenAsync(
        McpAuthConfigResponse config,
        OidcDeviceAuthorizationPrompt prompt,
        string mcpBaseUrl,
        Action<string>? onStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(config))
            throw new InvalidOperationException("OIDC is not enabled or auth config is incomplete.");

        var tokenEndpoint = RewriteLoopbackEndpointForAndroidEmulator(config.TokenEndpoint!, mcpBaseUrl);
        var pollDelay = TimeSpan.FromSeconds(Math.Clamp(prompt.PollIntervalSeconds, 1, 30));
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(30, prompt.ExpiresInSeconds));
        var transientNetworkErrorCount = 0;
        s_logger.LogInformation(
            "Starting OIDC token polling. tokenEndpoint={TokenEndpoint}, initialPollDelayMs={PollDelayMs}, timeoutUtc={TimeoutUtc:O}",
            tokenEndpoint,
            (int)pollDelay.TotalMilliseconds,
            expiresAtUtc);

        onStatus?.Invoke("Waiting for sign-in approval…");

        while (DateTime.UtcNow < expiresAtUtc)
        {
            await Task.Delay(pollDelay, cancellationToken).ConfigureAwait(false);

            var pairs = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                new("device_code", prompt.DeviceCode),
                new("client_id", config.ClientId!.Trim())
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(pairs)
            };
            request.Headers.Accept.ParseAdd("application/json");

            HttpResponseMessage? response = null;
            string body;
            try
            {
                response = await s_http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                transientNetworkErrorCount = 0;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                transientNetworkErrorCount++;
                s_logger.LogWarning(
                    "OIDC token polling network timeout/interruption while calling {TokenEndpoint}; transientErrorCount={TransientErrorCount}. Retrying until device code expires.",
                    tokenEndpoint,
                    transientNetworkErrorCount);
                onStatus?.Invoke("Temporary network issue while checking sign-in status. Retrying…");
                continue;
            }
            catch (HttpRequestException ex)
            {
                transientNetworkErrorCount++;
                s_logger.LogWarning(
                    ex,
                    "OIDC token polling request failed at {TokenEndpoint}; transientErrorCount={TransientErrorCount}. Retrying until device code expires.",
                    tokenEndpoint,
                    transientNetworkErrorCount);
                onStatus?.Invoke("Temporary network issue while checking sign-in status. Retrying…");
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var token = JsonSerializer.Deserialize<DeviceTokenResponse>(body, s_json)
                               ?? throw new InvalidOperationException("OIDC token endpoint returned an empty response.");
                    if (string.IsNullOrWhiteSpace(token.AccessToken))
                        throw new InvalidOperationException("OIDC token endpoint response did not include an access token.");

                    s_logger.LogInformation(
                        "OIDC token polling succeeded. tokenType={TokenType}, expiresInSeconds={ExpiresInSeconds}",
                        token.TokenType ?? "<null>",
                        token.ExpiresIn);
                    return new OidcDeviceTokenResult
                    {
                        AccessToken = token.AccessToken.Trim(),
                        ExpiresInSeconds = token.ExpiresIn,
                        TokenType = string.IsNullOrWhiteSpace(token.TokenType) ? null : token.TokenType.Trim()
                    };
                }

                var error = TryExtractOAuthErrorCode(body);
                if (string.Equals(error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
                {
                    s_logger.LogDebug("OIDC token polling pending authorization");
                    onStatus?.Invoke("Open the sign-in page and approve this device.");
                    continue;
                }

                if (string.Equals(error, "slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    pollDelay = pollDelay + TimeSpan.FromSeconds(5);
                    s_logger.LogWarning("OIDC token polling instructed to slow down. NewPollDelayMs={PollDelayMs}", (int)pollDelay.TotalMilliseconds);
                    onStatus?.Invoke("Sign-in provider asked to slow down polling…");
                    continue;
                }

                if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase))
                {
                    s_logger.LogWarning("OIDC token polling denied by identity provider");
                    throw new InvalidOperationException("OIDC sign-in was denied.");
                }

                if (string.Equals(error, "expired_token", StringComparison.OrdinalIgnoreCase))
                {
                    s_logger.LogWarning("OIDC device code expired during token polling");
                    throw new InvalidOperationException("OIDC device code expired. Start sign-in again.");
                }

                s_logger.LogWarning(
                    "OIDC token polling failed. HTTP {StatusCode}, error={Error}, endpoint={TokenEndpoint}",
                    (int)response.StatusCode,
                    TryExtractOAuthError(body) ?? body,
                    tokenEndpoint);
                throw new InvalidOperationException(
                    $"OIDC token polling failed: HTTP {(int)response.StatusCode}. {TryExtractOAuthError(body) ?? body}");
            }
        }

        s_logger.LogWarning("OIDC token polling timed out before authorization completed");
        throw new InvalidOperationException("OIDC sign-in timed out before authorization completed.");
    }

    public static async Task<McpApiKeyFetchResult> TryFetchMcpApiKeyAsync(
        string mcpBaseUrl,
        string? bearerAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUri = new Uri(McpServerRestClientFactory.NormalizeBaseUrl(mcpBaseUrl), UriKind.Absolute);
            var uri = new Uri(baseUri, "/api-key");
            var retryDelay = TimeSpan.FromMilliseconds(250);
            var retryDeadlineUtc = DateTime.UtcNow.AddSeconds(10);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.ParseAdd("application/json");

                if (!string.IsNullOrWhiteSpace(bearerAccessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken.Trim());
                    s_logger.LogInformation("Fetching MCP API key from /api-key using cached/new OIDC bearer token at {Uri}", uri);
                }
                else
                {
                    s_logger.LogInformation("Fetching MCP API key from /api-key without bearer token at {Uri}", uri);
                }

                using var response = await s_http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable &&
                    DateTime.UtcNow < retryDeadlineUtc)
                {
                    s_logger.LogInformation(
                        "GET /api-key returned 503 at {Uri}; retrying in {RetryDelayMs} ms",
                        uri,
                        (int)retryDelay.TotalMilliseconds);
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2d, 1500d));
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    s_logger.LogWarning(
                        "GET /api-key rejected bearer token. HTTP {StatusCode} at {Uri}. error={Error}",
                        (int)response.StatusCode,
                        uri,
                        TryExtractOAuthError(body) ?? body);
                    return new McpApiKeyFetchResult { WasRejected = true };
                }

                if (!response.IsSuccessStatusCode)
                {
                    s_logger.LogWarning(
                        "GET /api-key returned HTTP {StatusCode} ({ReasonPhrase}) at {Uri}",
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        uri);
                    return new McpApiKeyFetchResult();
                }

                var apiKey = TryExtractApiKey(body);
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    s_logger.LogWarning("GET /api-key succeeded at {Uri} but no apiKey property was returned", uri);
                    return new McpApiKeyFetchResult();
                }

                s_logger.LogInformation("GET /api-key succeeded at {Uri}; MCP API key acquired", uri);
                return new McpApiKeyFetchResult { ApiKey = apiKey.Trim() };
            }
        }
        catch (Exception ex)
        {
            s_logger.LogWarning(ex, "GET /api-key failed for {BaseUrl}", mcpBaseUrl);
            return new McpApiKeyFetchResult();
        }
    }

    public static string RewriteLoopbackEndpointForAndroidEmulator(string endpointUrl, string mcpBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
            return endpointUrl;

        if (!Uri.TryCreate(endpointUrl.Trim(), UriKind.Absolute, out var endpointUri))
            return endpointUrl.Trim();

        if (!Uri.TryCreate(McpServerRestClientFactory.NormalizeBaseUrl(mcpBaseUrl), UriKind.Absolute, out var mcpBaseUri))
            return endpointUrl.Trim();

        if (!IsLoopbackHost(endpointUri.Host))
            return endpointUri.ToString();

        var mcpHost = mcpBaseUri.Host;

        // Preserve localhost endpoints on desktop/local scenarios where the MCP host is also loopback.
        if (IsLoopbackHost(mcpHost))
            return endpointUri.ToString();

        // On Android (emulator or physical device), MCP may return localhost OIDC endpoints that must
        // be reached via the same host used for the MCP server connection (e.g. 10.0.2.2 or LAN IP).
        var rewritten = new UriBuilder(endpointUri)
        {
            Host = mcpHost
        };

        return rewritten.Uri.ToString();
    }

    private static bool IsLoopbackHost(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
           || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
           || (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip));

    private static McpAuthConfigResponse? TryParseAuthConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        return new McpAuthConfigResponse
        {
            Enabled = TryGetBool(root, "enabled"),
            Authority = TryGetString(root, "authority"),
            ClientId = TryGetString(root, "clientId"),
            Scopes = TryGetString(root, "scopes"),
            DeviceAuthorizationEndpoint = TryGetString(root, "deviceAuthorizationEndpoint"),
            TokenEndpoint = TryGetString(root, "tokenEndpoint")
        };
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static bool TryGetBool(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
            return true;

        foreach (var property in root.EnumerateObject())
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

    private static string? TryExtractOAuthErrorCode(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "");
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch
        {
            // Ignore parse errors and fall back to raw body.
        }

        return null;
    }

    private static string? TryExtractOAuthError(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "");
            if (doc.RootElement.TryGetProperty("error_description", out var errorDescription))
                return errorDescription.GetString();
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch
        {
            // Ignore parse errors and fall back to raw body.
        }

        return null;
    }

    private static string? TryExtractApiKey(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "");
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (TryGetProperty(doc.RootElement, "apiKey", out var apiKey))
            {
                return apiKey.ValueKind switch
                {
                    JsonValueKind.String => apiKey.GetString(),
                    JsonValueKind.Null => null,
                    _ => apiKey.ToString()
                };
            }
        }
        catch
        {
            // Ignore parse errors and fall back to null.
        }

        return null;
    }

    private sealed class DeviceAuthorizationResponse
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

    private sealed class DeviceTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }
    }
}
