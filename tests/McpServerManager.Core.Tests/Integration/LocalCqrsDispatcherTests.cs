using System.Threading.Tasks;
using FluentAssertions;
using McpServer.UI.Core.Commands;
using McpServerManager.Core.Services;
using Xunit;

namespace McpServerManager.Core.Tests.Integration;

public sealed class LocalCqrsDispatcherTests
{
    [Fact]
    public async Task InvokeUiActionCommand_DispatchesThroughFallbackDispatcher()
    {
        var executed = false;

        var dispatcher = ChatWindowViewModelFactory.CreateFallbackDispatcher(new TestLogAgentService());

        var result = await dispatcher.SendAsync(
            new InvokeUiActionCommand(() =>
            {
                executed = true;
                return Task.CompletedTask;
            }));

        result.IsSuccess.Should().BeTrue(result.Error);
        executed.Should().BeTrue();
    }

    private sealed class TestLogAgentService : ILogAgentService
    {
        public Task<string> SendMessageAsync(string userMessage, string contextSummary, string? model = null, IProgress<string>? contentProgress = null, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult("ok");
    }
}
