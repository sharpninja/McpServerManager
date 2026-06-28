using McpServer.Cqrs;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.Handlers;

/// <summary>Handles <see cref="GetTriageDashboardQuery"/>.</summary>
internal sealed class GetTriageDashboardQueryHandler : IQueryHandler<GetTriageDashboardQuery, TriageDashboardSnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<GetTriageDashboardQueryHandler> _logger;

    public GetTriageDashboardQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<GetTriageDashboardQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageDashboardSnapshot>> HandleAsync(GetTriageDashboardQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.GetDashboardAsync(query.WorkspacePath, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="QueryTriageGroupsQuery"/>.</summary>
internal sealed class QueryTriageGroupsQueryHandler : IQueryHandler<QueryTriageGroupsQuery, TriageGroupQuerySnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<QueryTriageGroupsQueryHandler> _logger;

    public QueryTriageGroupsQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<QueryTriageGroupsQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageGroupQuerySnapshot>> HandleAsync(QueryTriageGroupsQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.QueryGroupsAsync(query.Status, query.WorkspacePath, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="GetTriageGroupQuery"/>.</summary>
internal sealed class GetTriageGroupQueryHandler : IQueryHandler<GetTriageGroupQuery, TriageGroupSnapshot?>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<GetTriageGroupQueryHandler> _logger;

    public GetTriageGroupQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<GetTriageGroupQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageGroupSnapshot?>> HandleAsync(GetTriageGroupQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.GetGroupAsync(query.GroupId, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="GetTriageReportQuery"/>.</summary>
internal sealed class GetTriageReportQueryHandler : IQueryHandler<GetTriageReportQuery, TriageReportSnapshot?>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<GetTriageReportQueryHandler> _logger;

    public GetTriageReportQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<GetTriageReportQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageReportSnapshot?>> HandleAsync(GetTriageReportQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.GetReportAsync(query.ReportId, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="QueryTriageRunsQuery"/>.</summary>
internal sealed class QueryTriageRunsQueryHandler : IQueryHandler<QueryTriageRunsQuery, TriageRunQuerySnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<QueryTriageRunsQueryHandler> _logger;

    public QueryTriageRunsQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<QueryTriageRunsQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageRunQuerySnapshot>> HandleAsync(QueryTriageRunsQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.QueryRunsAsync(query.Status, query.GroupId, query.WorkspacePath, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="GetTriageRunQuery"/>.</summary>
internal sealed class GetTriageRunQueryHandler : IQueryHandler<GetTriageRunQuery, TriageRunSnapshot?>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<GetTriageRunQueryHandler> _logger;

    public GetTriageRunQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<GetTriageRunQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageRunSnapshot?>> HandleAsync(GetTriageRunQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.GetRunAsync(query.RunId, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="QueryOpenTriageTodosQuery"/>.</summary>
internal sealed class QueryOpenTriageTodosQueryHandler : IQueryHandler<QueryOpenTriageTodosQuery, OpenTriageTodosResult>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<QueryOpenTriageTodosQueryHandler> _logger;

    public QueryOpenTriageTodosQueryHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<QueryOpenTriageTodosQueryHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<OpenTriageTodosResult>> HandleAsync(QueryOpenTriageTodosQuery query, CallContext context)
        => TriageHandlerHelpers.HandleReadAsync(
            () => _client.QueryOpenCreatedTodosAsync(query.WorkspacePath, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="CreateTriageGroupFromSelectionCommand"/>.</summary>
internal sealed class CreateTriageGroupFromSelectionCommandHandler : ICommandHandler<CreateTriageGroupFromSelectionCommand, TriageGroupEditResultSnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<CreateTriageGroupFromSelectionCommandHandler> _logger;

    public CreateTriageGroupFromSelectionCommandHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<CreateTriageGroupFromSelectionCommandHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageGroupEditResultSnapshot>> HandleAsync(CreateTriageGroupFromSelectionCommand command, CallContext context)
        => TriageHandlerHelpers.HandleEditAsync(
            () => _client.CreateGroupFromSelectionAsync(command.Selection, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="ConsolidateTriageSelectionIntoGroupCommand"/>.</summary>
internal sealed class ConsolidateTriageSelectionIntoGroupCommandHandler : ICommandHandler<ConsolidateTriageSelectionIntoGroupCommand, TriageGroupEditResultSnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<ConsolidateTriageSelectionIntoGroupCommandHandler> _logger;

    public ConsolidateTriageSelectionIntoGroupCommandHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<ConsolidateTriageSelectionIntoGroupCommandHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageGroupEditResultSnapshot>> HandleAsync(ConsolidateTriageSelectionIntoGroupCommand command, CallContext context)
        => TriageHandlerHelpers.HandleEditAsync(
            () => _client.ConsolidateIntoGroupAsync(command.TargetGroupId, command.Selection, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="MergeTriageGroupsCommand"/>.</summary>
internal sealed class MergeTriageGroupsCommandHandler : ICommandHandler<MergeTriageGroupsCommand, TriageGroupEditResultSnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<MergeTriageGroupsCommandHandler> _logger;

    public MergeTriageGroupsCommandHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<MergeTriageGroupsCommandHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageGroupEditResultSnapshot>> HandleAsync(MergeTriageGroupsCommand command, CallContext context)
        => TriageHandlerHelpers.HandleEditAsync(
            () => _client.MergeGroupsAsync(command.TargetGroupId, command.Selection, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

/// <summary>Handles <see cref="RetryTriageGroupCommand"/>.</summary>
internal sealed class RetryTriageGroupCommandHandler : ICommandHandler<RetryTriageGroupCommand, TriageGroupSnapshot>
{
    private readonly ITriageApiClient _client;
    private readonly IAuthorizationPolicyService _authorizationPolicy;
    private readonly ILogger<RetryTriageGroupCommandHandler> _logger;

    public RetryTriageGroupCommandHandler(
        ITriageApiClient client,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger<RetryTriageGroupCommandHandler> logger)
    {
        _client = client;
        _authorizationPolicy = authorizationPolicy;
        _logger = logger;
    }

    public Task<Result<TriageGroupSnapshot>> HandleAsync(RetryTriageGroupCommand command, CallContext context)
        => TriageHandlerHelpers.HandleEditAsync(
            () => _client.RetryGroupAsync(command.GroupId, context.CancellationToken),
            _authorizationPolicy,
            _logger);
}

internal static class TriageHandlerHelpers
{
    public static async Task<Result<T>> HandleReadAsync<T>(
        Func<Task<T>> operation,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger logger)
    {
        if (!authorizationPolicy.CanExecuteAction(McpActionKeys.TriageRead))
        {
            var requiredRole = authorizationPolicy.GetRequiredRole(McpActionKeys.TriageRead);
            return Result<T>.Failure(string.IsNullOrWhiteSpace(requiredRole)
                ? "Permission denied."
                : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await operation().ConfigureAwait(true);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<T>.Failure(ex);
        }
    }

    public static async Task<Result<T>> HandleEditAsync<T>(
        Func<Task<T>> operation,
        IAuthorizationPolicyService authorizationPolicy,
        ILogger logger)
    {
        if (!authorizationPolicy.CanExecuteAction(McpActionKeys.TriageEdit))
        {
            var requiredRole = authorizationPolicy.GetRequiredRole(McpActionKeys.TriageEdit);
            return Result<T>.Failure(string.IsNullOrWhiteSpace(requiredRole)
                ? "Permission denied."
                : $"Permission denied: requires {requiredRole}.");
        }

        try
        {
            var result = await operation().ConfigureAwait(true);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError("{ExceptionDetail}", ex.ToString());
            return Result<T>.Failure(ex);
        }
    }
}
