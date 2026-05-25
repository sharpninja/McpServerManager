namespace McpServerManager.Director.Tests;

public sealed class McpHttpClientCredentialDiscoveryTests
{
    [Fact]
    public void TryCreateWorkspaceCredentialClient_FallsBackToConfiguredWorkspaceWhenNoPrimaryExists()
    {
        var workspacePath = CreateMarkerWorkspace("http://localhost:7147", "configured-key");

        try
        {
            var client = McpHttpClient.TryCreateWorkspaceCredentialClient(
                [new McpHttpClient.ConfiguredWorkspace(workspacePath, IsPrimary: false)],
                "http://localhost:7147");

            try
            {
                Assert.NotNull(client);
                Assert.Equal("configured-key", client.ApiKey);
                Assert.Equal(workspacePath, client.WorkspacePath);
            }
            finally
            {
                client?.Dispose();
            }
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public void TryCreateWorkspaceCredentialClient_PrefersPrimaryWorkspaceMarker()
    {
        var nonPrimaryPath = CreateMarkerWorkspace("http://localhost:7147", "non-primary-key");
        var primaryPath = CreateMarkerWorkspace("http://localhost:7147", "primary-key");

        try
        {
            var client = McpHttpClient.TryCreateWorkspaceCredentialClient(
                [
                    new McpHttpClient.ConfiguredWorkspace(nonPrimaryPath, IsPrimary: false),
                    new McpHttpClient.ConfiguredWorkspace(primaryPath, IsPrimary: true),
                ],
                "http://localhost:7147");

            try
            {
                Assert.NotNull(client);
                Assert.Equal("primary-key", client.ApiKey);
                Assert.Equal(primaryPath, client.WorkspacePath);
            }
            finally
            {
                client?.Dispose();
            }
        }
        finally
        {
            Directory.Delete(primaryPath, recursive: true);
            Directory.Delete(nonPrimaryPath, recursive: true);
        }
    }

    [Fact]
    public void TryCreateWorkspaceCredentialClient_TreatsLocalhostAndMachineNameAsSameLocalServer()
    {
        var workspacePath = CreateMarkerWorkspace($"http://{Environment.MachineName}:7147", "machine-key");

        try
        {
            var client = McpHttpClient.TryCreateWorkspaceCredentialClient(
                [new McpHttpClient.ConfiguredWorkspace(workspacePath, IsPrimary: false)],
                "http://localhost:7147");

            try
            {
                Assert.NotNull(client);
                Assert.Equal("machine-key", client.ApiKey);
            }
            finally
            {
                client?.Dispose();
            }
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public void ParseConfiguredWorkspacesFromJson_AcceptsCamelCaseProperties()
    {
        const string json = """
            {
              "mcp": {
                "workspaces": [
                  { "workspacePath": "F:\\GitHub\\McpServerManager", "isPrimary": false }
                ]
              }
            }
            """;

        var workspaces = McpHttpClient.ParseConfiguredWorkspacesFromJson(json);

        var workspace = Assert.Single(workspaces);
        Assert.Equal(@"F:\GitHub\McpServerManager", workspace.WorkspacePath);
        Assert.False(workspace.IsPrimary);
    }

    private static string CreateMarkerWorkspace(string baseUrl, string apiKey)
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"director-marker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllLines(
            Path.Combine(workspacePath, "AGENTS-README-FIRST.yaml"),
            [
                $"baseUrl: {baseUrl}",
                $"apiKey: {apiKey}",
                $"workspacePath: {workspacePath}",
            ]);

        return workspacePath;
    }
}
