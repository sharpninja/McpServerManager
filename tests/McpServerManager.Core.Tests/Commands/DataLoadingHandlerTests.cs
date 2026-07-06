using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Models.Json;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class DataLoadingHandlerTests
{
    private readonly ICommandTarget _target = Substitute.For<ICommandTarget>();
    private readonly CallContext _ctx = new();

    // --- InitializeFromMcp ---

    [Fact]
    public async Task InitializeFromMcpHandler_HandleAsync_DispatchesAndTracksBackgroundWork()
    {
        var handler = new InitializeFromMcpHandler(_target, _target);
        var result = await handler.HandleAsync(new InitializeFromMcpCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received().DispatchToUi(Arg.Any<Action>());
        _target.Received(1).TrackBackgroundWork(Arg.Any<Task>());
    }

    // --- RefreshAndLoadAllJson ---

    [Fact]
    public async Task RefreshAndLoadAllJsonHandler_HandleAsync_DispatchesAndTracksBackgroundWork()
    {
        var handler = new RefreshAndLoadAllJsonHandler(_target, _target);
        var result = await handler.HandleAsync(new RefreshAndLoadAllJsonCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received().DispatchToUi(Arg.Any<Action>());
        _target.Received(1).TrackBackgroundWork(Arg.Any<Task>());
    }

    [Fact]
    public async Task RefreshAndLoadAllJsonHandler_HandleAsync_WithPreselectedAgent()
    {
        var handler = new RefreshAndLoadAllJsonHandler(_target, _target);
        var result = await handler.HandleAsync(new RefreshAndLoadAllJsonCommand("Claude"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).TrackBackgroundWork(Arg.Any<Task>());
    }

    // --- RefreshAndLoadAgentJson ---

    [Fact]
    public async Task RefreshAndLoadAgentJsonHandler_HandleAsync_DelegatesToAllJsonHandler()
    {
        var handler = new RefreshAndLoadAgentJsonHandler(_target, _target);
        var result = await handler.HandleAsync(new RefreshAndLoadAgentJsonCommand("Copilot"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).TrackBackgroundWork(Arg.Any<Task>());
    }

    // --- RefreshAndLoadSession ---

    [Fact]
    public async Task RefreshAndLoadSessionHandler_HandleAsync_DispatchesAndTracksBackgroundWork()
    {
        var handler = new RefreshAndLoadSessionHandler(_target, _target);
        var result = await handler.HandleAsync(new RefreshAndLoadSessionCommand("/path/to/session"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received().DispatchToUi(Arg.Any<Action>());
        _target.Received(1).TrackBackgroundWork(Arg.Any<Task>());
    }

    // --- LoadJsonFile ---

    [Fact]
    public async Task LoadJsonFileHandler_HandleAsync_CallsLoadJson()
    {
        var handler = new LoadJsonFileHandler(_target, _target);
        var result = await handler.HandleAsync(new LoadJsonFileCommand("test.json"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).LoadJson("test.json");
    }

    // --- NavigateToNode ---

    [Fact]
    public async Task NavigateToNodeHandler_HandleAsync_CallsGenerateAndNavigate()
    {
        var node = new FileNode("test-node", false);
        var handler = new NavigateToNodeHandler(_target);
        var result = await handler.HandleAsync(new NavigateToNodeCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).GenerateAndNavigate(node);
    }

    [Fact]
    public async Task NavigateToNodeHandler_HandleAsync_NullNode()
    {
        var handler = new NavigateToNodeHandler(_target);
        var result = await handler.HandleAsync(new NavigateToNodeCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).GenerateAndNavigate(null);
    }

    // --- LoadMarkdownFile ---

    [Fact]
    public async Task LoadMarkdownFileHandler_HandleAsync_CallsLoadMarkdownFile()
    {
        var node = new FileNode("readme.md", false);
        var handler = new LoadMarkdownFileHandler(_target);
        var result = await handler.HandleAsync(new LoadMarkdownFileCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).LoadMarkdownFile(node);
    }

    // --- LoadSourceFile ---

    [Fact]
    public async Task LoadSourceFileHandler_HandleAsync_CallsLoadSourceFile()
    {
        var node = new FileNode("Program.cs", false);
        var handler = new LoadSourceFileHandler(_target);
        var result = await handler.HandleAsync(new LoadSourceFileCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).LoadSourceFile(node);
    }

    // --- RefreshView ---

    [Fact]
    public async Task RefreshViewHandler_HandleAsync_CallsRefreshAsync()
    {
        _target.RefreshAsync().Returns(Task.CompletedTask);
        var handler = new RefreshViewHandler(_target);
        var result = await handler.HandleAsync(new RefreshViewCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        await _target.Received(1).RefreshAsync();
    }

    [Fact]
    public async Task RefreshHandler_HandleAsync_CallsRefreshAsync()
    {
        _target.RefreshAsync().Returns(Task.CompletedTask);
        var handler = new RefreshHandler(_target);
        var result = await handler.HandleAsync(new RefreshCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        await _target.Received(1).RefreshAsync();
    }

    // --- Config ---

    [Fact]
    public async Task OpenAgentConfigHandler_HandleAsync_CallsOpenAgentConfig()
    {
        var handler = new OpenAgentConfigHandler(_target);
        var result = await handler.HandleAsync(new OpenAgentConfigCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).OpenAgentConfig();
    }

    [Fact]
    public async Task OpenPromptTemplatesHandler_HandleAsync_CallsOpenPromptTemplates()
    {
        var handler = new OpenPromptTemplatesHandler(_target);
        var result = await handler.HandleAsync(new OpenPromptTemplatesCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Received(1).OpenPromptTemplates();
    }
}
