using McpServer.Client.Models;
using McpServerManager.UI.Core.Messages;
using ClientScope = McpServer.Client.Models.MemoryScope;
using UiScope = McpServerManager.UI.Core.Messages.MemoryScope;
using UiFailureKind = McpServerManager.UI.Core.Messages.MemoryMutationFailureKind;

namespace McpServerManager.UI.Core.Services;

/// <summary>Maps SharpNinja.McpServer.Client memory types to UI.Core messages.</summary>
public static class MemoryMessageMapper
{
    /// <summary>Maps a client list result.</summary>
    public static ListMemoriesResult ToListResult(MemoryQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var items = result.Items.Select(ToListItem).ToList();
        return new ListMemoriesResult(items, result.TotalCount);
    }

    /// <summary>Maps a client memory item to a list row.</summary>
    public static MemoryListItem ToListItem(MemoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new MemoryListItem(
            Id: item.Id,
            Category: item.Category,
            Scope: ToUiScope(item.Scope),
            WorkspacePath: item.WorkspacePath,
            Text: item.Text,
            Version: item.Version,
            UpdatedAtUtc: item.UpdatedAtUtc,
            UpdatedBy: item.UpdatedBy);
    }

    /// <summary>Maps a client memory item to detail. Version is display-only.</summary>
    public static MemoryDetail ToDetail(MemoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new MemoryDetail(
            Id: item.Id,
            Category: item.Category,
            Scope: ToUiScope(item.Scope),
            WorkspacePath: item.WorkspacePath,
            Text: item.Text,
            Version: item.Version,
            CreatedAtUtc: item.CreatedAtUtc,
            UpdatedAtUtc: item.UpdatedAtUtc,
            UpdatedBy: item.UpdatedBy);
    }

    /// <summary>Maps a client mutation result. Does not read or write expectedVersion.</summary>
    public static MemoryMutationOutcome ToOutcome(MemoryMutationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MemoryMutationOutcome(
            Success: result.Success,
            Error: result.Error,
            Item: result.Memory is null ? null : ToDetail(result.Memory),
            FailureKind: MapFailureKind(result.FailureKind));
    }

    /// <summary>Maps an add command to the client request. No OCC fields.</summary>
    public static MemoryAddRequest ToAddRequest(AddMemoryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new MemoryAddRequest
        {
            Id = command.Id,
            Category = command.Category,
            Scope = ToClientScope(command.Scope),
            Text = command.Text,
            UpdatedBy = command.UpdatedBy,
        };
    }

    /// <summary>Maps an update command to the client request. No expectedVersion.</summary>
    public static MemoryUpdateRequest ToUpdateRequest(UpdateMemoryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new MemoryUpdateRequest
        {
            Category = command.Category,
            Scope = command.Scope is null ? null : ToClientScope(command.Scope.Value),
            Text = command.Text,
            UpdatedBy = command.UpdatedBy,
        };
    }

    /// <summary>Maps UI scope to the client enum.</summary>
    public static ClientScope ToClientScope(UiScope scope)
        => scope switch
        {
            UiScope.Global => ClientScope.Global,
            UiScope.Workspace => ClientScope.Workspace,
            _ => ClientScope.Workspace,
        };

    /// <summary>Maps optional UI scope. Null remains Effective (omit filter).</summary>
    public static ClientScope? ToClientScope(UiScope? scope)
        => scope is null ? null : ToClientScope(scope.Value);

    /// <summary>Maps client scope to the UI enum.</summary>
    public static UiScope ToUiScope(ClientScope scope)
        => scope switch
        {
            ClientScope.Global => UiScope.Global,
            ClientScope.Workspace => UiScope.Workspace,
            _ => UiScope.Workspace,
        };

    private static UiFailureKind MapFailureKind(McpServer.Client.Models.MemoryMutationFailureKind failureKind)
        => failureKind switch
        {
            McpServer.Client.Models.MemoryMutationFailureKind.Validation => UiFailureKind.Validation,
            McpServer.Client.Models.MemoryMutationFailureKind.Conflict => UiFailureKind.Conflict,
            McpServer.Client.Models.MemoryMutationFailureKind.NotFound => UiFailureKind.NotFound,
            _ => UiFailureKind.None,
        };
}
