using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
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
        var prompts = new[] { new PromptTemplate { Name = "t1", Content = "hi" } };
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateChatWindow();

        ViewModelDispatchTestHelper.SetupQueryResult<LoadChatPromptsQuery, IReadOnlyList<PromptTemplate>>(dispatcher, prompts);

        await vm.LoadPromptsAsync();

        await dispatcher.Received(1).QueryAsync(Arg.Is<LoadChatPromptsQuery>(q => q != null), Arg.Any<CancellationToken>());
        Assert.Single(vm.PromptTemplates);
        Assert.Equal("t1", vm.PromptTemplates[0].Name);
    }

    [Fact]
    public async Task LoadModelsAsync_DispatchesLoadChatModelsQuery_AndPopulatesModels()
    {
        var modelsResult = new ChatLoadModelsResult(true, new[] { "llama3" }, "llama3");
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateChatWindow(initialModel: "llama3");

        ViewModelDispatchTestHelper.SetupQueryResult<LoadChatModelsQuery, ChatLoadModelsResult>(dispatcher, modelsResult);

        await vm.LoadModelsAsync();

        await dispatcher.Received(1).QueryAsync(Arg.Any<LoadChatModelsQuery>(), Arg.Any<CancellationToken>());
        Assert.Contains("llama3", vm.AvailableModels);
        Assert.Equal("llama3", vm.SelectedModel);
    }

    [Fact]
    public async Task PopulatePrompt_DispatchesPopulateChatPromptQuery_AndSetsCurrentInput()
    {
        var (dispatcher, vm) = ViewModelDispatchTestHelper.CreateChatWindow();
        ViewModelDispatchTestHelper.SetupQueryResult<PopulateChatPromptQuery, string>(dispatcher, "populated prompt");

        var prompt = new PromptTemplate { Name = "p1", Content = "template" };
        // Call the protected for test via reflection or make internal for test; here assume accessible or test via public wrapper
        await vm.GetType().GetMethod("PopulatePrompt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
            .Invoke(vm, new object?[] { prompt });

        await dispatcher.Received(1).QueryAsync(Arg.Any<PopulateChatPromptQuery>(), Arg.Any<CancellationToken>());
        Assert.Equal("populated prompt", vm.CurrentInput);
    }
}