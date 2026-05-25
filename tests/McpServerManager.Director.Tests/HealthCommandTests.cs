namespace McpServerManager.Director.Tests;

/// <summary>
/// Tests for the <c>health</c> command.
/// Spins up a real MCP server instance on a random port and verifies the Director
/// can connect and report health status.
/// </summary>
public sealed class HealthCommandTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _server;

    public HealthCommandTests(McpServerFixture server) => _server = server;

    [Fact]
    public async Task HealthHelp_ExitZero()
    {
        var result = await DirectorRunner.RunAsync("health --help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--workspace", result.StdOut);
    }

    [Fact]
    public async Task Health_ReturnsServerStatus()
    {
        var result = await DirectorRunner.RunAsync(
            $"health --workspace \"{_server.WorkspaceDir}\"",
            workingDirectory: _server.WorkspaceDir);

        Assert.Equal(0, result.ExitCode);
        var output = result.AllOutput;
        Assert.Contains("healthy", output, StringComparison.OrdinalIgnoreCase);
    }
}
