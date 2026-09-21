using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Handlers;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.Tests.TestInfrastructure;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Handlers;

/// <summary>AC16: five memory verbs allow/deny as Viewer; no Operator strings.</summary>
public sealed class MemoryHandlerAuthContractTests
{
    private static readonly MemoryDetail Sample = new(
        "mem-1",
        "prefs",
        MemoryScope.Workspace,
        @"E:\repo",
        "remember this",
        3,
        DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
        DateTimeOffset.Parse("2026-09-19T00:00:00Z"),
        "viewer");

    public static TheoryData<string> MemoryActionKeys => new()
    {
        McpActionKeys.MemoryList,
        McpActionKeys.MemoryGet,
        McpActionKeys.MemoryAdd,
        McpActionKeys.MemoryUpdate,
        McpActionKeys.MemoryRemove,
    };

    [Theory]
    [MemberData(nameof(MemoryActionKeys))]
    public void MemoryActionKeys_AreViewerVerbs_WithoutOperator(string actionKey)
    {
        Assert.StartsWith("memory.", actionKey, StringComparison.Ordinal);
        Assert.DoesNotContain("operator", actionKey, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            actionKey,
            [
                McpActionKeys.MemoryList,
                McpActionKeys.MemoryGet,
                McpActionKeys.MemoryAdd,
                McpActionKeys.MemoryUpdate,
                McpActionKeys.MemoryRemove
            ],
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task List_WhenViewerAllowed_CallsClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var query = new ListMemoriesQuery { Category = "prefs" };
        client.ListMemoriesAsync(query, Arg.Any<CancellationToken>())
            .Returns(new ListMemoriesResult([ToListItem(Sample)], 1));
        var handler = new ListMemoriesQueryHandler(client, AllowViewer(), NullLogger<ListMemoriesQueryHandler>.Instance);

        var result = await handler.HandleAsync(query, CallContextFactory.Create());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        await client.Received(1).ListMemoriesAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WhenDenied_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new ListMemoriesQueryHandler(client, Deny(McpActionKeys.MemoryList), NullLogger<ListMemoriesQueryHandler>.Instance);

        var result = await handler.HandleAsync(new ListMemoriesQuery(), CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal("Permission denied: requires viewer.", result.Error);
        Assert.DoesNotContain("operator", result.Error, StringComparison.OrdinalIgnoreCase);
        await client.DidNotReceive().ListMemoriesAsync(Arg.Any<ListMemoriesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WhenViewerAllowed_CallsClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        client.GetMemoryAsync("mem-1", Arg.Any<CancellationToken>()).Returns(Sample);
        var handler = new GetMemoryQueryHandler(client, AllowViewer(), NullLogger<GetMemoryQueryHandler>.Instance);

        var result = await handler.HandleAsync(new GetMemoryQuery("mem-1"), CallContextFactory.Create());

        Assert.True(result.IsSuccess);
        Assert.Equal("mem-1", result.Value!.Id);
        await client.Received(1).GetMemoryAsync("mem-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WhenDenied_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new GetMemoryQueryHandler(client, Deny(McpActionKeys.MemoryGet), NullLogger<GetMemoryQueryHandler>.Instance);

        var result = await handler.HandleAsync(new GetMemoryQuery("mem-1"), CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal("Permission denied: requires viewer.", result.Error);
        await client.DidNotReceive().GetMemoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_WhenViewerAllowed_CallsClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var command = new AddMemoryCommand { Category = "prefs", Text = "remember this" };
        client.AddMemoryAsync(command, Arg.Any<CancellationToken>())
            .Returns(new MemoryMutationOutcome(true, null, Sample));
        var handler = new AddMemoryCommandHandler(
            client,
            AllowViewer(),
            Workspace(@"E:\repo"),
            NullLogger<AddMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(command, CallContextFactory.Create());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        await client.Received(1).AddMemoryAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_WhenDenied_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new AddMemoryCommandHandler(
            client,
            Deny(McpActionKeys.MemoryAdd),
            Workspace(null),
            NullLogger<AddMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new AddMemoryCommand { Category = "prefs", Text = "remember this" },
            CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal("Permission denied: requires viewer.", result.Error);
        await client.DidNotReceive().AddMemoryAsync(Arg.Any<AddMemoryCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenViewerAllowed_CallsClient_WithoutOcc()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var command = new UpdateMemoryCommand { MemoryId = "mem-1", Text = "updated" };
        client.UpdateMemoryAsync(command, Arg.Any<CancellationToken>())
            .Returns(new MemoryMutationOutcome(true, null, Sample with { Text = "updated", Version = 4 }));
        var handler = new UpdateMemoryCommandHandler(
            client,
            AllowViewer(),
            Workspace(@"E:\repo"),
            NullLogger<UpdateMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(command, CallContextFactory.Create());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        Assert.Null(command.GetType().GetProperty("ExpectedVersion"));
        await client.Received(1).UpdateMemoryAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenDenied_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new UpdateMemoryCommandHandler(
            client,
            Deny(McpActionKeys.MemoryUpdate),
            Workspace(null),
            NullLogger<UpdateMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new UpdateMemoryCommand { MemoryId = "mem-1", Text = "updated" },
            CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal("Permission denied: requires viewer.", result.Error);
        await client.DidNotReceive().UpdateMemoryAsync(Arg.Any<UpdateMemoryCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_WhenViewerAllowed_CallsClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var command = new RemoveMemoryCommand { MemoryId = "mem-1" };
        client.RemoveMemoryAsync(command, Arg.Any<CancellationToken>())
            .Returns(new MemoryMutationOutcome(true, null, Sample));
        var handler = new RemoveMemoryCommandHandler(client, AllowViewer(), NullLogger<RemoveMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(command, CallContextFactory.Create());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        await client.Received(1).RemoveMemoryAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_WhenDenied_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new RemoveMemoryCommandHandler(client, Deny(McpActionKeys.MemoryRemove), NullLogger<RemoveMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(new RemoveMemoryCommand { MemoryId = "mem-1" }, CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal("Permission denied: requires viewer.", result.Error);
        await client.DidNotReceive().RemoveMemoryAsync(Arg.Any<RemoveMemoryCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_GlobalOnDefaultWorkspace_CallsClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var command = new AddMemoryCommand
        {
            Category = "prefs",
            Text = "remember this",
            Scope = MemoryScope.Global,
        };
        client.AddMemoryAsync(command, Arg.Any<CancellationToken>())
            .Returns(new MemoryMutationOutcome(true, null, Sample with { Scope = MemoryScope.Global, WorkspacePath = null }));
        var handler = new AddMemoryCommandHandler(
            client,
            AllowViewer(),
            Workspace(null),
            NullLogger<AddMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(command, CallContextFactory.Create());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        await client.Received(1).AddMemoryAsync(
            Arg.Is<AddMemoryCommand>(c => c.Scope == MemoryScope.Global),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_WorkspaceScopeOnDefaultWorkspace_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new AddMemoryCommandHandler(
            client,
            AllowViewer(),
            Workspace(string.Empty),
            NullLogger<AddMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new AddMemoryCommand { Category = "prefs", Text = "remember this", Scope = MemoryScope.Workspace },
            CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal(MemoryScopePolicy.WorkspaceScopeRequiresActiveWorkspace, result.Error);
        await client.DidNotReceive().AddMemoryAsync(Arg.Any<AddMemoryCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WorkspaceScopeOnDefaultWorkspace_DoesNotCallClient()
    {
        var client = Substitute.For<IMemoryApiClient>();
        var handler = new UpdateMemoryCommandHandler(
            client,
            AllowViewer(),
            Workspace(null),
            NullLogger<UpdateMemoryCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new UpdateMemoryCommand { MemoryId = "mem-1", Scope = MemoryScope.Workspace, Text = "updated" },
            CallContextFactory.Create());

        Assert.False(result.IsSuccess);
        Assert.Equal(MemoryScopePolicy.WorkspaceScopeRequiresActiveWorkspace, result.Error);
        await client.DidNotReceive().UpdateMemoryAsync(Arg.Any<UpdateMemoryCommand>(), Arg.Any<CancellationToken>());
    }

    private static WorkspaceContextViewModel Workspace(string? path)
    {
        var workspace = new WorkspaceContextViewModel { ActiveWorkspacePath = path };
        return workspace;
    }

    private static ConfigurableAuthorizationPolicyService AllowViewer()
        => new(defaultAllow: true);

    private static ConfigurableAuthorizationPolicyService Deny(string actionKey)
        => new ConfigurableAuthorizationPolicyService(defaultAllow: true)
            .SetAction(actionKey, allowed: false, requiredRole: McpRoles.Viewer);

    private static MemoryListItem ToListItem(MemoryDetail detail)
        => new(detail.Id, detail.Category, detail.Scope, detail.WorkspacePath, detail.Text, detail.Version, detail.UpdatedAtUtc, detail.UpdatedBy);
}
