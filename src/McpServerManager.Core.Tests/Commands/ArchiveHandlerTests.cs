using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Models;
using McpServerManager.Core.Models.Json;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class ArchiveHandlerTests
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task ArchiveCurrentHandler_HandleAsync_CallsArchive()
    {
        var handler = new ArchiveCurrentHandler(_target.Object);
        var result = await handler.HandleAsync(new ArchiveCurrentCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.Archive(), Times.Once);
    }

    [Fact]
    public async Task ArchiveHandler_HandleAsync_CallsArchive()
    {
        var handler = new ArchiveHandler(_target.Object);
        var result = await handler.HandleAsync(new ArchiveCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.Archive(), Times.Once);
    }

    [Fact]
    public async Task ArchiveTreeItemHandler_HandleAsync_CallsArchiveTreeItem()
    {
        var node = new FileNode("session.json", false);
        var handler = new ArchiveTreeItemHandler(_target.Object);
        var result = await handler.HandleAsync(new ArchiveTreeItemCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.ArchiveTreeItem(node), Times.Once);
    }

    [Fact]
    public async Task ArchiveTreeItemHandler_HandleAsync_NullNode()
    {
        var handler = new ArchiveTreeItemHandler(_target.Object);
        var result = await handler.HandleAsync(new ArchiveTreeItemCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.ArchiveTreeItem(null), Times.Once);
    }

    [Fact]
    public async Task OpenTreeItemHandler_HandleAsync_CallsOpenTreeItem()
    {
        var node = new FileNode("data.json", false);
        var handler = new OpenTreeItemHandler(_target.Object);
        var result = await handler.HandleAsync(new OpenTreeItemCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.OpenTreeItem(node), Times.Once);
    }

    [Fact]
    public async Task SelectSearchEntryHandler_HandleAsync_CallsSelectSearchEntry()
    {
        var entry = new SearchableEntry();
        var handler = new SelectSearchEntryHandler(_target.Object);
        var result = await handler.HandleAsync(new SelectSearchEntryCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.SelectSearchEntry(entry), Times.Once);
    }
}
