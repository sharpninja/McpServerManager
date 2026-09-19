using System.Reflection;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Xunit;
using UiScope = McpServerManager.UI.Core.Messages.MemoryScope;

namespace McpServerManager.UI.Core.Tests;

public sealed class MemoryContractTests
{
    [Fact]
    public void MemoryArea_AndActionKeys_UseViewerVerbs_WithoutOperatorOrOcc()
    {
        Assert.True(Enum.IsDefined(McpArea.Memory));
        Assert.Equal("memory.list", McpActionKeys.MemoryList);
        Assert.Equal("memory.get", McpActionKeys.MemoryGet);
        Assert.Equal("memory.add", McpActionKeys.MemoryAdd);
        Assert.Equal("memory.update", McpActionKeys.MemoryUpdate);
        Assert.Equal("memory.remove", McpActionKeys.MemoryRemove);

        var keys = string.Join(' ',
            McpActionKeys.MemoryList,
            McpActionKeys.MemoryGet,
            McpActionKeys.MemoryAdd,
            McpActionKeys.MemoryUpdate,
            McpActionKeys.MemoryRemove);
        Assert.DoesNotContain("operator", keys, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expectedVersion", keys, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MemoryMessages_DoNotExposeExpectedVersion()
    {
        var types = new[]
        {
            typeof(ListMemoriesQuery),
            typeof(ListMemoriesResult),
            typeof(MemoryListItem),
            typeof(GetMemoryQuery),
            typeof(MemoryDetail),
            typeof(AddMemoryCommand),
            typeof(UpdateMemoryCommand),
            typeof(RemoveMemoryCommand),
            typeof(MemoryMutationOutcome),
        };

        foreach (var type in types)
        {
            var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name);
            Assert.DoesNotContain("ExpectedVersion", names, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("Operator", names, StringComparer.OrdinalIgnoreCase);
        }

        Assert.Contains(typeof(MemoryDetail).GetProperties(), p => p.Name == "Version");
        Assert.Contains(typeof(MemoryListItem).GetProperties(), p => p.Name == "Version");
    }

    [Fact]
    public void IMemoryApiClient_ExposesFiveVerbs()
    {
        var methods = typeof(IMemoryApiClient).GetMethods().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(IMemoryApiClient.ListMemoriesAsync), methods);
        Assert.Contains(nameof(IMemoryApiClient.GetMemoryAsync), methods);
        Assert.Contains(nameof(IMemoryApiClient.AddMemoryAsync), methods);
        Assert.Contains(nameof(IMemoryApiClient.UpdateMemoryAsync), methods);
        Assert.Contains(nameof(IMemoryApiClient.RemoveMemoryAsync), methods);
    }

    [Fact]
    public void MemoryMessageMapper_MapsVersionDisplayOnly_AndOmitsExpectedVersionOnUpdate()
    {
        var item = new MemoryItem
        {
            Id = "mem-1",
            Category = "prefs",
            Scope = McpServer.Client.Models.MemoryScope.Workspace,
            WorkspacePath = @"E:\repo",
            Text = "remember this",
            Version = 7,
            CreatedAtUtc = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-09-19T00:00:00Z"),
            UpdatedBy = "viewer",
        };

        var detail = MemoryMessageMapper.ToDetail(item);
        Assert.Equal(7, detail.Version);
        Assert.Equal(UiScope.Workspace, detail.Scope);

        var request = MemoryMessageMapper.ToUpdateRequest(new UpdateMemoryCommand
        {
            MemoryId = "mem-1",
            Text = "updated",
        });

        var requestNames = request.GetType().GetProperties().Select(p => p.Name);
        Assert.DoesNotContain("ExpectedVersion", requestNames, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("updated", request.Text);
        Assert.Null(request.Category);
    }
}
