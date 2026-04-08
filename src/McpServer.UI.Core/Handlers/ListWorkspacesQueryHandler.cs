using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="ListWorkspacesQuery"/> by calling the host-provided workspace API client.
/// </summary>
internal sealed class ListWorkspacesQueryHandler : IQueryHandler<ListWorkspacesQuery, ListWorkspacesResult>
{
    private readonly IWorkspaceApiClient _workspaceApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<ListWorkspacesQueryHandler> _logger;


    public ListWorkspacesQueryHandler(IWorkspaceApiClient workspaceApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<ListWorkspacesQueryHandler> logger)
    {
        _logger = logger;
        _workspaceApiClient = workspaceApiClient;
        _authorizationPolicy = authorizationPolicy;
    }

    public async Task<Result<ListWorkspacesResult>> HandleAsync(ListWorkspacesQuery query, CallContext context)
    {
        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.WorkspaceList))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.WorkspaceList);
            return Result<ListWorkspacesResult>.Failure(BuildPermissionDenied(requiredRole));
        }

        try
        {
            var result = await _workspaceApiClient.ListWorkspacesAsync(context.CancellationToken).ConfigureAwait(true);
            return Result<ListWorkspacesResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<ListWorkspacesResult>.Failure(ex);
        }
    }

    private static string BuildPermissionDenied(string? requiredRole)
        => string.IsNullOrWhiteSpace(requiredRole)
            ? "Permission denied."
            : $"Permission denied: requires {requiredRole}.";
}
