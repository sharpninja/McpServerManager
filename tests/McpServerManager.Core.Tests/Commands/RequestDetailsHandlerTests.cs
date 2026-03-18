using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServer.UI.Core.Models.Json;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class RequestDetailsHandlerTests
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task ShowRequestDetailsHandler_HandleAsync_CallsShowRequestDetails()
    {
        var entry = new SearchableTurn { RequestId = "req-1", DisplayText = "Test" };
        var handler = new ShowRequestDetailsHandler(_target.Object);
        var result = await handler.HandleAsync(new ShowRequestDetailsCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.ShowRequestDetails(entry), Times.Once);
    }

    [Fact]
    public async Task CloseRequestDetailsHandler_HandleAsync_CallsCloseRequestDetails()
    {
        var handler = new CloseRequestDetailsHandler(_target.Object);
        var result = await handler.HandleAsync(new CloseRequestDetailsCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.CloseRequestDetails(), Times.Once);
    }

    [Fact]
    public async Task NavigateToPreviousRequestHandler_HandleAsync_CallsNavigateToPreviousRequest()
    {
        var handler = new NavigateToPreviousRequestHandler(_target.Object);
        var result = await handler.HandleAsync(new NavigateToPreviousRequestCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.NavigateToPreviousRequest(), Times.Once);
    }

    [Fact]
    public async Task NavigateToNextRequestHandler_HandleAsync_CallsNavigateToNextRequest()
    {
        var handler = new NavigateToNextRequestHandler(_target.Object);
        var result = await handler.HandleAsync(new NavigateToNextRequestCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.NavigateToNextRequest(), Times.Once);
    }
}

