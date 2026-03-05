using FluentAssertions;
using McpServerManager.Core.Services.Infrastructure;
using Xunit;

namespace McpServerManager.Core.Tests.Services.Infrastructure;

public sealed class ProcessLauncherServiceTests
{
    private readonly ProcessLauncherService _sut = new();

    [Fact]
    public async Task RunAsync_EchoCommand_CapturesStdout()
    {
        var result = await _sut.RunAsync("cmd.exe", "/c echo hello");

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Trim().Should().Be("hello");
        result.StandardError.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ReportsExitCode()
    {
        var result = await _sut.RunAsync("cmd.exe", "/c exit 42");

        result.ExitCode.Should().Be(42);
    }

    [Fact]
    public async Task RunAsync_StderrOutput_CapturesStderr()
    {
        var result = await _sut.RunAsync("cmd.exe", "/c echo error 1>&2");

        result.StandardError.Trim().Should().Be("error");
    }

    [Fact]
    public async Task RunAsync_WithWorkingDirectory_UsesIt()
    {
        var tempDir = Path.GetTempPath();
        var result = await _sut.RunAsync("cmd.exe", "/c cd", workingDirectory: tempDir);

        result.ExitCode.Should().Be(0);
        // cd output should contain the temp dir path
        result.StandardOutput.Trim().Should().StartWith(tempDir.TrimEnd('\\'));
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOrReturns()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.RunAsync("cmd.exe", "/c ping -n 10 127.0.0.1", ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
