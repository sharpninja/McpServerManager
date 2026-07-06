using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models.Json;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class ClipboardHandlerTests
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget>();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task CopyTextHandler_HandleAsync_CallsCopyText()
    {
        _target.CopyText("hello").Returns(Task.CompletedTask);
        var handler = new CopyTextHandler(_target);
        var result = await handler.HandleAsync(new CopyTextCommand("hello"), _ctx);

        result.IsSuccess.Should().BeTrue();
        await _target.Received(1).CopyText("hello");
    }

    [Fact]
    public async Task CopyOriginalJsonHandler_HandleAsync_CallsCopyOriginalJson()
    {
        var entry = new UnifiedSessionTurn();
        _target.CopyOriginalJson(entry).Returns(Task.CompletedTask);
        var handler = new CopyOriginalJsonHandler(_target);
        var result = await handler.HandleAsync(new CopyOriginalJsonCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        await _target.Received(1).CopyOriginalJson(entry);
    }

    [Fact]
    public async Task CopyOriginalJsonHandler_HandleAsync_NullEntry()
    {
        _target.CopyOriginalJson(null).Returns(Task.CompletedTask);
        var handler = new CopyOriginalJsonHandler(_target);
        var result = await handler.HandleAsync(new CopyOriginalJsonCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        await _target.Received(1).CopyOriginalJson(null);
    }
}
