using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Models.Json;
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
    public async Task SelectSearchTurnHandler_HandleAsync_CallsSelectSearchTurn()
    {
        var entry = new SearchableTurn();
        var handler = new SelectSearchTurnHandler(_target.Object);
        var result = await handler.HandleAsync(new SelectSearchTurnCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.SelectSearchTurn(entry), Times.Once);
    }
}

