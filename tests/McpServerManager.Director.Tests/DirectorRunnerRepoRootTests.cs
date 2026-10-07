namespace McpServerManager.Director.Tests;

/// <summary>
/// Regression for HV-R3-01: repository-root discovery must stay inside the Manager
/// checkout even when it is nested under a parent that contains McpServer.sln
/// (for example an archive expanded inside an McpServer worktree).
/// </summary>
public sealed class DirectorRunnerRepoRootTests : IDisposable
{
    private readonly string _outer = Path.Combine(Path.GetTempPath(), "director-root-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindRepoRoot_NestedUnderMcpServerSln_ReturnsManagerRoot()
    {
        var manager = Path.Combine(_outer, "nested", "McpServerManager");
        var start = Path.Combine(manager, "tests", "McpServerManager.Director.Tests", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(start);
        Directory.CreateDirectory(Path.Combine(manager, "src", "McpServerManager.Director"));
        File.WriteAllText(Path.Combine(_outer, "McpServer.sln"), string.Empty);
        File.WriteAllText(Path.Combine(manager, "McpServerManager.sln"), string.Empty);

        var root = DirectorRunner.FindRepoRoot(start);

        Assert.Equal(Path.GetFullPath(manager), Path.GetFullPath(root));
    }

    [Fact]
    public void RepoRoot_ContainsDirectorProject()
    {
        Assert.True(
            File.Exists(Path.Combine(DirectorRunner.RepoRoot, "src", "McpServerManager.Director", "McpServerManager.Director.csproj")),
            $"RepoRoot '{DirectorRunner.RepoRoot}' does not contain the Director project.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_outer, recursive: true); } catch { /* best effort */ }
    }
}