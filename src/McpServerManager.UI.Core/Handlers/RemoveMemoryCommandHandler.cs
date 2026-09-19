using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="RemoveMemoryCommand"/> using the host-provided memory API client.
/// </summary>
internal sealed class RemoveMemoryCommandHandler : ICommandHandler<RemoveMemoryCommand, MemoryMutationOutcome>
{
    private readonly IMemoryApiClient _memoryApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<RemoveMemoryCommandHandler> _logger;

    public RemoveMemoryCommandHandler(
        IMemoryApiClient memoryApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<RemoveMemoryCommandHandler> logger)
    {
        _memoryApiClient = memoryApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<MemoryMutationOutcome>> HandleAsync(RemoveMemoryCommand command, CallContext context)
    {
        if (string.IsNullOrWhiteSpace(command.MemoryId))
            return Result<MemoryMutationOutcome>.Failure("MemoryId is required.");

        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.MemoryRemove))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.MemoryRemove);
            return Result<MemoryMutationOutcome>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _memoryApiClient.RemoveMemoryAsync(command, context.CancellationToken).ConfigureAwait(true);
            return Result<MemoryMutationOutcome>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<MemoryMutationOutcome>.Failure(ex);
        }
    }
}
