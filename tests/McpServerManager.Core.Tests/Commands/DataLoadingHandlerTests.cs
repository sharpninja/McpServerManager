using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Models.Json;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class DataLoadingHandlerTests
{
    private readonly Mock<ICommandTarget> _target = new();
    private readonly CallContext _ctx = new();

    // --- InitializeFromMcp ---

    [Fact]
    public async Task InitializeFromMcpHandler_HandleAsync_DispatchesAndTracksBackgroundWork()
    {
        _target.Setup(t => t.DispatchToUi(It.IsAny<Action>()));
        _target.Setup(t => t.TrackBackgroundWork(It.IsAny<Task>()));

        var handler = new InitializeFromMcpHandler(_target.Object, _target.Object);
        var result = await handler.HandleAsync(new InitializeFromMcpCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.DispatchToUi(It.IsAny<Action>()), Times.AtLeastOnce);
        _target.Verify(t => t.TrackBackgroundWork(It.IsAny<Task>()), Times.Once);
    }

    // --- RefreshAndLoadAllJson ---

    [Fact]
    public async Task RefreshAndLoadAllJsonHandler_HandleAsync_DispatchesAndTracksBackgroundWork()
    {
        _target.Setup(t => t.DispatchToUi(It.IsAny<Action>()));
        _target.Setup(t => t.TrackBackgroundWork(It.IsAny<Task>()));

        var handler = new RefreshAndLoadAllJsonHandler(_target.Object, _target.Object);
        var result = await handler.HandleAsync(new RefreshAndLoadAllJsonCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.DispatchToUi(It.IsAny<Action>()), Times.AtLeastOnce);
        _target.Verify(t => t.TrackBackgroundWork(It.IsAny<Task>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAndLoadAllJsonHandler_HandleAsync_WithPreselectedAgent()
    {
        _target.Setup(t => t.DispatchToUi(It.IsAny<Action>()));
        _target.Setup(t => t.TrackBackgroundWork(It.IsAny<Task>()));

        var handler = new RefreshAndLoadAllJsonHandler(_target.Object, _target.Object);
        var result = await handler.HandleAsync(new RefreshAndLoadAllJsonCommand("Claude"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.TrackBackgroundWork(It.IsAny<Task>()), Times.Once);
    }

    // --- RefreshAndLoadAgentJson ---

    [Fact]
    public async Task RefreshAndLoadAgentJsonHandler_HandleAsync_DelegatesToAllJsonHandler()
    {
        _target.Setup(t => t.DispatchToUi(It.IsAny<Action>()));
        _target.Setup(t => t.TrackBackgroundWork(It.IsAny<Task>()));

        var handler = new RefreshAndLoadAgentJsonHandler(_target.Object, _target.Object);
        var result = await handler.HandleAsync(new RefreshAndLoadAgentJsonCommand("Copilot"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.TrackBackgroundWork(It.IsAny<Task>()), Times.Once);
    }

    // --- RefreshAndLoadSession ---

    [Fact]
    public async Task RefreshAndLoadSessionHandler_HandleAsync_DispatchesAndTracksBackgroundWork()
    {
        _target.Setup(t => t.DispatchToUi(It.IsAny<Action>()));
        _target.Setup(t => t.TrackBackgroundWork(It.IsAny<Task>()));

        var handler = new RefreshAndLoadSessionHandler(_target.Object, _target.Object);
        var result = await handler.HandleAsync(new RefreshAndLoadSessionCommand("/path/to/session"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.DispatchToUi(It.IsAny<Action>()), Times.AtLeastOnce);
        _target.Verify(t => t.TrackBackgroundWork(It.IsAny<Task>()), Times.Once);
    }

    // --- LoadJsonFile ---

    [Fact]
    public async Task LoadJsonFileHandler_HandleAsync_CallsLoadJson()
    {
        _target.Setup(t => t.DispatchToUi(It.IsAny<Action>()));
        var handler = new LoadJsonFileHandler(_target.Object, _target.Object);
        var result = await handler.HandleAsync(new LoadJsonFileCommand("test.json"), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.LoadJson("test.json"), Times.Once);
    }

    // --- NavigateToNode ---

    [Fact]
    public async Task NavigateToNodeHandler_HandleAsync_CallsGenerateAndNavigate()
    {
        var node = new FileNode("test-node", false);
        var handler = new NavigateToNodeHandler(_target.Object);
        var result = await handler.HandleAsync(new NavigateToNodeCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.GenerateAndNavigate(node), Times.Once);
    }

    [Fact]
    public async Task NavigateToNodeHandler_HandleAsync_NullNode()
    {
        var handler = new NavigateToNodeHandler(_target.Object);
        var result = await handler.HandleAsync(new NavigateToNodeCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.GenerateAndNavigate(null), Times.Once);
    }

    // --- LoadMarkdownFile ---

    [Fact]
    public async Task LoadMarkdownFileHandler_HandleAsync_CallsLoadMarkdownFile()
    {
        var node = new FileNode("readme.md", false);
        var handler = new LoadMarkdownFileHandler(_target.Object);
        var result = await handler.HandleAsync(new LoadMarkdownFileCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.LoadMarkdownFile(node), Times.Once);
    }

    // --- LoadSourceFile ---

    [Fact]
    public async Task LoadSourceFileHandler_HandleAsync_CallsLoadSourceFile()
    {
        var node = new FileNode("Program.cs", false);
        var handler = new LoadSourceFileHandler(_target.Object);
        var result = await handler.HandleAsync(new LoadSourceFileCommand(node), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.LoadSourceFile(node), Times.Once);
    }

    // --- RefreshView ---

    [Fact]
    public async Task RefreshViewHandler_HandleAsync_CallsRefreshAsync()
    {
        _target.Setup(t => t.RefreshAsync()).Returns(Task.CompletedTask);
        var handler = new RefreshViewHandler(_target.Object);
        var result = await handler.HandleAsync(new RefreshViewCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.RefreshAsync(), Times.Once);
    }

    [Fact]
    public async Task RefreshHandler_HandleAsync_CallsRefreshAsync()
    {
        _target.Setup(t => t.RefreshAsync()).Returns(Task.CompletedTask);
        var handler = new RefreshHandler(_target.Object);
        var result = await handler.HandleAsync(new RefreshCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.RefreshAsync(), Times.Once);
    }

    // --- Config ---

    [Fact]
    public async Task OpenAgentConfigHandler_HandleAsync_CallsOpenAgentConfig()
    {
        var handler = new OpenAgentConfigHandler(_target.Object);
        var result = await handler.HandleAsync(new OpenAgentConfigCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.OpenAgentConfig(), Times.Once);
    }

    [Fact]
    public async Task OpenPromptTemplatesHandler_HandleAsync_CallsOpenPromptTemplates()
    {
        var handler = new OpenPromptTemplatesHandler(_target.Object);
        var result = await handler.HandleAsync(new OpenPromptTemplatesCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        _target.Verify(t => t.OpenPromptTemplates(), Times.Once);
    }
}

