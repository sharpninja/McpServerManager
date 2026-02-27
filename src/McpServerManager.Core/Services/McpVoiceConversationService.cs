using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

/// <summary>
/// Typed client for MCP voice conversation REST endpoints.
/// Auth tokens are set once at construction; no runtime resolution.
/// </summary>
public sealed class McpVoiceConversationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _baseUrl;
    private readonly string? _apiKey;
    private readonly string? _bearerToken;

    /// <summary>
    /// Creates a new MCP voice conversation client with pre-resolved auth.
    /// </summary>
    public McpVoiceConversationService(string baseUrl, string? apiKey = null, string? bearerToken = null)
    {
        _baseUrl = McpServerRestClientFactory.NormalizeBaseUrl(baseUrl);
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _bearerToken = string.IsNullOrWhiteSpace(bearerToken) ? null : bearerToken.Trim();
    }

    /// <summary>
    /// Creates a voice session on the MCP server.
    /// </summary>
    public async Task<McpVoiceSessionCreateResponse> CreateSessionAsync(
        McpVoiceSessionCreateRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateAuthorizedClientAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(true);
        using var response = await client.PostAsJsonAsync("mcp/voice/session", request ?? new McpVoiceSessionCreateRequest(), JsonOptions, cancellationToken).ConfigureAwait(true);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(true);
        return (await response.Content.ReadFromJsonAsync<McpVoiceSessionCreateResponse>(JsonOptions, cancellationToken).ConfigureAwait(true))
            ?? throw new InvalidOperationException("MCP voice create session returned an empty response.");
    }

    /// <summary>
    /// Submits a transcribed turn to the MCP voice session.
    /// </summary>
    public async Task<McpVoiceTurnResponse> SubmitTurnAsync(
        string sessionId,
        McpVoiceTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        using var client = await CreateAuthorizedClientAsync(TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(true);
        using var response = await client.PostAsJsonAsync(
            $"mcp/voice/session/{Uri.EscapeDataString(sessionId)}/turn",
            request,
            JsonOptions,
            cancellationToken).ConfigureAwait(true);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(true);
        return (await response.Content.ReadFromJsonAsync<McpVoiceTurnResponse>(JsonOptions, cancellationToken).ConfigureAwait(true))
            ?? throw new InvalidOperationException("MCP voice submit turn returned an empty response.");
    }

    /// <summary>
    /// Interrupts an active turn for a voice session.
    /// </summary>
    public async Task<McpVoiceInterruptResponse> InterruptAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using var client = await CreateAuthorizedClientAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(true);
        using var response = await client.PostAsync($"mcp/voice/session/{Uri.EscapeDataString(sessionId)}/interrupt", content: null, cancellationToken).ConfigureAwait(true);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(true);
        return (await response.Content.ReadFromJsonAsync<McpVoiceInterruptResponse>(JsonOptions, cancellationToken).ConfigureAwait(true))
            ?? throw new InvalidOperationException("MCP voice interrupt returned an empty response.");
    }

    /// <summary>
    /// Gets a voice session status snapshot.
    /// </summary>
    public async Task<McpVoiceSessionStatus> GetStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using var client = await CreateAuthorizedClientAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(true);
        using var response = await client.GetAsync($"mcp/voice/session/{Uri.EscapeDataString(sessionId)}", cancellationToken).ConfigureAwait(true);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(true);
        return (await response.Content.ReadFromJsonAsync<McpVoiceSessionStatus>(JsonOptions, cancellationToken).ConfigureAwait(true))
            ?? throw new InvalidOperationException("MCP voice status returned an empty response.");
    }

    /// <summary>
    /// Gets the transcript entries for a voice session.
    /// </summary>
    public async Task<McpVoiceTranscriptResponse> GetTranscriptAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using var client = await CreateAuthorizedClientAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(true);
        using var response = await client.GetAsync($"mcp/voice/session/{Uri.EscapeDataString(sessionId)}/transcript", cancellationToken).ConfigureAwait(true);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(true);
        return (await response.Content.ReadFromJsonAsync<McpVoiceTranscriptResponse>(JsonOptions, cancellationToken).ConfigureAwait(true))
            ?? throw new InvalidOperationException("MCP voice transcript returned an empty response.");
    }

    /// <summary>
    /// Deletes a voice session and any associated in-memory state.
    /// </summary>
    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using var client = await CreateAuthorizedClientAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(true);
        using var response = await client.DeleteAsync($"mcp/voice/session/{Uri.EscapeDataString(sessionId)}", cancellationToken).ConfigureAwait(true);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(true);
    }

    private Task<HttpClient> CreateAuthorizedClientAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl.TrimEnd('/') + "/", UriKind.Absolute),
            Timeout = timeout
        };

        // Bearer takes precedence (mutual exclusivity with API key)
        if (!string.IsNullOrWhiteSpace(_bearerToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        else if (!string.IsNullOrWhiteSpace(_apiKey))
            client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return Task.FromResult(client);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = response.Content is null
            ? string.Empty
            : (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true)).Trim();

        var message = response.StatusCode switch
        {
            HttpStatusCode.NotFound => "MCP voice endpoint or session was not found.",
            HttpStatusCode.Unauthorized => "Unauthorized to call MCP voice endpoint (API key missing/invalid).",
            HttpStatusCode.BadRequest => string.IsNullOrWhiteSpace(body) ? "MCP voice request was invalid." : $"MCP voice request was invalid: {body}",
            HttpStatusCode.ServiceUnavailable => string.IsNullOrWhiteSpace(body) ? "MCP voice service is unavailable." : $"MCP voice service unavailable: {body}",
            _ => string.IsNullOrWhiteSpace(body)
                ? $"MCP voice request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
                : $"MCP voice request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}"
        };

        throw new InvalidOperationException(message);
    }
}
