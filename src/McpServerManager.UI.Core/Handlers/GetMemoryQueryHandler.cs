using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="GetMemoryQuery"/> using the host-provided memory API client.
/// </summary>
internal sealed class GetMemoryQueryHandler : IQueryHandler<GetMemoryQuery, MemoryDetail?>
{
    private readonly IMemoryApiClient _memoryApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<GetMemoryQueryHandler> _logger;

    public GetMemoryQueryHandler(
        IMemoryApiClient memoryApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<GetMemoryQueryHandler> logger)
    {
        _memoryApiClient = memoryApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<MemoryDetail?>> HandleAsync(GetMemoryQuery query, CallContext context)
    {
        if (string.IsNullOrWhiteSpace(query.MemoryId))
            return Result<MemoryDetail?>.Failure("MemoryId is required.");

        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.MemoryGet))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.MemoryGet);
            return Result<MemoryDetail?>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _memoryApiClient.GetMemoryAsync(query.MemoryId, context.CancellationToken).ConfigureAwait(true);
            return Result<MemoryDetail?>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<MemoryDetail?>.Failure(ex);
        }
    }
}
