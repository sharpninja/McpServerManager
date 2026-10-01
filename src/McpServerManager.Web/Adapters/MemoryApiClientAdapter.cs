using McpServer.Client;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServerManager.Web.Adapters;

internal sealed class MemoryApiClientAdapter : IMemoryApiClient
{
    private readonly WebMcpContext _context;
    private readonly ILogger<MemoryApiClientAdapter> _logger;

    public MemoryApiClientAdapter(WebMcpContext context, ILogger<MemoryApiClientAdapter>? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger<MemoryApiClientAdapter>.Instance;
    }

    public async Task<ListMemoriesResult> ListMemoriesAsync(ListMemoriesQuery query, CancellationToken cancellationToken = default)
    {
        var response = await UseMemoryClientAsync(
                (client, ct) => client.Memory.ListAsync(
                    MemoryMessageMapper.ToClientScope(query.Scope),
                    query.Category,
                    query.Keyword,
                    ct),
                cancellationToken)
            .ConfigureAwait(true);
        return MemoryMessageMapper.ToListResult(response);
    }

    public async Task<MemoryDetail?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await UseMemoryClientAsync(
                    (client, ct) => client.Memory.GetAsync(memoryId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
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
            var result = await UseMemoryClientAsync(
                    (client, ct) => client.Memory.AddAsync(MemoryMessageMapper.ToAddRequest(command), ct),
                    cancellationToken)
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
            var result = await UseMemoryClientAsync(
                    (client, ct) => client.Memory.UpdateAsync(
                        command.MemoryId,
                        MemoryMessageMapper.ToUpdateRequest(command),
                        ct),
                    cancellationToken)
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
            var result = await UseMemoryClientAsync(
                    (client, ct) => client.Memory.RemoveAsync(command.MemoryId, ct),
                    cancellationToken)
                .ConfigureAwait(true);
            return MemoryMessageMapper.ToOutcome(result);
        }
        catch (McpNotFoundException ex)
        {
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
            return new MemoryMutationOutcome(false, ex.Message, null, MemoryMutationFailureKind.NotFound);
        }
    }

    /// <summary>
    /// Default workspace (empty active path) uses the control client so Global creates
    /// are not sent with a stale workspace path. A selected workspace keeps the active client.
    /// </summary>
    private Task<T> UseMemoryClientAsync<T>(
        Func<McpServerClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_context.ActiveWorkspacePath))
            return _context.UseControlApiClientAsync(operation, cancellationToken);

        return _context.UseActiveWorkspaceApiClientAsync(operation, cancellationToken);
    }
}
