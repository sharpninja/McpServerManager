using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServer.UI.Core.Models.Json;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class ClipboardHandlerTests
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task CopyTextHandler_HandleAsync_CallsCopyText()
    {
        _target.Setup(t => t.CopyText("hello")).Returns(Task.CompletedTask);
        var handler = new CopyTextHandler(_target.Object);
        var result = await handler.HandleAsync(new CopyTextCommand("hello"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.CopyText("hello"), Times.Once);
    }

    [Fact]
    public async Task CopyOriginalJsonHandler_HandleAsync_CallsCopyOriginalJson()
    {
        var entry = new UnifiedRequestEntry();
        _target.Setup(t => t.CopyOriginalJson(entry)).Returns(Task.CompletedTask);
        var handler = new CopyOriginalJsonHandler(_target.Object);
        var result = await handler.HandleAsync(new CopyOriginalJsonCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.CopyOriginalJson(entry), Times.Once);
    }

    [Fact]
    public async Task CopyOriginalJsonHandler_HandleAsync_NullEntry()
    {
        _target.Setup(t => t.CopyOriginalJson(null)).Returns(Task.CompletedTask);
        var handler = new CopyOriginalJsonHandler(_target.Object);
        var result = await handler.HandleAsync(new CopyOriginalJsonCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.CopyOriginalJson(null), Times.Once);
    }
}

