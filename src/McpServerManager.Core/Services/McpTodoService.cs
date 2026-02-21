using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

public sealed class McpTodoService
{
    private readonly HttpClient _httpClient;

    public McpTodoService(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>List / filter todos.</summary>
    public async Task<McpTodoQueryResult> QueryAsync(
        string? keyword = null,
        string? priority = null,
        string? section = null,
        string? id = null,
        bool? done = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (!string.IsNullOrWhiteSpace(priority)) query.Add($"priority={Uri.EscapeDataString(priority)}");
        if (!string.IsNullOrWhiteSpace(section)) query.Add($"section={Uri.EscapeDataString(section)}");
        if (!string.IsNullOrWhiteSpace(id)) query.Add($"id={Uri.EscapeDataString(id)}");
        if (done.HasValue) query.Add($"done={done.Value.ToString().ToLowerInvariant()}");

        var url = "/mcp/todo";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await _httpClient.GetFromJsonAsync<McpTodoQueryResult>(url, cancellationToken).ConfigureAwait(true)
               ?? new McpTodoQueryResult();
    }

    /// <summary>Get a single todo by id.</summary>
    public async Task<McpTodoFlatItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<McpTodoFlatItem>($"/mcp/todo/{Uri.EscapeDataString(id)}", cancellationToken)
            .ConfigureAwait(true);
    }

    /// <summary>Create a new todo.</summary>
    public async Task<McpTodoMutationResult> CreateAsync(McpTodoCreateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/mcp/todo", request, cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<McpTodoMutationResult>(cancellationToken: cancellationToken).ConfigureAwait(true)
               ?? new McpTodoMutationResult();
    }

    /// <summary>Update an existing todo (partial update).</summary>
    public async Task<McpTodoMutationResult> UpdateAsync(string id, McpTodoUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/mcp/todo/{Uri.EscapeDataString(id)}", request, cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<McpTodoMutationResult>(cancellationToken: cancellationToken).ConfigureAwait(true)
               ?? new McpTodoMutationResult();
    }

    /// <summary>Delete a todo by id.</summary>
    public async Task<McpTodoMutationResult> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/mcp/todo/{Uri.EscapeDataString(id)}", cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<McpTodoMutationResult>(cancellationToken: cancellationToken).ConfigureAwait(true)
               ?? new McpTodoMutationResult();
    }

    /// <summary>Run AI requirements analysis on a todo.</summary>
    public async Task<McpRequirementsAnalysisResult> AnalyzeRequirementsAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"/mcp/todo/{Uri.EscapeDataString(id)}/requirements", null, cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<McpRequirementsAnalysisResult>(cancellationToken: cancellationToken).ConfigureAwait(true)
               ?? new McpRequirementsAnalysisResult();
    }
}
