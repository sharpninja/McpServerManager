using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class PreviewHandlerTests
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task OpenPreviewInBrowserHandler_HandleAsync_CallsOpenPreviewInBrowser()
    {
        var handler = new OpenPreviewInBrowserHandler(_target.Object);
        var result = await handler.HandleAsync(new OpenPreviewInBrowserCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.OpenPreviewInBrowser(), Times.Once);
    }

    [Fact]
    public async Task ToggleShowRawMarkdownHandler_HandleAsync_CallsToggleShowRawMarkdown()
    {
        var handler = new ToggleShowRawMarkdownHandler(_target.Object);
        var result = await handler.HandleAsync(new ToggleShowRawMarkdownCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.ToggleShowRawMarkdown(), Times.Once);
    }
}
