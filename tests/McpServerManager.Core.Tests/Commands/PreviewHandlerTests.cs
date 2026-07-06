using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class PreviewHandlerTests
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget>();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task OpenPreviewInBrowserHandler_HandleAsync_CallsOpenPreviewInBrowser()
    {
        var handler = new OpenPreviewInBrowserHandler(_target);
        var result = await handler.HandleAsync(new OpenPreviewInBrowserCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).OpenPreviewInBrowser();
    }

    [Fact]
    public async Task ToggleShowRawMarkdownHandler_HandleAsync_CallsToggleShowRawMarkdown()
    {
        var handler = new ToggleShowRawMarkdownHandler(_target);
        var result = await handler.HandleAsync(new ToggleShowRawMarkdownCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).ToggleShowRawMarkdown();
    }
}
