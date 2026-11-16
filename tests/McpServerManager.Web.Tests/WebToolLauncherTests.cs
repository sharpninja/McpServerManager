using System.Diagnostics;
using System.Runtime.InteropServices;
using McpServerManager.Web.Tool;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class WebToolLauncherTests
{
    [Theory]
    [InlineData(WebToolOperatingSystem.Windows, "win-x64", "McpServerManager.Web.Hybrid.exe", false)]
    [InlineData(WebToolOperatingSystem.Linux, "linux-x64", "McpServerManager.Web", true)]
    public void ResolvePayload_ReturnsPlatformSpecificExecutable(
        WebToolOperatingSystem operatingSystem,
        string runtimeIdentifier,
        string executableName,
        bool requiresUnixExecutePermission)
    {
        var payload = WebToolLauncher.ResolvePayload(
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
    [InlineData(WebToolOperatingSystem.MacOS, Architecture.X64)]
    [InlineData(WebToolOperatingSystem.Windows, Architecture.Arm64)]
    [InlineData(WebToolOperatingSystem.Linux, Architecture.Arm64)]
    public void ResolvePayload_RejectsUnsupportedPlatformOrArchitecture(
        WebToolOperatingSystem operatingSystem,
        Architecture architecture)
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(() =>
            WebToolLauncher.ResolvePayload("tool-root", operatingSystem, architecture));

        Assert.Contains(operatingSystem.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(architecture.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateStartInfo_ForwardsArgumentsWithoutShellParsing()
    {
        var payload = new WebToolPayload("linux-x64", "/tool/mcp-web", true);
        string[] arguments = ["--urls", "http://localhost:5200", "value with spaces"];

        var startInfo = WebToolLauncher.CreateStartInfo(payload, arguments);

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

        Assert.Equal(0, WebToolLauncher.RunProcess(startInfo));
    }
}
