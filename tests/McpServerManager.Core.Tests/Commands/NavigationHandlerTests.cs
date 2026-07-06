using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Models.Json;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class NavigationHandlerTests
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget>();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task NavigateBackHandler_HandleAsync_CallsNavigateBack()
    {
        var handler = new NavigateBackHandler(_target);
        var result = await handler.HandleAsync(new NavigateBackCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).NavigateBack();
    }

    [Fact]
    public async Task NavigateForwardHandler_HandleAsync_CallsNavigateForward()
    {
        var handler = new NavigateForwardHandler(_target);
        var result = await handler.HandleAsync(new NavigateForwardCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).NavigateForward();
    }

    [Fact]
    public async Task PhoneNavigateSectionHandler_HandleAsync_CallsPhoneNavigateSection()
    {
        var handler = new PhoneNavigateSectionHandler(_target);
        var result = await handler.HandleAsync(new PhoneNavigateSectionCommand("tree"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).PhoneNavigateSection("tree");
    }

    [Fact]
    public async Task PhoneNavigateSectionHandler_HandleAsync_NullSectionKey()
    {
        var handler = new PhoneNavigateSectionHandler(_target);
        var result = await handler.HandleAsync(new PhoneNavigateSectionCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).PhoneNavigateSection(null);
    }

    [Fact]
    public async Task TreeItemTappedHandler_HandleAsync_CallsTreeItemTapped()
    {
        var node = new FileNode("test.json", false);
        var handler = new TreeItemTappedHandler(_target);
        var result = await handler.HandleAsync(new TreeItemTappedCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).TreeItemTapped(node);
    }

    [Fact]
    public async Task TreeItemTappedHandler_HandleAsync_NullNode()
    {
        var handler = new TreeItemTappedHandler(_target);
        var result = await handler.HandleAsync(new TreeItemTappedCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).TreeItemTapped(null);
    }

    [Fact]
    public async Task JsonNodeDoubleTappedHandler_HandleAsync_CallsJsonNodeDoubleTapped()
    {
        var node = new JsonTreeNode("key", "value", "String");
        var handler = new JsonNodeDoubleTappedHandler(_target);
        var result = await handler.HandleAsync(new JsonNodeDoubleTappedCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).JsonNodeDoubleTapped(node);
    }

    [Fact]
    public async Task SearchRowTappedHandler_HandleAsync_CallsSearchRowTapped()
    {
        var entry = new SearchableTurn();
        var handler = new SearchRowTappedHandler(_target);
        var result = await handler.HandleAsync(new SearchRowTappedCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).SearchRowTapped(entry);
    }

    [Fact]
    public async Task SearchRowDoubleTappedHandler_HandleAsync_CallsSearchRowDoubleTapped()
    {
        var entry = new SearchableTurn();
        var handler = new SearchRowDoubleTappedHandler(_target);
        var result = await handler.HandleAsync(new SearchRowDoubleTappedCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).SearchRowDoubleTapped(entry);
    }
}
