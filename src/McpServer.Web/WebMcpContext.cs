using System.IO;
using System.Runtime.CompilerServices;
using McpServer.Client;
using McpServerManager.UI.Core.Hosting;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.Web.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.Web;

internal sealed class WebMcpContext : IMcpHostContext
{
    private readonly object _gate = new();
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly McpServerClient _controlApiClient;
    private readonly McpServerClient _activeWorkspaceApiClient;
    private readonly BearerTokenAccessor _bearerTokenAccessor;
    private readonly Func<McpServerClientOptions, McpServerClient> _clientFactory;
    private readonly ILogger<WebMcpContext> _logger;
    private readonly SemaphoreSlim _authRefreshGate = new(1, 1);
    private string? _apiKey;

    public WebMcpContext(
        IConfiguration configuration,
        WorkspaceContextViewModel workspaceContext,
        BearerTokenAccessor bearerTokenAccessor,
        ILogger<WebMcpContext>? logger = null,
        Func<McpServerClientOptions, McpServerClient>? clientFactory = null)
    {
        _workspaceContext = workspaceContext;
        _bearerTokenAccessor = bearerTokenAccessor;
        _logger = logger ?? NullLogger<WebMcpContext>.Instance;
        _clientFactory = clientFactory ?? McpServerClientFactory.Create;

        var baseUrl = configuration["McpServer:BaseUrl"] ?? "http://localhost:7147";
        _apiKey = configuration["McpServer:ApiKey"];
        var configuredWorkspacePath = NormalizeWorkspacePath(configuration["McpServer:WorkspacePath"]);
        ActiveWorkspacePath = configuredWorkspacePath;
        BaseUrl = new Uri(baseUrl, UriKind.Absolute);

        _controlApiClient = CreateTypedClient(BaseUrl, _apiKey, workspacePath: null);
        _activeWorkspaceApiClient = CreateTypedClient(BaseUrl, _apiKey, ActiveWorkspacePath);

        if (string.IsNullOrWhiteSpace(_workspaceContext.ActiveWorkspacePath))
            _workspaceContext.ActiveWorkspacePath = configuredWorkspacePath;
        else
            TrySetActiveWorkspace(_workspaceContext.ActiveWorkspacePath);

        _workspaceContext.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkspaceContextViewModel.ActiveWorkspacePath))
                TrySetActiveWorkspace(_workspaceContext.ActiveWorkspacePath, updateViewModel: false);
        };
    }

    public Uri BaseUrl { get; }

    public string? ActiveWorkspacePath { get; private set; }

    public void RefreshBearerTokens()
    {
        var bearerToken = _bearerTokenAccessor.GetAccessTokenAsync().GetAwaiter().GetResult();
        AlignAuthenticationMode(bearerToken);
    }

    public bool TrySetActiveWorkspace(string? workspacePath)
        => TrySetActiveWorkspace(workspacePath, updateViewModel: true);

    public bool TrySetActiveWorkspace(string? workspacePath, bool updateViewModel = true)
    {
        var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);

        lock (_gate)
        {
            ActiveWorkspacePath = normalizedWorkspacePath;
            _activeWorkspaceApiClient.WorkspacePath = normalizedWorkspacePath ?? string.Empty;
        }

        if (updateViewModel &&
            !string.Equals(_workspaceContext.ActiveWorkspacePath, normalizedWorkspacePath, StringComparison.Ordinal))
        {
            _workspaceContext.ActiveWorkspacePath = normalizedWorkspacePath;
        }

        return true;
    }

    public Task<McpServerClient> GetApiClientAsync(CancellationToken cancellationToken = default)
        => GetRequiredActiveWorkspaceApiClientAsync(cancellationToken);

    public async Task<McpServerClient> GetRequiredControlApiClientAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(_controlApiClient, cancellationToken).ConfigureAwait(true);
        return _controlApiClient;
    }

    public async Task<McpServerClient> GetRequiredActiveWorkspaceApiClientAsync(CancellationToken cancellationToken = default)
    {
        // Always pull from the singleton source of truth — WorkspaceContextViewModel owns the active workspace.
        var contextPath = _workspaceContext.ActiveWorkspacePath;
        if (!string.IsNullOrWhiteSpace(contextPath) &&
            !string.Equals(_activeWorkspaceApiClient.WorkspacePath, contextPath, StringComparison.Ordinal))
        {
            lock (_gate)
            {
                _activeWorkspaceApiClient.WorkspacePath = contextPath;
                ActiveWorkspacePath = contextPath;
            }
        }

        await EnsureInitializedAsync(_activeWorkspaceApiClient, cancellationToken).ConfigureAwait(true);
        return _activeWorkspaceApiClient;
    }

    public Task<T> UseControlApiClientAsync<T>(
        Func<McpServerClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => UseApiClientAsync(GetRequiredControlApiClientAsync, operation, "control", cancellationToken);

    public Task UseControlApiClientAsync(
        Func<McpServerClient, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        => UseApiClientAsync(
            GetRequiredControlApiClientAsync,
            async (client, ct) =>
            {
                await operation(client, ct).ConfigureAwait(true);
                return true;
            },
            "control",
            cancellationToken);

    public Task<T> UseActiveWorkspaceApiClientAsync<T>(
        Func<McpServerClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => UseApiClientAsync(GetRequiredActiveWorkspaceApiClientAsync, operation, "active workspace", cancellationToken);

    public Task UseActiveWorkspaceApiClientAsync(
        Func<McpServerClient, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        => UseApiClientAsync(
            GetRequiredActiveWorkspaceApiClientAsync,
            async (client, ct) =>
            {
                await operation(client, ct).ConfigureAwait(true);
                return true;
            },
            "active workspace",
            cancellationToken);

    public IAsyncEnumerable<T> StreamActiveWorkspaceApiClientAsync<T>(
        Func<McpServerClient, CancellationToken, IAsyncEnumerable<T>> operation,
        CancellationToken cancellationToken = default)
        => StreamApiClientAsync(GetRequiredActiveWorkspaceApiClientAsync, operation, "active workspace", cancellationToken);

    private async Task EnsureInitializedAsync(McpServerClient client, CancellationToken cancellationToken)
    {
        // If the user is authenticated, prefer their OIDC access token over the static API key.
        var bearerToken = await _bearerTokenAccessor.GetAccessTokenAsync(cancellationToken).ConfigureAwait(true);
        AlignAuthenticationMode(bearerToken);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            return;
        }

        if (TryGetWorkspaceMarkerApiKey(client, out var markerApiKey, out var markerPath))
        {
            var currentApiKey = client.ApiKey;
            ApplyApiKey(markerApiKey, overwriteExisting: true);

            if (!string.Equals(currentApiKey, markerApiKey, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Loaded Web MCP API key from workspace marker file {MarkerPath}.",
                    markerPath);
            }

            return;
        }

        // Already initialized with a static API key or previously discovered key.
        if (!string.IsNullOrWhiteSpace(client.ApiKey) || !string.IsNullOrWhiteSpace(client.BearerToken))
            return;

        // Auto-discover the API key from the McpServer AGENTS-README-FIRST.yaml initialization endpoint.
        var apiKey = await client.InitializeAsync(cancellationToken).ConfigureAwait(true);
        ApplyApiKey(apiKey, overwriteExisting: false);
    }

    private void AlignAuthenticationMode(string? bearerToken)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                SwitchToBearerTokenNoLock(bearerToken);
                return;
            }

            if (!HasCachedBearerAuthNoLock())
                return;

            var apiKeyToRestore = _apiKey;
            _controlApiClient.Logout();
            _activeWorkspaceApiClient.Logout();

            if (!string.IsNullOrWhiteSpace(apiKeyToRestore))
            {
                _controlApiClient.ApiKey = apiKeyToRestore;
                _activeWorkspaceApiClient.ApiKey = apiKeyToRestore;
            }

            _logger.LogInformation(
                "No current Web MCP bearer token is available. Cleared cached bearer-token auth and restored API key mode for the current circuit.");
        }
    }

    private void SwitchToBearerTokenNoLock(string bearerToken)
    {
        var bearerChanged =
            (!string.IsNullOrWhiteSpace(_controlApiClient.BearerToken) &&
             !string.Equals(_controlApiClient.BearerToken, bearerToken, StringComparison.Ordinal)) ||
            (!string.IsNullOrWhiteSpace(_activeWorkspaceApiClient.BearerToken) &&
             !string.Equals(_activeWorkspaceApiClient.BearerToken, bearerToken, StringComparison.Ordinal));

        if (bearerChanged)
        {
            _controlApiClient.Logout();
            _activeWorkspaceApiClient.Logout();
        }

        if (string.IsNullOrWhiteSpace(_controlApiClient.BearerToken))
            _controlApiClient.BearerToken = bearerToken;

        if (string.IsNullOrWhiteSpace(_activeWorkspaceApiClient.BearerToken))
            _activeWorkspaceApiClient.BearerToken = bearerToken;
    }

    private bool HasCachedBearerAuthNoLock()
        => !string.IsNullOrWhiteSpace(_controlApiClient.BearerToken) ||
           !string.IsNullOrWhiteSpace(_activeWorkspaceApiClient.BearerToken) ||
           _controlApiClient.Todo.RequireBearerToken ||
           _activeWorkspaceApiClient.Todo.RequireBearerToken;

    private async Task<T> UseApiClientAsync<T>(
        Func<CancellationToken, Task<McpServerClient>> getClientAsync,
        Func<McpServerClient, CancellationToken, Task<T>> operation,
        string clientKind,
        CancellationToken cancellationToken)
    {
        var client = await getClientAsync(cancellationToken).ConfigureAwait(true);
        var attemptedApiKey = client.ApiKey;

        try
        {
            return await operation(client, cancellationToken).ConfigureAwait(true);
        }
        catch (McpUnauthorizedException exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (!await TryRefreshApiKeyAfterUnauthorizedAsync(attemptedApiKey, clientKind, exception, cancellationToken).ConfigureAwait(true))
                throw;
        }

        client = await getClientAsync(cancellationToken).ConfigureAwait(true);
        return await operation(client, cancellationToken).ConfigureAwait(true);
    }

    private async IAsyncEnumerable<T> StreamApiClientAsync<T>(
        Func<CancellationToken, Task<McpServerClient>> getClientAsync,
        Func<McpServerClient, CancellationToken, IAsyncEnumerable<T>> operation,
        string clientKind,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await getClientAsync(cancellationToken).ConfigureAwait(true);
        var attemptedApiKey = client.ApiKey;

        await using var enumerator = operation(client, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool movedNext;
            var retryWithRefreshedClient = false;
            try
            {
                movedNext = await enumerator.MoveNextAsync().ConfigureAwait(true);
            }
            catch (McpUnauthorizedException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (!await TryRefreshApiKeyAfterUnauthorizedAsync(attemptedApiKey, clientKind, exception, cancellationToken).ConfigureAwait(true))
                    throw;

                retryWithRefreshedClient = true;
                movedNext = false;
            }

            if (retryWithRefreshedClient)
            {
                client = await getClientAsync(cancellationToken).ConfigureAwait(true);
                await foreach (var item in operation(client, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(true))
                    yield return item;
                yield break;
            }

            if (!movedNext)
                yield break;

            yield return enumerator.Current;
        }
    }

    private async Task<bool> TryRefreshApiKeyAfterUnauthorizedAsync(
        string? attemptedApiKey,
        string clientKind,
        McpUnauthorizedException exception,
        CancellationToken cancellationToken)
    {
        var currentBearerToken = await _bearerTokenAccessor.GetAccessTokenAsync(cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(currentBearerToken) ||
            !string.IsNullOrWhiteSpace(_controlApiClient.BearerToken) ||
            !string.IsNullOrWhiteSpace(_activeWorkspaceApiClient.BearerToken))
        {
            _logger.LogWarning(
                exception,
                "Received unauthorized response for Web MCP {ClientKind} client while bearer-token auth is active. Skipping API key refresh.",
                clientKind);
            return false;
        }

        await _authRefreshGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            var currentApiKey = _controlApiClient.ApiKey;
            if (!string.IsNullOrWhiteSpace(currentApiKey) &&
                !string.Equals(currentApiKey, attemptedApiKey, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Web MCP {ClientKind} client detected that another request already refreshed the API key. Retrying once with key prefix {ApiKeyPrefix}.",
                    clientKind,
                    GetApiKeyPrefix(currentApiKey));
                return true;
            }

            ClearApiKeys();
            if (TryGetWorkspaceMarkerApiKey(out var markerApiKey, out var markerPath) &&
                !string.Equals(markerApiKey, attemptedApiKey, StringComparison.Ordinal))
            {
                ApplyApiKey(markerApiKey, overwriteExisting: true);
                _logger.LogWarning(
                    exception,
                    "Web MCP {ClientKind} client refreshed a stale API key from workspace marker file {MarkerPath}. Previous key prefix: {OldKeyPrefix}. New key prefix: {NewKeyPrefix}. Retrying once.",
                    clientKind,
                    markerPath,
                    GetApiKeyPrefix(attemptedApiKey),
                    GetApiKeyPrefix(markerApiKey));
                return true;
            }

            var refreshedApiKey = await _controlApiClient.InitializeAsync(cancellationToken).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(refreshedApiKey))
            {
                _logger.LogWarning(
                    exception,
                    "Failed to refresh the Web MCP {ClientKind} API key after unauthorized response because the refresh endpoint returned an empty key.",
                    clientKind);
                return false;
            }

            ApplyApiKey(refreshedApiKey, overwriteExisting: true);
            _logger.LogWarning(
                exception,
                "Web MCP {ClientKind} client refreshed a stale API key from the default /api-key endpoint after unauthorized response. Previous key prefix: {OldKeyPrefix}. New key prefix: {NewKeyPrefix}. Retrying once.",
                clientKind,
                GetApiKeyPrefix(attemptedApiKey),
                GetApiKeyPrefix(refreshedApiKey));
            return true;
        }
        finally
        {
            _authRefreshGate.Release();
        }
    }

    private void ApplyApiKey(string apiKey, bool overwriteExisting)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        lock (_gate)
        {
            if (overwriteExisting &&
                ((!string.IsNullOrWhiteSpace(_controlApiClient.ApiKey) &&
                  !string.Equals(_controlApiClient.ApiKey, apiKey, StringComparison.Ordinal)) ||
                 (!string.IsNullOrWhiteSpace(_activeWorkspaceApiClient.ApiKey) &&
                  !string.Equals(_activeWorkspaceApiClient.ApiKey, apiKey, StringComparison.Ordinal))))
            {
                _controlApiClient.Logout();
                _activeWorkspaceApiClient.Logout();
            }

            _apiKey = apiKey;

            if (overwriteExisting || string.IsNullOrWhiteSpace(_controlApiClient.ApiKey))
                _controlApiClient.ApiKey = apiKey;

            if (overwriteExisting || string.IsNullOrWhiteSpace(_activeWorkspaceApiClient.ApiKey))
                _activeWorkspaceApiClient.ApiKey = apiKey;
        }
    }

    private void ClearApiKeys()
    {
        lock (_gate)
        {
            _apiKey = null;
            _controlApiClient.Logout();
            _activeWorkspaceApiClient.Logout();
        }
    }

    private McpServerClient CreateTypedClient(Uri baseUrl, string? apiKey, string? workspacePath)
        => _clientFactory(new McpServerClientOptions
        {
            BaseUrl = baseUrl,
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            WorkspacePath = workspacePath,
            Timeout = TimeSpan.FromMinutes(10),
        });

    private static string? NormalizeWorkspacePath(string? workspacePath)
        => string.IsNullOrWhiteSpace(workspacePath) ? null : workspacePath.Trim();

    private bool TryGetWorkspaceMarkerApiKey(out string apiKey, out string markerPath)
        => TryGetWorkspaceMarkerApiKey(_activeWorkspaceApiClient, out apiKey, out markerPath);

    private bool TryGetWorkspaceMarkerApiKey(McpServerClient client, out string apiKey, out string markerPath)
    {
        apiKey = string.Empty;
        markerPath = string.Empty;

        var workspacePath = ResolveMarkerWorkspacePath(client);
        if (string.IsNullOrWhiteSpace(workspacePath))
            return false;

        markerPath = Path.Combine(workspacePath, "AGENTS-README-FIRST.yaml");
        if (!File.Exists(markerPath))
            return false;

        foreach (var line in File.ReadLines(markerPath))
        {
            if (!line.TrimStart().StartsWith("apiKey:", StringComparison.OrdinalIgnoreCase))
                continue;

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
                break;

            var value = line[(separatorIndex + 1)..].Trim();
            value = TrimYamlScalar(value);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            apiKey = value;
            return true;
        }

        return false;
    }

    private string? ResolveMarkerWorkspacePath(McpServerClient client)
    {
        var clientWorkspacePath = NormalizeWorkspacePath(client.WorkspacePath);
        if (!string.IsNullOrWhiteSpace(clientWorkspacePath))
            return clientWorkspacePath;

        var contextWorkspacePath = NormalizeWorkspacePath(_workspaceContext.ActiveWorkspacePath);
        if (!string.IsNullOrWhiteSpace(contextWorkspacePath))
            return contextWorkspacePath;

        return NormalizeWorkspacePath(ActiveWorkspacePath);
    }

    private static string TrimYamlScalar(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string GetApiKeyPrefix(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "(empty)";

        var length = Math.Min(8, apiKey.Length);
        return apiKey[..length] + "…";
    }
}
