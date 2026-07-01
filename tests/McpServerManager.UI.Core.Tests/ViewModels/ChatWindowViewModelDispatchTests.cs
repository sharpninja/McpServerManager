using System.Threading;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
using McpServerManager.UI.Core.ViewModels;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

/// <summary>
/// Pure mock-only dispatch verification tests for ChatWindowViewModel.
/// Uses ViewModelDispatchTestHelper (no AddCqrs, no AddUiCore).
/// Asserts exact message dispatched + resulting observable state.
/// </summary>
public sealed class ChatWindowViewModelDispatchTests
{
    [Fact]
    public async Task LoadPromptsAsync_DispatchesLoadChatPromptsQuery_AndPopulatesTemplates()
    {
        var prompts = new[] { new PromptTemplate { Name = "t1", Template = "hi" } };
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

        // PopulatePrompt is protected; call via reflection for test or expose for verification.
        // For this test we invoke the logic path.
        var prompt = new PromptTemplate { Name = "p1" };
        var method = vm.GetType().GetMethod("PopulatePrompt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var task = (Task?)method.Invoke(vm, new object?[] { prompt });
        if (task != null) await task;

        await dispatcher.Received(1).QueryAsync(Arg.Any<PopulateChatPromptQuery>(), Arg.Any<CancellationToken>());
        Assert.Equal("populated prompt", vm.CurrentInput);
    }
}