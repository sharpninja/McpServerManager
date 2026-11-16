using System.Diagnostics;
using System.Runtime.InteropServices;
using McpServerManager.Director.Tool;

namespace McpServerManager.Director.Tests;

public sealed class DirectorToolLauncherTests
{
    [Theory]
    [InlineData(DirectorToolOperatingSystem.Windows, "win-x64", "director.exe", false)]
    [InlineData(DirectorToolOperatingSystem.Linux, "linux-x64", "director", true)]
    public void ResolvePayload_ReturnsPlatformSpecificExecutable(
        DirectorToolOperatingSystem operatingSystem,
        string runtimeIdentifier,
        string executableName,
        bool requiresUnixExecutePermission)
    {
        var payload = DirectorToolLauncher.ResolvePayload(
            Path.GetFullPath("tool-root"),
            operatingSystem,
            Architecture.X64);

        Assert.Equal(runtimeIdentifier, payload.RuntimeIdentifier);
        Assert.Equal(
            Path.GetFullPath(Path.Combine("tool-root", "runtimes", runtimeIdentifier, executableName)),
            payload.ExecutablePath);
        Assert.Equal(requiresUnixExecutePermission, payload.RequiresUnixExecutePermission);
    }

    [Theory]
    [InlineData(DirectorToolOperatingSystem.MacOS, Architecture.X64)]
    [InlineData(DirectorToolOperatingSystem.Windows, Architecture.Arm64)]
    [InlineData(DirectorToolOperatingSystem.Linux, Architecture.Arm64)]
    public void ResolvePayload_RejectsUnsupportedPlatformOrArchitecture(
        DirectorToolOperatingSystem operatingSystem,
        Architecture architecture)
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(() =>
            DirectorToolLauncher.ResolvePayload("tool-root", operatingSystem, architecture));

        Assert.Contains(operatingSystem.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(architecture.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateStartInfo_ForwardsArgumentsWithoutShellParsing()
    {
        var payload = new DirectorToolPayload("linux-x64", "/tool/director", true);
        string[] arguments = ["exec", "get-todo", "--input", "{\"TodoId\":\"value with spaces\"}"];

        var startInfo = DirectorToolLauncher.CreateStartInfo(payload, arguments);

        Assert.Equal(payload.ExecutablePath, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(arguments, startInfo.ArgumentList);
    }

    [Fact]
    public void RunProcess_ReturnsChildExitCode()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--version");

        Assert.Equal(0, DirectorToolLauncher.RunProcess(startInfo));
    }
}
