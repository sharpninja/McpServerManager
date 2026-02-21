using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

/// <summary>Client for MCP workspace-management endpoints.</summary>
public sealed class McpWorkspaceService
{
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
        return await _httpClient.GetFromJsonAsync<McpWorkspaceQueryResult>("/mcp/workspace", cancellationToken).ConfigureAwait(true)
               ?? new McpWorkspaceQueryResult();
    }

    /// <summary>Get a single workspace by key.</summary>
    public async Task<McpWorkspaceItem?> GetByIdAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<McpWorkspaceItem>($"/mcp/workspace/{Uri.EscapeDataString(key)}", cancellationToken)
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
        using var response = await _httpClient.GetAsync($"/mcp/workspace/{Uri.EscapeDataString(key)}/status", cancellationToken).ConfigureAwait(true);
        return await ReadJsonOrDefaultAsync<McpWorkspaceProcessStatus>(response, cancellationToken).ConfigureAwait(true);
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
        catch
        {
            return new T();
        }
    }
}
