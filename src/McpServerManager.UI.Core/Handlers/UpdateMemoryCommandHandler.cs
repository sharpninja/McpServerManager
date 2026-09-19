using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="UpdateMemoryCommand"/> using the host-provided memory API client.
/// Does not send expectedVersion (D11).
/// </summary>
internal sealed class UpdateMemoryCommandHandler : ICommandHandler<UpdateMemoryCommand, MemoryMutationOutcome>
{
    private readonly IMemoryApiClient _memoryApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<UpdateMemoryCommandHandler> _logger;

    public UpdateMemoryCommandHandler(
        IMemoryApiClient memoryApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<UpdateMemoryCommandHandler> logger)
    {
        _memoryApiClient = memoryApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<MemoryMutationOutcome>> HandleAsync(UpdateMemoryCommand command, CallContext context)
    {
        if (string.IsNullOrWhiteSpace(command.MemoryId))
            return Result<MemoryMutationOutcome>.Failure("MemoryId is required.");

        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.MemoryUpdate))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.MemoryUpdate);
            return Result<MemoryMutationOutcome>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _memoryApiClient.UpdateMemoryAsync(command, context.CancellationToken).ConfigureAwait(true);
            return Result<MemoryMutationOutcome>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<MemoryMutationOutcome>.Failure(ex);
        }
    }
}
