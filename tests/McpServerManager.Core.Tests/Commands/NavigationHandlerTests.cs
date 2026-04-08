using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Models.Json;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class NavigationHandlerTests
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task NavigateBackHandler_HandleAsync_CallsNavigateBack()
    {
        var handler = new NavigateBackHandler(_target.Object);
        var result = await handler.HandleAsync(new NavigateBackCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.NavigateBack(), Times.Once);
    }

    [Fact]
    public async Task NavigateForwardHandler_HandleAsync_CallsNavigateForward()
    {
        var handler = new NavigateForwardHandler(_target.Object);
        var result = await handler.HandleAsync(new NavigateForwardCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.NavigateForward(), Times.Once);
    }

    [Fact]
    public async Task PhoneNavigateSectionHandler_HandleAsync_CallsPhoneNavigateSection()
    {
        var handler = new PhoneNavigateSectionHandler(_target.Object);
        var result = await handler.HandleAsync(new PhoneNavigateSectionCommand("tree"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.PhoneNavigateSection("tree"), Times.Once);
    }

    [Fact]
    public async Task PhoneNavigateSectionHandler_HandleAsync_NullSectionKey()
    {
        var handler = new PhoneNavigateSectionHandler(_target.Object);
        var result = await handler.HandleAsync(new PhoneNavigateSectionCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.PhoneNavigateSection(null), Times.Once);
    }

    [Fact]
    public async Task TreeItemTappedHandler_HandleAsync_CallsTreeItemTapped()
    {
        var node = new FileNode("test.json", false);
        var handler = new TreeItemTappedHandler(_target.Object);
        var result = await handler.HandleAsync(new TreeItemTappedCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.TreeItemTapped(node), Times.Once);
    }

    [Fact]
    public async Task TreeItemTappedHandler_HandleAsync_NullNode()
    {
        var handler = new TreeItemTappedHandler(_target.Object);
        var result = await handler.HandleAsync(new TreeItemTappedCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.TreeItemTapped(null), Times.Once);
    }

    [Fact]
    public async Task JsonNodeDoubleTappedHandler_HandleAsync_CallsJsonNodeDoubleTapped()
    {
        var node = new JsonTreeNode("key", "value", "String");
        var handler = new JsonNodeDoubleTappedHandler(_target.Object);
        var result = await handler.HandleAsync(new JsonNodeDoubleTappedCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.JsonNodeDoubleTapped(node), Times.Once);
    }

    [Fact]
    public async Task SearchRowTappedHandler_HandleAsync_CallsSearchRowTapped()
    {
        var entry = new SearchableTurn();
        var handler = new SearchRowTappedHandler(_target.Object);
        var result = await handler.HandleAsync(new SearchRowTappedCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.SearchRowTapped(entry), Times.Once);
    }

    [Fact]
    public async Task SearchRowDoubleTappedHandler_HandleAsync_CallsSearchRowDoubleTapped()
    {
        var entry = new SearchableTurn();
        var handler = new SearchRowDoubleTappedHandler(_target.Object);
        var result = await handler.HandleAsync(new SearchRowDoubleTappedCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.SearchRowDoubleTapped(entry), Times.Once);
    }
}

