using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="InitWorkspaceCommand"/> by delegating the initialization workflow to the host-provided workspace API client.
/// </summary>
internal sealed class InitWorkspaceCommandHandler : ICommandHandler<InitWorkspaceCommand, WorkspaceInitInfo>
{
    private readonly IWorkspaceApiClient _workspaceApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<InitWorkspaceCommandHandler> _logger;


    public InitWorkspaceCommandHandler(IWorkspaceApiClient workspaceApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<InitWorkspaceCommandHandler> logger)
    {
        _logger = logger;
        _workspaceApiClient = workspaceApiClient;
        _authorizationPolicy = authorizationPolicy;
    }

    public async Task<Result<WorkspaceInitInfo>> HandleAsync(InitWorkspaceCommand command, CallContext context)
    {
        if (string.IsNullOrWhiteSpace(command.WorkspacePath))
            return Result<WorkspaceInitInfo>.Failure("WorkspacePath is required.");

        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.WorkspaceInit))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.WorkspaceInit);
            return Result<WorkspaceInitInfo>.Failure(BuildPermissionDenied(requiredRole));
        }

        try
        {
            var result = await _workspaceApiClient.InitWorkspaceAsync(command.WorkspacePath, context.CancellationToken)
                .ConfigureAwait(true);
            return Result<WorkspaceInitInfo>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<WorkspaceInitInfo>.Failure(ex);
        }
    }

    private static string BuildPermissionDenied(string? requiredRole)
        => string.IsNullOrWhiteSpace(requiredRole)
            ? "Permission denied."
            : $"Permission denied: requires {requiredRole}.";
}
