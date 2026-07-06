using FluentAssertions;
using McpServer.Cqrs;
using McpServerManager.Core.Commands;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;
using NSubstitute;
using Xunit;

namespace McpServerManager.Core.Tests.Commands;

public sealed class ChatHandlerTests
{
    private readonly CallContext _ctx = new();

    // --- ChatOpenAgentConfig ---

    [Fact]
    public async Task ChatOpenAgentConfigHandler_HandleAsync_CallsOpenAgentConfigInEditor()
    {
        var svc = Substitute.For<IChatConfigFilesService>();
        var expected = new ChatFileOpenResult(true, "/path/config.json");
        svc.OpenAgentConfigInEditor().Returns(expected);

        var handler = new ChatOpenAgentConfigHandler(svc);
        var result = await handler.HandleAsync(new ChatOpenAgentConfigCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        svc.Received(1).OpenAgentConfigInEditor();
    }

    // --- ChatOpenPromptTemplates ---

    [Fact]
    public async Task ChatOpenPromptTemplatesHandler_HandleAsync_CallsOpenPromptTemplatesInEditor()
    {
        var svc = Substitute.For<IChatConfigFilesService>();
        var expected = new ChatFileOpenResult(true, "/path/prompts.yaml");
        svc.OpenPromptTemplatesInEditor().Returns(expected);

        var handler = new ChatOpenPromptTemplatesHandler(svc);
        var result = await handler.HandleAsync(new ChatOpenPromptTemplatesCommand(), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
        svc.Received(1).OpenPromptTemplatesInEditor();
    }

    // --- ChatLoadPrompts ---

    [Fact]
    public async Task ChatLoadPromptsHandler_HandleAsync_ReturnsPromptTemplates()
    {
        var svc = Substitute.For<IChatPromptTemplateService>();
        var templates = new List<PromptTemplate>
        {
            new() { Name = "Summarize", Template = "Summarize this" }
        };
        svc.GetPromptTemplates().Returns(templates);

        var handler = new ChatLoadPromptsHandler(svc);
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
        var svc = Substitute.For<IChatModelDiscoveryService>();
        var models = new List<string> { "llama3", "mistral" };
        svc.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(models));

        var handler = new ChatLoadModelsHandler(svc);
        var result = await handler.HandleAsync(new ChatLoadModelsQuery("llama3"), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReachable.Should().BeTrue();
        result.Value.Models.Should().HaveCount(2);
        result.Value.SelectedModel.Should().Be("llama3");
    }

    [Fact]
    public async Task ChatLoadModelsHandler_HandleAsync_PreferredNotInList_SelectsFirst()
    {
        var svc = Substitute.For<IChatModelDiscoveryService>();
        var models = new List<string> { "llama3", "mistral" };
        svc.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(models));

        var handler = new ChatLoadModelsHandler(svc);
        var result = await handler.HandleAsync(new ChatLoadModelsQuery("gpt-4"), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SelectedModel.Should().Be("llama3");
    }

    [Fact]
    public async Task ChatLoadModelsHandler_HandleAsync_ServiceThrows_ReturnsNotReachable()
    {
        var svc = Substitute.For<IChatModelDiscoveryService>();
        svc.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<string>>(new HttpRequestException("connection refused")));

        var handler = new ChatLoadModelsHandler(svc);
        var result = await handler.HandleAsync(new ChatLoadModelsQuery(null), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsReachable.Should().BeFalse();
        result.Value.Models.Should().BeEmpty();
    }

    // --- ChatSendMessage ---

    [Fact]
    public async Task ChatSendMessageHandler_HandleAsync_ReturnsReply()
    {
        var svc = Substitute.For<IChatSendOrchestrationService>();
        svc.SendMessageAsync(Arg.Any<ChatSendRequest>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("Hello back!"));

        var request = new ChatSendRequest("Hi", "context", "llama3");
        var handler = new ChatSendMessageHandler(svc);
        var result = await handler.HandleAsync(new ChatSendMessageCommand(request), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.ReplyText.Should().Be("Hello back!");
        result.Value.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task ChatSendMessageHandler_HandleAsync_Cancelled_ReturnsCancelledResult()
    {
        var svc = Substitute.For<IChatSendOrchestrationService>();
        svc.SendMessageAsync(Arg.Any<ChatSendRequest>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new OperationCanceledException()));

        var request = new ChatSendRequest("Hi", "context", "llama3");
        var handler = new ChatSendMessageHandler(svc);
        var result = await handler.HandleAsync(new ChatSendMessageCommand(request), _ctx);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WasCancelled.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ChatSendMessageHandler_HandleAsync_Error_ReturnsFailure()
    {
        var svc = Substitute.For<IChatSendOrchestrationService>();
        svc.SendMessageAsync(Arg.Any<ChatSendRequest>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("backend error")));

        var request = new ChatSendRequest("Hi", "context", "llama3");
        var handler = new ChatSendMessageHandler(svc);
        var result = await handler.HandleAsync(new ChatSendMessageCommand(request), _ctx);

        result.IsSuccess.Should().BeFalse();
    }
}
