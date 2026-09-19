using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="ListMemoriesQuery"/> using the host-provided memory API client.
/// </summary>
internal sealed class ListMemoriesQueryHandler : IQueryHandler<ListMemoriesQuery, ListMemoriesResult>
{
    private readonly IMemoryApiClient _memoryApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<ListMemoriesQueryHandler> _logger;

    public ListMemoriesQueryHandler(
        IMemoryApiClient memoryApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<ListMemoriesQueryHandler> logger)
    {
        _memoryApiClient = memoryApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<ListMemoriesResult>> HandleAsync(ListMemoriesQuery query, CallContext context)
    {
        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.MemoryList))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.MemoryList);
            return Result<ListMemoriesResult>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _memoryApiClient.ListMemoriesAsync(query, context.CancellationToken).ConfigureAwait(true);
            return Result<ListMemoriesResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<ListMemoriesResult>.Failure(ex);
        }
    }
}
