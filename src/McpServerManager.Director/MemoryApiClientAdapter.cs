using McpServer.Client;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.Director;

/// <summary>
/// Director adapter for <see cref="IMemoryApiClient"/> backed by <see cref="McpServerClient.Memory"/>.
/// </summary>
internal sealed class MemoryApiClientAdapter : IMemoryApiClient
{
    private readonly DirectorMcpContext _context;
    private readonly ILogger<MemoryApiClientAdapter> _logger;

    public MemoryApiClientAdapter(DirectorMcpContext context, ILogger<MemoryApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<MemoryApiClientAdapter>.Instance;
    }

    public async Task<ListMemoriesResult> ListMemoriesAsync(ListMemoriesQuery query, CancellationToken cancellationToken = default)
    {
        var client = await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
        var response = await client.Memory.ListAsync(
                MemoryMessageMapper.ToClientScope(query.Scope),
                query.Category,
                query.Keyword,
                cancellationToken)
            .ConfigureAwait(true);
        return MemoryMessageMapper.ToListResult(response);
    }

    public async Task<MemoryDetail?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
            var item = await client.Memory.GetAsync(memoryId, cancellationToken).ConfigureAwait(true);
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
            var client = await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
            var result = await client.Memory
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
            var client = await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
            var result = await client.Memory
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
            var client = await _context.GetRequiredActiveWorkspaceApiClientAsync(cancellationToken).ConfigureAwait(true);
            var result = await client.Memory.RemoveAsync(command.MemoryId, cancellationToken).ConfigureAwait(true);
            return MemoryMessageMapper.ToOutcome(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.NotFound);
        }
    }
}
