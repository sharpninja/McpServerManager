using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace McpServerManager.Director.Tests;

/// <summary>
/// xUnit fixture that launches a deterministic loopback MCP-compatible health endpoint
/// on a random available port, creates a temporary workspace directory with an
/// <c>AGENTS-README-FIRST.yaml</c> marker file, and tears everything down on dispose.
/// </summary>
public sealed class McpServerFixture : IAsyncLifetime
{
    private const string ApiKey = "test-api-key";
    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private string? _workspaceDir;
    private string? _baseUrl;
    private bool _ownsWorkspaceDir;
    private int _port;

    /// <summary>The temporary workspace directory containing the marker file.</summary>
    public string WorkspaceDir => _workspaceDir ?? throw new InvalidOperationException("Fixture not initialized.");

    /// <summary>The port the MCP server is listening on.</summary>
    public int Port => _port;

    /// <summary>The base URL of the running MCP server.</summary>
    public string BaseUrl => _baseUrl ?? $"http://localhost:{_port}";

    public async ValueTask InitializeAsync()
    {
        _port = GetAvailablePort();
        _workspaceDir = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspaceDir);
        _ownsWorkspaceDir = true;
        _baseUrl = $"http://localhost:{_port}";

        StartHealthEndpoint();
        await WaitForHealthAsync().ConfigureAwait(true);
        await WriteMarkerFileAsync().ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        _listenerCts?.Cancel();
        _listener?.Stop();
        _listener?.Close();

        if (_listenerTask is not null)
        {
            try { await _listenerTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true); }
            catch { /* best effort */ }
        }

        _listenerCts?.Dispose();

        if (_ownsWorkspaceDir && _workspaceDir is not null)
        {
            try { Directory.Delete(_workspaceDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private void StartHealthEndpoint()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"{BaseUrl.TrimEnd('/')}/");
        _listener.Start();

        _listenerCts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => RunHealthEndpointAsync(_listener, _listenerCts.Token));
    }

    private async Task WaitForHealthAsync()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync("/health").ConfigureAwait(true);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Listener not ready yet. Retry until the deadline.
            }

            await Task.Delay(100).ConfigureAwait(true);
        }

        throw new TimeoutException($"Test health endpoint did not become ready within 30 seconds on port {_port}.");
    }

    private static async Task RunHealthEndpointAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            await HandleRequestAsync(context).ConfigureAwait(false);
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;
        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context, new { status = "healthy", source = "Director test fixture" }).ConfigureAwait(false);
            return;
        }

        if (string.Equals(path, "/api-key", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context, new { apiKey = ApiKey }).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.Close();
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task WriteMarkerFileAsync()
    {
        var marker = new Dictionary<string, string>
        {
            [nameof(BaseUrl)] = BaseUrl,
            ["apiKey"] = ApiKey,
            [nameof(WorkspaceDir)] = WorkspaceDir,
        };
        var normalizedMarker = new Dictionary<string, string>
        {
            ["baseUrl"] = marker[nameof(BaseUrl)],
            ["apiKey"] = marker["apiKey"],
            ["workspacePath"] = marker[nameof(WorkspaceDir)],
        };
        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(normalizedMarker);
        await File.WriteAllTextAsync(Path.Combine(_workspaceDir!, "AGENTS-README-FIRST.yaml"), yaml).ConfigureAwait(true);
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
