using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Core.Services;

/// <summary>Client for MCP workspace-management endpoints.</summary>
public sealed class McpWorkspaceService
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("WorkspaceService");
    private readonly HttpClient _httpClient;

    public McpWorkspaceService(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>List workspaces.</summary>
    public async Task<McpWorkspaceQueryResult> QueryAsync(CancellationToken cancellationToken = default)
    {
        return await GetFreshJsonAsync<McpWorkspaceQueryResult>("/mcp/workspace", cancellationToken).ConfigureAwait(true)
               ?? new McpWorkspaceQueryResult();
    }

    /// <summary>Get a single workspace by key.</summary>
    public async Task<McpWorkspaceItem?> GetByIdAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetFreshJsonAsync<McpWorkspaceItem>($"/mcp/workspace/{Uri.EscapeDataString(key)}", cancellationToken)
            .ConfigureAwait(true);
    }

    /// <summary>Create a workspace.</summary>
    public async Task<McpWorkspaceMutationResult> CreateAsync(McpWorkspaceCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/mcp/workspace", request, cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceMutationResult>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Update an existing workspace.</summary>
    public async Task<McpWorkspaceMutationResult> UpdateAsync(string key, McpWorkspaceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/mcp/workspace/{Uri.EscapeDataString(key)}", request, cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceMutationResult>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Delete a workspace.</summary>
    public async Task<McpWorkspaceMutationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"/mcp/workspace/{Uri.EscapeDataString(key)}", cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceMutationResult>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Read current process status for a workspace.</summary>
    public async Task<McpWorkspaceProcessStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetFreshJsonAsync<McpWorkspaceProcessStatus>($"/mcp/workspace/{Uri.EscapeDataString(key)}/status", cancellationToken)
            .ConfigureAwait(true) ?? new McpWorkspaceProcessStatus();
    }

    /// <summary>Initialize workspace files.</summary>
    public async Task<McpWorkspaceInitResult> InitAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"/mcp/workspace/{Uri.EscapeDataString(key)}/init", null, cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceInitResult>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Start workspace process.</summary>
    public async Task<McpWorkspaceProcessStatus> StartAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"/mcp/workspace/{Uri.EscapeDataString(key)}/start", null, cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceProcessStatus>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Stop workspace process.</summary>
    public async Task<McpWorkspaceProcessStatus> StopAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"/mcp/workspace/{Uri.EscapeDataString(key)}/stop", null, cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceProcessStatus>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Read the global marker prompt template (primary workspace only).</summary>
    public async Task<McpWorkspaceGlobalPromptResult> GetGlobalPromptAsync(CancellationToken cancellationToken = default)
    {
        return await GetFreshJsonAsync<McpWorkspaceGlobalPromptResult>("/mcp/workspace/prompt", cancellationToken)
            .ConfigureAwait(true) ?? new McpWorkspaceGlobalPromptResult();
    }

    /// <summary>Update the global marker prompt template (primary workspace only).</summary>
    public async Task<McpWorkspaceGlobalPromptResult> UpdateGlobalPromptAsync(
        McpWorkspaceGlobalPromptUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync("/mcp/workspace/prompt", request, cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceGlobalPromptResult>(response, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Probe the selected workspace health endpoint.</summary>
    public async Task<McpWorkspaceHealthResult> GetHealthAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new McpWorkspaceHealthResult { Success = false, Error = "Workspace key is required." };

        var workspace = await GetByIdAsync(key, cancellationToken).ConfigureAwait(true);
        if (workspace == null)
            return new McpWorkspaceHealthResult { Success = false, Error = $"Workspace '{key}' not found." };

        if (workspace.WorkspacePort is < 1 or > 65535)
            return new McpWorkspaceHealthResult
            {
                Success = false,
                Error = $"Workspace '{key}' has invalid port '{workspace.WorkspacePort}'."
            };

        var baseUri = _httpClient.BaseAddress;
        if (baseUri == null)
            return new McpWorkspaceHealthResult { Success = false, Error = "Workspace service base address is not configured." };

        // Common health endpoints. Try /health first, then /mcp/health.
        var candidates = new[] { "/health", "/mcp/health" };
        McpWorkspaceHealthResult? lastResult = null;
        foreach (var candidate in candidates)
        {
            lastResult = await ProbeHealthAsync(baseUri, workspace.WorkspacePort, candidate, cancellationToken).ConfigureAwait(true);
            if (lastResult.Success || lastResult.StatusCode != 404)
                return lastResult;
        }

        return lastResult ?? new McpWorkspaceHealthResult { Success = false, Error = "No health endpoint response." };
    }

    /// <summary>Probe health for this service base URL (selected connection target).</summary>
    public async Task<McpWorkspaceHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var baseUri = _httpClient.BaseAddress;
        if (baseUri == null)
            return new McpWorkspaceHealthResult { Success = false, Error = "Workspace service base address is not configured." };

        var candidates = new[] { "/health", "/mcp/health" };
        McpWorkspaceHealthResult? lastResult = null;
        foreach (var candidate in candidates)
        {
            lastResult = await ProbeHealthAtBaseAsync(baseUri, candidate, cancellationToken).ConfigureAwait(true);
            if (lastResult.Success || lastResult.StatusCode != 404)
                return lastResult;
        }

        return lastResult ?? new McpWorkspaceHealthResult { Success = false, Error = "No health endpoint response." };
    }

    private static async Task<T> ReadJsonOrDefaultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : new()
    {
        response.EnsureSuccessStatusCode();
        if (response.Content == null)
            return new T();

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(true) ?? new T();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"[WorkspaceService] JSON deserialization failed for {typeof(T).Name} (HTTP {(int)response.StatusCode}); returning default");
            return new T();
        }
    }

    private async Task<T?> GetFreshJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var request = CreateNoCacheGet(url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(true);
    }

    private static HttpRequestMessage CreateNoCacheGet(string url)
    {
        var separator = url.Contains('?') ? '&' : '?';
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}{separator}_rt={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        ApplyNoCacheHeaders(request.Headers);
        return request;
    }

    private static HttpRequestMessage CreateNoCacheGet(Uri uri)
    {
        var builder = new UriBuilder(uri);
        var existingQuery = builder.Query.TrimStart('?');
        var stamp = $"_rt={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        builder.Query = string.IsNullOrWhiteSpace(existingQuery)
            ? stamp
            : $"{existingQuery}&{stamp}";

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        ApplyNoCacheHeaders(request.Headers);
        return request;
    }

    private static void ApplyNoCacheHeaders(HttpRequestHeaders headers)
    {
        if (headers == null) return;
        headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        headers.Pragma.ParseAdd("no-cache");
    }

    private Task<McpWorkspaceHealthResult> ProbeHealthAtBaseAsync(
        Uri baseUri,
        string healthPath,
        CancellationToken cancellationToken)
    {
        return ProbeHealthAsync(baseUri, baseUri.Port, healthPath, cancellationToken);
    }

    private async Task<McpWorkspaceHealthResult> ProbeHealthAsync(
        Uri baseUri,
        int workspacePort,
        string healthPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var uriBuilder = new UriBuilder(baseUri)
            {
                Port = workspacePort,
                Path = healthPath.TrimStart('/')
            };

            using var request = CreateNoCacheGet(uriBuilder.Uri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(true);
            var body = response.Content == null
                ? ""
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);

            return new McpWorkspaceHealthResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Url = uriBuilder.Uri.ToString(),
                Body = body
            };
        }
        catch (Exception ex)
        {
            return new McpWorkspaceHealthResult
            {
                Success = false,
                StatusCode = 0,
                Url = null,
                Body = "",
                Error = ex.Message
            };
        }
    }
}
