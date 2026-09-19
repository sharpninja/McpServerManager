using McpServer.Client;
using McpServerManager.UI.Core.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Desktop/Android bootstrap adapter for <see cref="IMemoryApiClient"/> backed by <see cref="McpServerClient.Memory"/>.
/// </summary>
internal sealed class UiCoreMemoryApiClientAdapter : IMemoryApiClient
{
    private readonly McpServerClient _client;
    private readonly ILogger<UiCoreMemoryApiClientAdapter> _logger;

    public UiCoreMemoryApiClientAdapter(
        McpServerClient client,
        ILogger<UiCoreMemoryApiClientAdapter>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLogger<UiCoreMemoryApiClientAdapter>.Instance;
    }

    public async Task<ListMemoriesResult> ListMemoriesAsync(ListMemoriesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var result = await _client.Memory.ListAsync(
                MemoryMessageMapper.ToClientScope(query.Scope),
                query.Category,
                query.Keyword,
                cancellationToken)
            .ConfigureAwait(true);
        return MemoryMessageMapper.ToListResult(result);
    }

    public async Task<MemoryDetail?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _client.Memory.GetAsync(memoryId, cancellationToken).ConfigureAwait(true);
            return item is null ? null : MemoryMessageMapper.ToDetail(item);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return null;
        }
    }

    public async Task<MemoryMutationOutcome> AddMemoryAsync(AddMemoryCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.Memory
                .AddAsync(MemoryMessageMapper.ToAddRequest(command), cancellationToken)
                .ConfigureAwait(true);
            return MemoryMessageMapper.ToOutcome(result);
        }
        catch (McpConflictException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.Conflict);
        }
        catch (McpValidationException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.Validation);
        }
    }

    public async Task<MemoryMutationOutcome> UpdateMemoryAsync(UpdateMemoryCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.Memory
                .UpdateAsync(command.MemoryId, MemoryMessageMapper.ToUpdateRequest(command), cancellationToken)
                .ConfigureAwait(true);
            return MemoryMessageMapper.ToOutcome(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.NotFound);
        }
        catch (McpValidationException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.Validation);
        }
    }

    public async Task<MemoryMutationOutcome> RemoveMemoryAsync(RemoveMemoryCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.Memory.RemoveAsync(command.MemoryId, cancellationToken).ConfigureAwait(true);
            return MemoryMessageMapper.ToOutcome(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.NotFound);
        }
    }
}
