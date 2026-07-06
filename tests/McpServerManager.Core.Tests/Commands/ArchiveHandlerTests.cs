using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Models.Json;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class ArchiveHandlerTests
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget>();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task ArchiveCurrentHandler_HandleAsync_CallsArchive()
    {
        var handler = new ArchiveCurrentHandler(_target);
        var result = await handler.HandleAsync(new ArchiveCurrentCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).Archive();
    }

    [Fact]
    public async Task ArchiveHandler_HandleAsync_CallsArchive()
    {
        var handler = new ArchiveHandler(_target);
        var result = await handler.HandleAsync(new ArchiveCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).Archive();
    }

    [Fact]
    public async Task ArchiveTreeItemHandler_HandleAsync_CallsArchiveTreeItem()
    {
        var node = new FileNode("session.json", false);
        var handler = new ArchiveTreeItemHandler(_target);
        var result = await handler.HandleAsync(new ArchiveTreeItemCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).ArchiveTreeItem(node);
    }

    [Fact]
    public async Task ArchiveTreeItemHandler_HandleAsync_NullNode()
    {
        var handler = new ArchiveTreeItemHandler(_target);
        var result = await handler.HandleAsync(new ArchiveTreeItemCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).ArchiveTreeItem(null);
    }

    [Fact]
    public async Task OpenTreeItemHandler_HandleAsync_CallsOpenTreeItem()
    {
        var node = new FileNode("data.json", false);
        var handler = new OpenTreeItemHandler(_target);
        var result = await handler.HandleAsync(new OpenTreeItemCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).OpenTreeItem(node);
    }

    [Fact]
    public async Task SelectSearchTurnHandler_HandleAsync_CallsSelectSearchTurn()
    {
        var entry = new SearchableTurn();
        var handler = new SelectSearchTurnHandler(_target);
        var result = await handler.HandleAsync(new SelectSearchTurnCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).SelectSearchTurn(entry);
    }
}
