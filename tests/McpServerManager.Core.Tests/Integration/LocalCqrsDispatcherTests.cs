using System.Threading.Tasks;
using FluentAssertions;
using McpServer.UI.Core.Commands;
using McpServerManager.Core.Services;
using Xunit;

namespace McpServerManager.Core.Tests.Integration;

public sealed class LocalCqrsDispatcherTests
{
    [Fact]
    public async Task InvokeUiActionCommand_DispatchesThroughLocalDispatcher()
    {
        var executed = false;

        var result = await LocalCqrsDispatcher.Instance.SendAsync(
            new InvokeUiActionCommand(() =>
            {
                executed = true;
                return Task.CompletedTask;
            }));

        result.IsSuccess.Should().BeTrue(result.Error);
        executed.Should().BeTrue();
    }
}
