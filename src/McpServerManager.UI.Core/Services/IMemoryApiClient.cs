using McpServerManager.UI.Core.Messages;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Host-provided API client abstraction for memory list/get/add/update/remove.
/// </summary>
public interface IMemoryApiClient
{
    /// <summary>Lists memories using the supplied filters.</summary>
    Task<ListMemoriesResult> ListMemoriesAsync(ListMemoriesQuery query, CancellationToken cancellationToken = default);

    /// <summary>Loads a single memory by ID, or null when not found/not visible.</summary>
    Task<MemoryDetail?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default);

    /// <summary>Adds a memory.</summary>
    Task<MemoryMutationOutcome> AddMemoryAsync(AddMemoryCommand command, CancellationToken cancellationToken = default);

    /// <summary>Updates a memory. Does not send expectedVersion (D11).</summary>
    Task<MemoryMutationOutcome> UpdateMemoryAsync(UpdateMemoryCommand command, CancellationToken cancellationToken = default);

    /// <summary>Removes a memory.</summary>
    Task<MemoryMutationOutcome> RemoveMemoryAsync(RemoveMemoryCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Empty fallback for hosts that do not expose memory UI.</summary>
internal sealed class NoOpMemoryApiClient : IMemoryApiClient
{
    public Task<ListMemoriesResult> ListMemoriesAsync(ListMemoriesQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new ListMemoriesResult([], 0));

    public Task<MemoryDetail?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
        => Task.FromResult<MemoryDetail?>(null);

    public Task<MemoryMutationOutcome> AddMemoryAsync(AddMemoryCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(new MemoryMutationOutcome(false, "Memory add is not supported by this host.", null, MemoryMutationFailureKind.Validation));

    public Task<MemoryMutationOutcome> UpdateMemoryAsync(UpdateMemoryCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(new MemoryMutationOutcome(false, "Memory update is not supported by this host.", null, MemoryMutationFailureKind.Validation));

    public Task<MemoryMutationOutcome> RemoveMemoryAsync(RemoveMemoryCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(new MemoryMutationOutcome(false, "Memory remove is not supported by this host.", null, MemoryMutationFailureKind.Validation));
}
