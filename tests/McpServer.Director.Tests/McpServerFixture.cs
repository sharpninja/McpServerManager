using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace McpServerManager.Director.Tests;

/// <summary>
/// xUnit fixture that launches the MCP server (<c>McpServer.Support.Mcp</c>) on a random
/// available port, creates a temporary workspace directory with an
/// <c>AGENTS-README-FIRST.yaml</c> marker file, and tears everything down on dispose.
/// </summary>
public sealed class McpServerFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private string? _workspaceDir;
    private int _port;

    /// <summary>The temporary workspace directory containing the marker file.</summary>
    public string WorkspaceDir => _workspaceDir ?? throw new InvalidOperationException("Fixture not initialized.");

    /// <summary>The port the MCP server is listening on.</summary>
    public int Port => _port;

    /// <summary>The base URL of the running MCP server.</summary>
    public string BaseUrl => $"http://localhost:{_port}";

    /// <summary>Path to the built MCP server DLL.</summary>
    private static readonly string McpServerDll = Path.GetFullPath(
        Path.Combine(DirectorRunner.RepoRoot, "lib", "McpServer", "src",
            "McpServer.Support.Mcp", "bin", "Debug", "net9.0", "McpServer.Support.Mcp.dll"));

    public async ValueTask InitializeAsync()
    {
        _port = GetAvailablePort();
        _workspaceDir = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspaceDir);

        WriteAppSettings(_workspaceDir, _port);

        // Start the MCP server process.
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{McpServerDll}\"",
            WorkingDirectory = _workspaceDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Test";
        psi.Environment["PORT"] = _port.ToString();
        psi.Environment["NO_COLOR"] = "1";

        _serverProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start MCP server process.");

        // Consume stdout/stderr asynchronously so the process doesn't block on buffer limits.
        var stdoutBuilder = new System.Text.StringBuilder();
        var stderrBuilder = new System.Text.StringBuilder();
        _serverProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutBuilder.AppendLine(e.Data); };
        _serverProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrBuilder.AppendLine(e.Data); };
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        // Wait for the /health endpoint to respond (up to 30 seconds).
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_serverProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"MCP server exited with code {_serverProcess.ExitCode} before becoming healthy.\nStdout: {stdoutBuilder}\nStderr: {stderrBuilder}");
            }

            try
            {
                var response = await httpClient.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    await WriteMarkerFileAsync(httpClient);
                    return;
                }
            }
            catch (Exception) when (!_serverProcess.HasExited)
            {
                // Server not ready yet — retry.
            }

            await Task.Delay(500);
        }

        // If we get here, the server didn't start in time.
        try { _serverProcess.Kill(entireProcessTree: true); } catch { /* best effort */ }

        throw new TimeoutException(
            $"MCP server did not become healthy within 30 seconds on port {_port}.\nStdout: {stdoutBuilder}\nStderr: {stderrBuilder}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_serverProcess is { HasExited: false } proc)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            try { await proc.WaitForExitAsync(new CancellationTokenSource(5000).Token); } catch { /* best effort */ }
        }

        _serverProcess?.Dispose();

        if (_workspaceDir is not null)
        {
            try { Directory.Delete(_workspaceDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private async Task WriteMarkerFileAsync(HttpClient httpClient)
    {
        // Fetch the API key from the loopback endpoint.
        var apiKey = "";
        try
        {
            var apiKeyResponse = await httpClient.GetAsync("/api-key");
            if (apiKeyResponse.IsSuccessStatusCode)
            {
                var json = await apiKeyResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("apiKey", out var keyProp))
                    apiKey = keyProp.GetString() ?? "";
            }
        }
        catch { /* proceed with empty key */ }

        // Write the marker file in the workspace directory.
        var markerLines = new[]
        {
            $"baseUrl: {BaseUrl}",
            $"apiKey: {apiKey}",
            $"workspacePath: {_workspaceDir}",
        };
        await File.WriteAllLinesAsync(
            Path.Combine(_workspaceDir!, "AGENTS-README-FIRST.yaml"),
            markerLines);
    }

    private static void WriteAppSettings(string dir, int port)
    {
        var escapedDir = dir.Replace("\\", "/");
        var yaml =
$@"Mcp:
  Port: {port}
  DataSource: "":memory:""
  DataDirectory: ""{escapedDir}""
  RepoRoot: ""{escapedDir}""
  TodoFilePath: TODO.yaml
  SessionsPath: sessions
  TodoStorage:
    Provider: sqlite
    SqliteDataSource: "":memory:""
  Workspaces:
  - WorkspacePath: ""{escapedDir}""
    Name: TestWorkspace
    IsPrimary: true
    IsEnabled: true
Embedding:
  Enabled: false
VectorIndex:
  Enabled: false
Logging:
  LogLevel:
    Default: Warning
";
        File.WriteAllText(Path.Combine(dir, "appsettings.yaml"), yaml);
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
