using McpServer.Cqrs;
using McpServer.UI.Core.Authorization;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.UI.Core.Handlers;

/// <summary>Handles <see cref="GetConfigurationValuesQuery"/>.</summary>
internal sealed class GetConfigurationValuesQueryHandler
    : IQueryHandler<GetConfigurationValuesQuery, IReadOnlyDictionary<string, string>>
{
    private readonly IConfigurationApiClient _configurationApiClient;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<GetConfigurationValuesQueryHandler> _logger;

    public GetConfigurationValuesQueryHandler(
        IConfigurationApiClient configurationApiClient,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<GetConfigurationValuesQueryHandler> logger)
    {
        _configurationApiClient = configurationApiClient;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> HandleAsync(
        GetConfigurationValuesQuery query,
        CallContext context)
    {
        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.ConfigurationGet))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.ConfigurationGet);
            return Result<IReadOnlyDictionary<string, string>>.Failure(
                string.IsNullOrWhiteSpace(requiredRole)
                    ? "Permission denied."
                    : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await _configurationApiClient
                .GetValuesAsync(context.CancellationToken)
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
