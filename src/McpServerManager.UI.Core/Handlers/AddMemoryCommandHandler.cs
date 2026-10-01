using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="AddMemoryCommand"/> using the host-provided memory API client.
/// </summary>
internal sealed class AddMemoryCommandHandler : ICommandHandler<AddMemoryCommand, MemoryMutationOutcome>
{
    private readonly IMemoryApiClient _memoryApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly ILogger<AddMemoryCommandHandler> _logger;

    public AddMemoryCommandHandler(
        IMemoryApiClient memoryApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        WorkspaceContextViewModel workspaceContext,
        ILogger<AddMemoryCommandHandler> logger)
    {
        _memoryApiClient = memoryApiClient;
        _authorizationPolicy = authorizationPolicy;
        _workspaceContext = workspaceContext;
        _logger = logger;
    }

    public async Task<Result<MemoryMutationOutcome>> HandleAsync(AddMemoryCommand command, CallContext context)
    {
        if (string.IsNullOrWhiteSpace(command.Category))
            return Result<MemoryMutationOutcome>.Failure("Category is required.");
        if (string.IsNullOrWhiteSpace(command.Text))
            return Result<MemoryMutationOutcome>.Failure("Text is required.");

        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.MemoryAdd))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.MemoryAdd);
            return Result<MemoryMutationOutcome>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        if (!MemoryScopePolicy.CanPersist(_workspaceContext.ActiveWorkspacePath, command.Scope))
            return Result<MemoryMutationOutcome>.Failure(MemoryScopePolicy.WorkspaceScopeRequiresActiveWorkspace);

        try
        {
            var result = await _memoryApiClient.AddMemoryAsync(command, context.CancellationToken).ConfigureAwait(true);
            return Result<MemoryMutationOutcome>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<MemoryMutationOutcome>.Failure(ex);
        }
    }
}
