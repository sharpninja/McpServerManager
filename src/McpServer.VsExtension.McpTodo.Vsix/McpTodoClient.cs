using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.VsExtension.McpTodo.Models;

namespace McpServerManager.VsExtension.McpTodo;

public sealed class McpTodoClient
{
    private readonly HttpClient _http;
    private readonly string? _workspacePath;
    private readonly string? _apiKey;
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string BaseUrl { get; }


    public McpTodoClient(string baseUrl = "http://localhost:7147", string? solutionDir = null)
    {
        _workspacePath = ResolveWorkspacePath(solutionDir);
        _apiKey = TryReadMarkerValue(_workspacePath, "apiKey");
        var markerBaseUrl = TryReadMarkerValue(_workspacePath, "baseUrl");

        BaseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? markerBaseUrl : baseUrl)?.TrimEnd('/')
                  ?? "http://localhost:7147";
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
            CopilotOutputPane.Log("McpTodoClient configured with X-Api-Key from AGENTS-README-FIRST.yaml");
        }
        else if (!string.IsNullOrWhiteSpace(_workspacePath))
        {
            _http.DefaultRequestHeaders.Add("X-Workspace-Path", _workspacePath);
            CopilotOutputPane.Log($"McpTodoClient configured with X-Workspace-Path='{_workspacePath}'");
        }
    }

    /// <summary>
    /// Checks if the MCP server is reachable. If not, attempts to start it
    /// via Start-McpServer.ps1 and waits for it to become healthy.
    /// </summary>
    /// <returns>True if the server had to be started; false if already running.</returns>
    public async Task<bool> EnsureServerRunningAsync(string? solutionDir = null, CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(cancellationToken).ConfigureAwait(true))
            return false;

        CopilotOutputPane.Log("MCP server is not running. Attempting to start...");

        var scriptPath = FindStartScript(solutionDir);
        if (scriptPath == null)
        {
            CopilotOutputPane.Log("Could not find scripts\\Start-McpServer.ps1. MCP server must be started manually.");
            return false;
        }

        CopilotOutputPane.Log($"Starting MCP server via {scriptPath}");
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -File \"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)) ?? ".",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            CopilotOutputPane.Log($"Failed to start MCP server process: {ex}");
            CopilotOutputPane.Log($"Failed to start MCP server process: {ex.Message}");
            return false;
        }

        // Wait for the server to become healthy (up to ~30 seconds)
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(3000, cancellationToken).ConfigureAwait(true);
            if (await IsHealthyAsync(cancellationToken).ConfigureAwait(true))
            {
                CopilotOutputPane.Log("MCP server started successfully.");
                return true;
            }
        }

        CopilotOutputPane.Log("MCP server did not become healthy within 30 seconds.");
        return false;
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await _http.GetAsync("/health", cts.Token).ConfigureAwait(true);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindStartScript(string? solutionDir)
    {
        if (string.IsNullOrEmpty(solutionDir))
            return null;

        var candidate = Path.Combine(solutionDir!, "scripts", "Start-McpServer.ps1");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? ResolveWorkspacePath(string? solutionDir)
    {
        if (string.IsNullOrWhiteSpace(solutionDir))
            return null;

        return File.Exists(Path.Combine(solutionDir, "AGENTS-README-FIRST.yaml"))
            ? solutionDir
            : null;
    }

    private static string? TryReadMarkerValue(string? workspacePath, string key)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(key))
            return null;

        var markerPath = Path.Combine(workspacePath, "AGENTS-README-FIRST.yaml");
        if (!File.Exists(markerPath))
            return null;

        try
        {
            var content = File.ReadAllText(markerPath);
            var match = Regex.Match(content, $@"^{Regex.Escape(key)}:\s*(.+)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch (IOException ex)
        {
            CopilotOutputPane.Log($"Failed to read '{key}' from marker file: {ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            CopilotOutputPane.Log($"Failed to read '{key}' from marker file: {ex.Message}");
            return null;
        }
    }

    public async Task<TodoQueryResult> GetTodoListAsync(bool? done = false, string? priority = null, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (done.HasValue) query.Add("done=" + done.Value.ToString().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(priority)) query.Add("priority=" + Uri.EscapeDataString(priority!));
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add("keyword=" + Uri.EscapeDataString(keyword!));
        var path = query.Count > 0 ? "/mcpserver/todo?" + string.Join("&", query) : "/mcpserver/todo";
        var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        var result = JsonSerializer.Deserialize<TodoQueryResult>(json, s_jsonOptions);
        return result ?? new TodoQueryResult { Items = new List<TodoFlatItem>(), TotalCount = 0 };
    }

    public async Task<TodoFlatItem?> GetTodoByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = "/mcpserver/todo/" + Uri.EscapeDataString(id);
        var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(true);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        return JsonSerializer.Deserialize<TodoFlatItem>(json, s_jsonOptions);
    }

    public async Task<TodoMutationResult> UpdateTodoAsync(string id, TodoUpdateBody body, CancellationToken cancellationToken = default)
    {
        var path = "/mcpserver/todo/" + Uri.EscapeDataString(id);
        using var content = new StringContent(JsonSerializer.Serialize(body, s_jsonOptions), System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PutAsync(path, content, cancellationToken).ConfigureAwait(true);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        if (!response.IsSuccessStatusCode)
        {
            var err = JsonSerializer.Deserialize<TodoMutationResult>(json, s_jsonOptions);
            return err ?? new TodoMutationResult { Success = false, Error = response.ReasonPhrase ?? response.StatusCode.ToString() };
        }
        return JsonSerializer.Deserialize<TodoMutationResult>(json, s_jsonOptions) ?? new TodoMutationResult { Success = true };
    }
}
