using McpServer.Cqrs;

namespace McpServerManager.UI.Core.Messages;

/// <summary>Memory visibility scope used by list/detail/editor surfaces.</summary>
public enum MemoryScope
{
    /// <summary>Memory visible to every workspace.</summary>
    Global = 0,

    /// <summary>Memory visible only to the active workspace.</summary>
    Workspace = 1,
}

/// <summary>Query to list memories with optional filters.</summary>
public sealed record ListMemoriesQuery : IQuery<ListMemoriesResult>
{
    /// <summary>Optional scope filter. Null means the server Effective set (Global + Workspace).</summary>
    public MemoryScope? Scope { get; init; }

    /// <summary>Optional category filter.</summary>
    public string? Category { get; init; }

    /// <summary>Optional keyword filter.</summary>
    public string? Keyword { get; init; }
}

/// <summary>Result of a memory list query.</summary>
public sealed record ListMemoriesResult(IReadOnlyList<MemoryListItem> Items, int TotalCount);

/// <summary>List-friendly memory summary. Version is display-only (D11).</summary>
public sealed record MemoryListItem(
    string Id,
    string Category,
    MemoryScope Scope,
    string? WorkspacePath,
    string Text,
    int Version,
    DateTimeOffset UpdatedAtUtc,
    string? UpdatedBy);

/// <summary>Query to load a single memory by ID.</summary>
public sealed record GetMemoryQuery(string MemoryId) : IQuery<MemoryDetail?>;

/// <summary>Detailed memory view. Version is display-only (D11).</summary>
public sealed record MemoryDetail(
    string Id,
    string Category,
    MemoryScope Scope,
    string? WorkspacePath,
    string Text,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? UpdatedBy);

/// <summary>Typed result of a memory add/update/remove mutation.</summary>
public sealed record MemoryMutationOutcome(
    bool Success,
    string? Error,
    MemoryDetail? Item,
    MemoryMutationFailureKind FailureKind = MemoryMutationFailureKind.None);

/// <summary>Classifies the failure mode of a memory mutation.</summary>
public enum MemoryMutationFailureKind
{
    None = 0,
    Validation = 1,
    Conflict = 2,
    NotFound = 3,
}

/// <summary>Command to add a memory.</summary>
public sealed record AddMemoryCommand : ICommand<MemoryMutationOutcome>
{
    /// <summary>Optional explicit memory id.</summary>
    public string? Id { get; init; }

    /// <summary>Memory category (required).</summary>
    public required string Category { get; init; }

    /// <summary>Visibility scope. Defaults to workspace.</summary>
    public MemoryScope Scope { get; init; } = MemoryScope.Workspace;

    /// <summary>Raw memory text (required).</summary>
    public required string Text { get; init; }

    /// <summary>Optional actor recorded as updater.</summary>
    public string? UpdatedBy { get; init; }
}

/// <summary>Command to update a memory. Version is not sent (D11: no OCC).</summary>
public sealed record UpdateMemoryCommand : ICommand<MemoryMutationOutcome>
{
    /// <summary>Memory id to update (required).</summary>
    public required string MemoryId { get; init; }

    /// <summary>Optional category replacement.</summary>
    public string? Category { get; init; }

    /// <summary>Optional scope replacement.</summary>
    public MemoryScope? Scope { get; init; }

    /// <summary>Optional raw text replacement.</summary>
    public string? Text { get; init; }

    /// <summary>Optional actor recorded as updater.</summary>
    public string? UpdatedBy { get; init; }
}

/// <summary>Command to remove a memory.</summary>
public sealed record RemoveMemoryCommand : ICommand<MemoryMutationOutcome>
{
    /// <summary>Memory id to remove (required).</summary>
    public required string MemoryId { get; init; }
}
