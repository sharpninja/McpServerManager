namespace McpServerManager.Director.Tests;

public sealed class AgentHostCommandTests
{
    [Fact]
    public async Task AgentHelp_ExitZero()
    {
        var result = await DirectorRunner.RunAsync("agent --help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--workspace", result.StdOut);
        Assert.Contains("hosted MCP Agent console mode", result.AllOutput, StringComparison.OrdinalIgnoreCase);
    }
}
