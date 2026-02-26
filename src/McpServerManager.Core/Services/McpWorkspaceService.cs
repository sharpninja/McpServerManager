using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpServer.Client;
using McpServerManager.Core.Models;
using Microsoft.Extensions.Logging;
using ClientModels = McpServer.Client.Models;

namespace McpServerManager.Core.Services;

/// <summary>Client for MCP workspace-management endpoints.</summary>
public sealed class McpWorkspaceService
{
    private static readonly ILogger _logger = AppLogService.Instance.CreateLogger("WorkspaceService");
    private readonly McpServerClient _client;
    private readonly Uri _baseUri;
    private readonly HttpClient _healthHttpClient;

    public McpWorkspaceService(string baseUrl, string? apiKey = null, string? workspaceRootPath = null, string? bearerToken = null)
    {
        _client = McpServerRestClientFactory.Create(
            baseUrl,
            timeout: TimeSpan.FromSeconds(5),
            apiKey: apiKey,
            workspaceRootPath: workspaceRootPath,
            bearerToken: bearerToken);

        var normalizedBaseUrl = McpServerRestClientFactory.NormalizeBaseUrl(baseUrl);
        _baseUri = new Uri(normalizedBaseUrl, UriKind.Absolute);
        _healthHttpClient = new HttpClient
        {
            BaseAddress = _baseUri,
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>List workspaces.</summary>
    public async Task<McpWorkspaceQueryResult> QueryAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.ListAsync(cancellationToken).ConfigureAwait(true);
        return new McpWorkspaceQueryResult
        {
            TotalCount = result.TotalCount,
            Items = result.Items?.Select(Map).ToList() ?? new()
        };
    }

    /// <summary>Get a single workspace by key.</summary>
    public async Task<McpWorkspaceItem?> GetByIdAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.GetAsync(EncodeKey(key), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Create a workspace.</summary>
    public async Task<McpWorkspaceMutationResult> CreateAsync(McpWorkspaceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.CreateAsync(Map(request), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Update an existing workspace.</summary>
    public async Task<McpWorkspaceMutationResult> UpdateAsync(string key, McpWorkspaceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.UpdateAsync(EncodeKey(key), Map(request), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Delete a workspace.</summary>
    public async Task<McpWorkspaceMutationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.DeleteAsync(EncodeKey(key), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Read current process status for a workspace.</summary>
    public async Task<McpWorkspaceProcessStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.GetStatusAsync(EncodeKey(key), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Initialize workspace files.</summary>
    public async Task<McpWorkspaceInitResult> InitAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.InitAsync(EncodeKey(key), cancellationToken).ConfigureAwait(true);
        return new McpWorkspaceInitResult
        {
            Success = result.Success,
            Error = result.Error,
            FilesCreated = result.FilesCreated?.ToList()
        };
    }

    /// <summary>Start workspace process.</summary>
    public async Task<McpWorkspaceProcessStatus> StartAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.StartAsync(EncodeKey(key), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Stop workspace process.</summary>
    public async Task<McpWorkspaceProcessStatus> StopAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.StopAsync(EncodeKey(key), cancellationToken).ConfigureAwait(true);
        return Map(result);
    }

    /// <summary>Read the global marker prompt template (primary workspace only).</summary>
    public async Task<McpWorkspaceGlobalPromptResult> GetGlobalPromptAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.GetGlobalPromptAsync(cancellationToken).ConfigureAwait(true);
        return new McpWorkspaceGlobalPromptResult
        {
            Template = result.Template,
            IsDefault = result.IsDefault
        };
    }

    /// <summary>Update the global marker prompt template (primary workspace only).</summary>
    public async Task<McpWorkspaceGlobalPromptResult> UpdateGlobalPromptAsync(
        McpWorkspaceGlobalPromptUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.Workspace.UpdateGlobalPromptAsync(
            new ClientModels.GlobalPromptUpdateRequest { Template = request.Template },
            cancellationToken).ConfigureAwait(true);

        return new McpWorkspaceGlobalPromptResult
        {
            Template = result.Template,
            IsDefault = result.IsDefault
        };
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
        {
            return new McpWorkspaceHealthResult
            {
                Success = false,
                Error = $"Workspace '{key}' has invalid port '{workspace.WorkspacePort}'."
            };
        }

        var candidates = new[] { "/health", "/mcp/health" };
        McpWorkspaceHealthResult? lastResult = null;
        foreach (var candidate in candidates)
        {
            lastResult = await ProbeHealthAsync(_baseUri, workspace.WorkspacePort, candidate, cancellationToken).ConfigureAwait(true);
            if (lastResult.Success || lastResult.StatusCode != 404)
                return lastResult;
        }

        return lastResult ?? new McpWorkspaceHealthResult { Success = false, Error = "No health endpoint response." };
    }

    /// <summary>Probe health for this service base URL (selected connection target).</summary>
    public async Task<McpWorkspaceHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new[] { "/health", "/mcp/health" };
        McpWorkspaceHealthResult? lastResult = null;
        foreach (var candidate in candidates)
        {
            lastResult = await ProbeHealthAtBaseAsync(_baseUri, candidate, cancellationToken).ConfigureAwait(true);
            if (lastResult.Success || lastResult.StatusCode != 404)
                return lastResult;
        }

        return lastResult ?? new McpWorkspaceHealthResult { Success = false, Error = "No health endpoint response." };
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
            using var response = await _healthHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

    private static HttpRequestMessage CreateNoCacheGet(Uri uri)
    {
        var builder = new UriBuilder(uri);
        var existingQuery = builder.Query.TrimStart('?');
        var stamp = $"_rt={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        builder.Query = string.IsNullOrWhiteSpace(existingQuery)
            ? stamp
            : $"{existingQuery}&{stamp}";

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        request.Headers.Pragma.ParseAdd("no-cache");
        return request;
    }

    private static McpWorkspaceMutationResult Map(ClientModels.WorkspaceMutationResult result)
    {
        return new McpWorkspaceMutationResult
        {
            Success = result.Success,
            Error = result.Error,
            Workspace = result.Workspace == null ? null : Map(result.Workspace)
        };
    }

    private static McpWorkspaceProcessStatus Map(ClientModels.WorkspaceProcessStatus result)
    {
        return new McpWorkspaceProcessStatus
        {
            IsRunning = result.IsRunning,
            Pid = result.Pid,
            Uptime = result.Uptime,
            Port = result.Port,
            Error = result.Error
        };
    }

    private static McpWorkspaceItem Map(ClientModels.WorkspaceDto item)
    {
        return new McpWorkspaceItem
        {
            WorkspacePath = item.WorkspacePath,
            Name = item.Name,
            TodoPath = item.TodoPath,
            DataDirectory = item.DataDirectory,
            WorkspacePort = item.WorkspacePort,
            TunnelProvider = item.TunnelProvider,
            IsPrimary = item.IsPrimary,
            IsEnabled = item.IsEnabled,
            DateTimeCreated = item.DateTimeCreated,
            DateTimeModified = item.DateTimeModified,
            RunAs = item.RunAs,
            PromptTemplate = item.PromptTemplate,
            StatusPrompt = item.StatusPrompt,
            ImplementPrompt = item.ImplementPrompt,
            PlanPrompt = item.PlanPrompt
        };
    }

    private static ClientModels.WorkspaceCreateRequest Map(McpWorkspaceCreateRequest request)
    {
        return new ClientModels.WorkspaceCreateRequest
        {
            WorkspacePath = request.WorkspacePath ?? string.Empty,
            Name = request.Name,
            WorkspacePort = request.WorkspacePort ?? 0,
            TodoPath = request.TodoPath,
            DataDirectory = request.DataDirectory,
            TunnelProvider = request.TunnelProvider,
            RunAs = request.RunAs,
            IsPrimary = request.IsPrimary ?? false,
            IsEnabled = request.IsEnabled ?? true,
            PromptTemplate = request.PromptTemplate,
            StatusPrompt = request.StatusPrompt,
            ImplementPrompt = request.ImplementPrompt,
            PlanPrompt = request.PlanPrompt
        };
    }

    private static ClientModels.WorkspaceUpdateRequest Map(McpWorkspaceUpdateRequest request)
    {
        return new ClientModels.WorkspaceUpdateRequest
        {
            Name = request.Name,
            TodoPath = request.TodoPath,
            DataDirectory = request.DataDirectory,
            WorkspacePort = request.WorkspacePort,
            TunnelProvider = request.TunnelProvider,
            RunAs = request.RunAs,
            IsPrimary = request.IsPrimary,
            IsEnabled = request.IsEnabled,
            PromptTemplate = request.PromptTemplate,
            StatusPrompt = request.StatusPrompt,
            ImplementPrompt = request.ImplementPrompt,
            PlanPrompt = request.PlanPrompt
        };
    }

    /// <summary>Base64URL-encode a raw workspace path for use as an API route key.</summary>
    private static string EncodeKey(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
