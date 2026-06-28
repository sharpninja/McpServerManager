using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

/// <summary>
/// Dispatch verification tests for ChatWindowViewModel using pure mocked Dispatcher (no AddCqrs/AddUiCore).
/// Written first per Byrd. These must fail until VM dispatches instead of calling _chatService directly.
/// </summary>
public sealed class ChatWindowViewModelTests
{
    [Fact]
    public async Task LoadPromptsAsync_DispatchesLoadChatPromptsQuery_AndPopulatesTemplates()
    {
        var prompts = new[] { new PromptTemplate { Name = "t1", Template = "hi" } };
        var (_, vm, chatService) = ViewModelDispatchTestHelper.CreateChatWindow();
        chatService.LoadPromptsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PromptTemplate>>(prompts));

        await vm.LoadPromptsAsync();

        await chatService.Received(1).LoadPromptsAsync(Arg.Any<CancellationToken>());
        Assert.Single(vm.PromptTemplates);
        Assert.Equal("t1", vm.PromptTemplates[0].Name);
    }

    [Fact]
    public async Task LoadModelsAsync_DispatchesLoadChatModelsQuery_AndPopulatesModels()
    {
        var modelsResult = new ChatLoadModelsResult(true, new[] { "llama3" }, "llama3");
        var (_, vm, chatService) = ViewModelDispatchTestHelper.CreateChatWindow(initialModel: "llama3");
        chatService.LoadModelsAsync("llama3", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(modelsResult));

        await vm.LoadModelsAsync();

        await chatService.Received(1).LoadModelsAsync("llama3", Arg.Any<CancellationToken>());
        Assert.Contains("llama3", vm.AvailableModels);
        Assert.Equal("llama3", vm.SelectedModel);
    }

    [Fact]
    public async Task PopulatePrompt_DispatchesPopulateChatPromptQuery_AndSetsCurrentInput()
    {
        var (_, vm, chatService) = ViewModelDispatchTestHelper.CreateChatWindow();
        chatService.PopulatePromptAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("populated prompt"));

        var prompt = new PromptTemplate { Name = "p1", Template = "template" };
        // Call the protected for test via reflection or make internal for test; here assume accessible or test via public wrapper
        var task = (Task)vm.GetType()
            .GetMethod("PopulatePrompt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
            .Invoke(vm, new object?[] { prompt })!;
        await task;

        await chatService.Received(1).PopulatePromptAsync(prompt, Arg.Any<CancellationToken>());
        Assert.Equal("populated prompt", vm.CurrentInput);
    }
}
