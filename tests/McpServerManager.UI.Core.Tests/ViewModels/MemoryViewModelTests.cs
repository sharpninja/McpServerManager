using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.ViewModels;

public sealed class MemoryViewModelTests
{
    private static readonly MemoryDetail Sample = new(
        "mem-1",
        "prefs",
        MemoryScope.Workspace,
        @"E:\repo",
        "remember this",
        2,
        DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
        DateTimeOffset.Parse("2026-09-19T00:00:00Z"),
        "viewer");

    [Fact]
    public async Task MemoryListViewModel_LoadAsync_MapsFiltersAndItems()
    {
        var api = Substitute.For<IMemoryApiClient>();
        api.ListMemoriesAsync(Arg.Any<ListMemoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<ListMemoriesQuery>();
                Assert.Equal(MemoryScope.Workspace, query.Scope);
                Assert.Equal("prefs", query.Category);
                Assert.Equal("remember", query.Keyword);
                return new ListMemoriesResult(
                    [new MemoryListItem(Sample.Id, Sample.Category, Sample.Scope, Sample.WorkspacePath, Sample.Text, Sample.Version, Sample.UpdatedAtUtc, Sample.UpdatedBy)],
                    1);
            });

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        var viewModel = host.GetRequiredService<MemoryListViewModel>();
        viewModel.Scope = MemoryScope.Workspace;
        viewModel.Category = " prefs ";
        viewModel.Keyword = " remember ";

        await viewModel.LoadAsync();

        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsLoading);
        Assert.Equal(1, viewModel.TotalCount);
        Assert.Single(viewModel.Items);
        Assert.Equal("mem-1", viewModel.Items[0].Id);
        Assert.Equal(2, viewModel.Items[0].Version);
        Assert.Equal(McpArea.Memory, viewModel.Area);
    }

    [Fact]
    public async Task MemoryDetailViewModel_LoadAndSave_UsesAddThenUpdate_WithoutOcc()
    {
        var api = Substitute.For<IMemoryApiClient>();
        api.GetMemoryAsync("mem-1", Arg.Any<CancellationToken>()).Returns(Sample);
        api.AddMemoryAsync(Arg.Any<AddMemoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryMutationOutcome(true, null, Sample));
        api.UpdateMemoryAsync(Arg.Any<UpdateMemoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<UpdateMemoryCommand>();
                Assert.Equal("mem-1", command.MemoryId);
                Assert.Null(command.GetType().GetProperty("ExpectedVersion"));
                return new MemoryMutationOutcome(true, null, Sample with { Text = command.Text ?? Sample.Text, Version = 3 });
            });

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        var viewModel = host.GetRequiredService<MemoryDetailViewModel>();

        viewModel.BeginNewDraft();
        viewModel.EditorCategory = "prefs";
        viewModel.EditorText = "remember this";
        Assert.True(await viewModel.SaveAsync());
        await api.Received(1).AddMemoryAsync(Arg.Any<AddMemoryCommand>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, viewModel.DisplayVersion);

        await viewModel.LoadAsync("mem-1");
        Assert.Equal("mem-1", viewModel.EditorId);
        Assert.False(viewModel.IsNewDraft);

        viewModel.EditorText = "updated";
        Assert.True(await viewModel.SaveAsync());
        await api.Received(1).UpdateMemoryAsync(Arg.Any<UpdateMemoryCommand>(), Arg.Any<CancellationToken>());
        Assert.Equal(3, viewModel.DisplayVersion);
        Assert.Equal(McpArea.Memory, viewModel.Area);
    }

    [Fact]
    public async Task MemoryDetailViewModel_DeleteAsync_RemovesAndResetsDraft()
    {
        var api = Substitute.For<IMemoryApiClient>();
        api.GetMemoryAsync("mem-1", Arg.Any<CancellationToken>()).Returns(Sample);
        api.RemoveMemoryAsync(Arg.Any<RemoveMemoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryMutationOutcome(true, null, Sample));

        using var host = UiCoreTestHost.Create(services => services.AddSingleton(api));
        var viewModel = host.GetRequiredService<MemoryDetailViewModel>();
        await viewModel.LoadAsync("mem-1");

        Assert.True(await viewModel.DeleteAsync());
        await api.Received(1).RemoveMemoryAsync(
            Arg.Is<RemoveMemoryCommand>(c => c.MemoryId == "mem-1"),
            Arg.Any<CancellationToken>());
        Assert.True(viewModel.IsNewDraft);
        Assert.Null(viewModel.Detail);
    }
}
