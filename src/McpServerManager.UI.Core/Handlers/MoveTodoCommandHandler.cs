using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="MoveTodoCommand"/> using the host-provided TODO API client.
/// </summary>
internal sealed class MoveTodoCommandHandler : ICommandHandler<MoveTodoCommand, TodoMutationOutcome>
{
    private readonly ITodoApiClient _todoApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<MoveTodoCommandHandler> _logger;

    public MoveTodoCommandHandler(ITodoApiClient todoApiClient, IAuthorizationPolicyService authorizationPolicy,
        ILogger<MoveTodoCommandHandler> logger)
    {
        _todoApiClient = todoApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<TodoMutationOutcome>> HandleAsync(MoveTodoCommand command, CallContext context)
    {
        if (string.IsNullOrWhiteSpace(command.TodoId))
            return Result<TodoMutationOutcome>.Failure("TodoId is required.");
        if (string.IsNullOrWhiteSpace(command.TargetWorkspacePath))
            return Result<TodoMutationOutcome>.Failure("TargetWorkspacePath is required.");

        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.TodoMove))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.TodoMove);
            return Result<TodoMutationOutcome>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _todoApiClient.MoveTodoAsync(command, context.CancellationToken).ConfigureAwait(true);
            return Result<TodoMutationOutcome>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<TodoMutationOutcome>.Failure(ex);
        }
    }
}
