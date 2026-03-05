using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;
using Moq;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class ChatHandlerTests
{
    private readonly CallContext _ctx = new();

    // --- ChatOpenAgentConfig ---

    [Fact]
    public async Task ChatOpenAgentConfigHandler_HandleAsync_CallsOpenAgentConfigInEditor()
    {
        var svc = new Mock<IChatConfigFilesService>();
        var expected = new ChatFileOpenResult(true, "/path/config.json");
        svc.Setup(s => s.OpenAgentConfigInEditor()).Returns(expected);

        var handler = new ChatOpenAgentConfigHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatOpenAgentConfigCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        svc.Verify(s => s.OpenAgentConfigInEditor(), Times.Once);
    }

    // --- ChatOpenPromptTemplates ---

    [Fact]
    public async Task ChatOpenPromptTemplatesHandler_HandleAsync_CallsOpenPromptTemplatesInEditor()
    {
        var svc = new Mock<IChatConfigFilesService>();
        var expected = new ChatFileOpenResult(true, "/path/prompts.yaml");
        svc.Setup(s => s.OpenPromptTemplatesInEditor()).Returns(expected);

        var handler = new ChatOpenPromptTemplatesHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatOpenPromptTemplatesCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        svc.Verify(s => s.OpenPromptTemplatesInEditor(), Times.Once);
    }

    // --- ChatLoadPrompts ---

    [Fact]
    public async Task ChatLoadPromptsHandler_HandleAsync_ReturnsPromptTemplates()
    {
        var svc = new Mock<IChatPromptTemplateService>();
        var templates = new List<PromptTemplate>
        {
            new() { Name = "Summarize", Template = "Summarize this" }
        };
        svc.Setup(s => s.GetPromptTemplates()).Returns(templates);

        var handler = new ChatLoadPromptsHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatLoadPromptsCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("Summarize");
    }

    // --- ChatSubmitPrompt ---

    [Fact]
    public async Task ChatSubmitPromptHandler_HandleAsync_WithTemplate_ReturnsShouldSendTrue()
    {
        var prompt = new PromptTemplate { Name = "Test", Template = "Do the thing" };
        var handler = new ChatSubmitPromptHandler();
        var result = await handler.HandleAsync(new ChatSubmitPromptCommand(prompt), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ShouldSend.Should().BeTrue();
        result.Value.PromptText.Should().Be("Do the thing");
    }

    [Fact]
    public async Task ChatSubmitPromptHandler_HandleAsync_NullPrompt_ReturnsShouldSendFalse()
    {
        var handler = new ChatSubmitPromptHandler();
        var result = await handler.HandleAsync(new ChatSubmitPromptCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ShouldSend.Should().BeFalse();
        result.Value.PromptText.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatSubmitPromptHandler_HandleAsync_EmptyTemplate_ReturnsShouldSendFalse()
    {
        var prompt = new PromptTemplate { Name = "Empty", Template = "   " };
        var handler = new ChatSubmitPromptHandler();
        var result = await handler.HandleAsync(new ChatSubmitPromptCommand(prompt), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ShouldSend.Should().BeFalse();
    }

    // --- ChatPopulatePrompt ---

    [Fact]
    public async Task ChatPopulatePromptHandler_HandleAsync_ReturnsTemplateText()
    {
        var prompt = new PromptTemplate { Name = "Q", Template = "  Ask a question  " };
        var handler = new ChatPopulatePromptHandler();
        var result = await handler.HandleAsync(new ChatPopulatePromptCommand(prompt), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Ask a question");
    }

    [Fact]
    public async Task ChatPopulatePromptHandler_HandleAsync_NullPrompt_ReturnsEmpty()
    {
        var handler = new ChatPopulatePromptHandler();
        var result = await handler.HandleAsync(new ChatPopulatePromptCommand(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // --- ChatLoadModels ---

    [Fact]
    public async Task ChatLoadModelsHandler_HandleAsync_ReturnsModels()
    {
        var svc = new Mock<IChatModelDiscoveryService>();
        var models = new List<string> { "llama3", "mistral" };
        svc.Setup(s => s.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(models);

        var handler = new ChatLoadModelsHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatLoadModelsQuery("llama3"), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReachable.Should().BeTrue();
        result.Value.Models.Should().HaveCount(2);
        result.Value.SelectedModel.Should().Be("llama3");
    }

    [Fact]
    public async Task ChatLoadModelsHandler_HandleAsync_PreferredNotInList_SelectsFirst()
    {
        var svc = new Mock<IChatModelDiscoveryService>();
        var models = new List<string> { "llama3", "mistral" };
        svc.Setup(s => s.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(models);

        var handler = new ChatLoadModelsHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatLoadModelsQuery("gpt-4"), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SelectedModel.Should().Be("llama3");
    }

    [Fact]
    public async Task ChatLoadModelsHandler_HandleAsync_ServiceThrows_ReturnsNotReachable()
    {
        var svc = new Mock<IChatModelDiscoveryService>();
        svc.Setup(s => s.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("connection refused"));

        var handler = new ChatLoadModelsHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatLoadModelsQuery(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReachable.Should().BeFalse();
        result.Value.Models.Should().BeEmpty();
    }

    // --- ChatSendMessage ---

    [Fact]
    public async Task ChatSendMessageHandler_HandleAsync_ReturnsReply()
    {
        var svc = new Mock<IChatSendOrchestrationService>();
        svc.Setup(s => s.SendMessageAsync(It.IsAny<ChatSendRequest>(), It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync("Hello back!");

        var request = new ChatSendRequest("Hi", "context", "llama3");
        var handler = new ChatSendMessageHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatSendMessageCommand(request), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.ReplyText.Should().Be("Hello back!");
        result.Value.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task ChatSendMessageHandler_HandleAsync_Cancelled_ReturnsCancelledResult()
    {
        var svc = new Mock<IChatSendOrchestrationService>();
        svc.Setup(s => s.SendMessageAsync(It.IsAny<ChatSendRequest>(), It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new OperationCanceledException());

        var request = new ChatSendRequest("Hi", "context", "llama3");
        var handler = new ChatSendMessageHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatSendMessageCommand(request), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WasCancelled.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ChatSendMessageHandler_HandleAsync_Error_ReturnsFailure()
    {
        var svc = new Mock<IChatSendOrchestrationService>();
        svc.Setup(s => s.SendMessageAsync(It.IsAny<ChatSendRequest>(), It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("backend error"));

        var request = new ChatSendRequest("Hi", "context", "llama3");
        var handler = new ChatSendMessageHandler(svc.Object);
        var result = await handler.HandleAsync(new ChatSendMessageCommand(request), _ctx);

        result.IsSuccess.Should().BeFalse();
    }
}
