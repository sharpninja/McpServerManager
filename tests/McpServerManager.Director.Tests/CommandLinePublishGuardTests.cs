using McpServerManager.Build;

namespace McpServerManager.Director.Tests;

/// <summary>
/// director must publish a stable System.CommandLine build. Assembly 2.0.0.0 is the
/// 2.0.0-beta4 contract. Stable 2.0.3 and later expose SetAction and do not expose AddCommand.
/// </summary>
public sealed class CommandLinePublishGuardTests
{
    [Fact]
    public void IsSupportedVersion_RejectsBetaAssemblyAndAcceptsStable()
    {
        Assert.False(CommandLinePublishGuard.IsSupportedVersion(new Version(2, 0, 0, 0)));
        Assert.True(CommandLinePublishGuard.IsSupportedVersion(new Version(2, 0, 12, 0)));
    }

    [Fact]
    public void EnsureCompatible_AcceptsReferencedStableAssembly()
    {
        var path = typeof(System.CommandLine.Command).Assembly.Location;
        var version = System.Reflection.AssemblyName.GetAssemblyName(path).Version;

        Assert.NotNull(version);
        Assert.True(version >= new Version(2, 0, 12, 0));

        var exception = Record.Exception(() => CommandLinePublishGuard.EnsureCompatible(path));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCompatible_AcceptsStable209Assembly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "System.CommandLine.2.0.9.dll");

        var exception = Record.Exception(() => CommandLinePublishGuard.EnsureCompatible(path));

        Assert.Null(exception);
    }
}
