using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>
/// Handles <see cref="PushRequirementsWikiCommand"/>: generates the requirements wiki then publishes
/// it to the requested target (PLAN-REQSDESKTOP-001, FR-REQS-PUSH-001).
/// </summary>
internal sealed class PushRequirementsWikiCommandHandler : ICommandHandler<PushRequirementsWikiCommand, WikiPushResult>
{
    private readonly IRequirementsApiClient _client;
    private readonly IRequirementsWikiPublisher _publisher;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<PushRequirementsWikiCommandHandler> _logger;

    public PushRequirementsWikiCommandHandler(
        IRequirementsApiClient client,
        IRequirementsWikiPublisher publisher,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<PushRequirementsWikiCommandHandler> logger)
    {
        _client = client;
        _publisher = publisher;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WikiPushResult>> HandleAsync(PushRequirementsWikiCommand command, CallContext context)
    {
        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.RequirementsGenerate))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.RequirementsGenerate);
            return Result<WikiPushResult>.Failure(RequirementsHandlerHelpers.BuildPermissionDenied(requiredRole));
        }

        try
        {
            var generated = await _client.GenerateAsync(
                new GenerateRequirementsDocumentQuery(command.Doc), context.CancellationToken).ConfigureAwait(true);
            var result = await _publisher.PublishAsync(
                generated.Content, generated.ContentType, command.Target, context.CancellationToken).ConfigureAwait(true);
            return Result<WikiPushResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push requirements wiki to {Target}", command.Target);
            return Result<WikiPushResult>.Failure(ex.Message);
        }
    }
}

/// <summary>
/// Handles <see cref="ImportRequirementsCommand"/>: creates each parsed FR/TR/TEST record, collecting
/// per-item errors (PLAN-REQSDESKTOP-001, FR-REQS-IMPORT-001).
/// </summary>
internal sealed class ImportRequirementsCommandHandler : ICommandHandler<ImportRequirementsCommand, RequirementsImportResult>
{
    private readonly IRequirementsApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<ImportRequirementsCommandHandler> _logger;

    public ImportRequirementsCommandHandler(
        IRequirementsApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<ImportRequirementsCommandHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<RequirementsImportResult>> HandleAsync(ImportRequirementsCommand command, CallContext context)
    {
        if (!_authorizationPolicy.CanExecuteAction(McpActionKeys.RequirementsWrite))
        {
            var requiredRole = _authorizationPolicy.GetRequiredRole(McpActionKeys.RequirementsWrite);
            return Result<RequirementsImportResult>.Failure(RequirementsHandlerHelpers.BuildPermissionDenied(requiredRole));
        }

        var request = command.Request;
        var errors = new List<string>();
        var fr = 0;
        var tr = 0;
        var test = 0;

        foreach (var create in request.Functional)
        {
            try { await _client.CreateFunctionalRequirementAsync(create, context.CancellationToken).ConfigureAwait(true); fr++; }
            catch (Exception ex) { errors.Add($"FR {create.Id}: {ex.Message}"); _logger.LogWarning(ex, "Import FR {Id} failed", create.Id); }
        }

        foreach (var create in request.Technical)
        {
            try { await _client.CreateTechnicalRequirementAsync(create, context.CancellationToken).ConfigureAwait(true); tr++; }
            catch (Exception ex) { errors.Add($"TR {create.Id}: {ex.Message}"); _logger.LogWarning(ex, "Import TR {Id} failed", create.Id); }
        }

        foreach (var create in request.Testing)
        {
            try { await _client.CreateTestingRequirementAsync(create, context.CancellationToken).ConfigureAwait(true); test++; }
            catch (Exception ex) { errors.Add($"TEST {create.Id}: {ex.Message}"); _logger.LogWarning(ex, "Import TEST {Id} failed", create.Id); }
        }

        return Result<RequirementsImportResult>.Success(new RequirementsImportResult(fr, tr, test, errors));
    }
}
