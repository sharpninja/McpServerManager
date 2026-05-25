using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>Handles <see cref="PatchConfigurationValuesCommand"/>.</summary>
internal sealed class PatchConfigurationValuesCommandHandler
    : ICommandHandler<PatchConfigurationValuesCommand, IReadOnlyDictionary<string, string>>
{
    private readonly IConfigurationApiClient _configurationApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<PatchConfigurationValuesCommandHandler> _logger;

    public PatchConfigurationValuesCommandHandler(
        IConfigurationApiClient configurationApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<PatchConfigurationValuesCommandHandler> logger)
    {
        _configurationApiClient = configurationApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> HandleAsync(
        PatchConfigurationValuesCommand command,
        CallContext context)
    {
        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.ConfigurationPatch))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.ConfigurationPatch);
            return Result<IReadOnlyDictionary<string, string>>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _configurationApiClient
                .PatchValuesAsync(command.Values, context.CancellationToken)
                .ConfigureAwait(true);
            return Result<IReadOnlyDictionary<string, string>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<IReadOnlyDictionary<string, string>>.Failure(ex);
        }
    }
}
