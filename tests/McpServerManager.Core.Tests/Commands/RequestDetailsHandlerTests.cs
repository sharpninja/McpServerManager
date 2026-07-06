using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models.Json;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class RequestDetailsHandlerTests
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget>();
    private readonly CallContext _ctx = new();

    [Fact]
    public async Task ShowRequestDetailsHandler_HandleAsync_CallsShowRequestDetails()
    {
        var entry = new SearchableTurn { RequestId = "req-1", DisplayText = "Test" };
        var handler = new ShowRequestDetailsHandler(_target);
        var result = await handler.HandleAsync(new ShowRequestDetailsCommand(entry), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).ShowRequestDetails(entry);
    }

    [Fact]
    public async Task CloseRequestDetailsHandler_HandleAsync_CallsCloseRequestDetails()
    {
        var handler = new CloseRequestDetailsHandler(_target);
        var result = await handler.HandleAsync(new CloseRequestDetailsCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).CloseRequestDetails();
    }

    [Fact]
    public async Task NavigateToPreviousRequestHandler_HandleAsync_CallsNavigateToPreviousRequest()
    {
        var handler = new NavigateToPreviousRequestHandler(_target);
        var result = await handler.HandleAsync(new NavigateToPreviousRequestCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).NavigateToPreviousRequest();
    }

    [Fact]
    public async Task NavigateToNextRequestHandler_HandleAsync_CallsNavigateToNextRequest()
    {
        var handler = new NavigateToNextRequestHandler(_target);
        var result = await handler.HandleAsync(new NavigateToNextRequestCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).NavigateToNextRequest();
    }
}
