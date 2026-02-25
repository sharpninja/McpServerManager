using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.Commands;

// ── Queries ─────────────────────────────────────────────────────────────────

/// <summary>Query all todos with optional filters.</summary>
public sealed class QueryTodosQuery : IQuery<McpTodoQueryResult>
{
    public string? Keyword { get; init; }
    public string? Priority { get; init; }
    public string? Section { get; init; }
    public string? Id { get; init; }
    public bool? Done { get; init; }
}

public sealed class QueryTodosHandler : IQueryHandler<QueryTodosQuery, McpTodoQueryResult>
{
    private readonly McpTodoService _service;
    public QueryTodosHandler(McpTodoService service) => _service = service;

    public Task<McpTodoQueryResult> ExecuteAsync(QueryTodosQuery query, CancellationToken cancellationToken = default)
        => _service.QueryAsync(query.Keyword, query.Priority, query.Section, query.Id, query.Done, cancellationToken);
}

/// <summary>Get a single todo by its id.</summary>
public sealed class GetTodoByIdQuery : IQuery<McpTodoFlatItem?>
{
    public string Id { get; }
    public GetTodoByIdQuery(string id) => Id = id;
}

public sealed class GetTodoByIdHandler : IQueryHandler<GetTodoByIdQuery, McpTodoFlatItem?>
{
    private readonly McpTodoService _service;
    public GetTodoByIdHandler(McpTodoService service) => _service = service;

    public Task<McpTodoFlatItem?> ExecuteAsync(GetTodoByIdQuery query, CancellationToken cancellationToken = default)
        => _service.GetByIdAsync(query.Id, cancellationToken);
}

// ── Commands ────────────────────────────────────────────────────────────────

/// <summary>Create a new todo item.</summary>
public sealed class CreateTodoCommand : ICommand<McpTodoMutationResult>
{
    public McpTodoCreateRequest Request { get; }
    public CreateTodoCommand(McpTodoCreateRequest request) => Request = request;
}

public sealed class CreateTodoHandler : ICommandHandler<CreateTodoCommand, McpTodoMutationResult>
{
    private readonly McpTodoService _service;
    public CreateTodoHandler(McpTodoService service) => _service = service;

    public Task<McpTodoMutationResult> ExecuteAsync(CreateTodoCommand command, CancellationToken cancellationToken = default)
        => _service.CreateAsync(command.Request, cancellationToken);
}

/// <summary>Update an existing todo item (partial update).</summary>
public sealed class UpdateTodoCommand : ICommand<McpTodoMutationResult>
{
    public string Id { get; }
    public McpTodoUpdateRequest Request { get; }
    public UpdateTodoCommand(string id, McpTodoUpdateRequest request)
    {
        Id = id;
        Request = request;
    }
}

public sealed class UpdateTodoHandler : ICommandHandler<UpdateTodoCommand, McpTodoMutationResult>
{
    private readonly McpTodoService _service;
    public UpdateTodoHandler(McpTodoService service) => _service = service;

    public Task<McpTodoMutationResult> ExecuteAsync(UpdateTodoCommand command, CancellationToken cancellationToken = default)
        => _service.UpdateAsync(command.Id, command.Request, cancellationToken);
}

/// <summary>Delete a todo item by id.</summary>
public sealed class DeleteTodoCommand : ICommand<McpTodoMutationResult>
{
    public string Id { get; }
    public DeleteTodoCommand(string id) => Id = id;
}

public sealed class DeleteTodoHandler : ICommandHandler<DeleteTodoCommand, McpTodoMutationResult>
{
    private readonly McpTodoService _service;
    public DeleteTodoHandler(McpTodoService service) => _service = service;

    public Task<McpTodoMutationResult> ExecuteAsync(DeleteTodoCommand command, CancellationToken cancellationToken = default)
        => _service.DeleteAsync(command.Id, cancellationToken);
}

/// <summary>Run AI requirements analysis on a todo.</summary>
public sealed class AnalyzeTodoRequirementsCommand : ICommand<McpRequirementsAnalysisResult>
{
    public string Id { get; }
    public AnalyzeTodoRequirementsCommand(string id) => Id = id;
}

public sealed class AnalyzeTodoRequirementsHandler : ICommandHandler<AnalyzeTodoRequirementsCommand, McpRequirementsAnalysisResult>
{
    private readonly McpTodoService _service;
    public AnalyzeTodoRequirementsHandler(McpTodoService service) => _service = service;

    public Task<McpRequirementsAnalysisResult> ExecuteAsync(AnalyzeTodoRequirementsCommand command, CancellationToken cancellationToken = default)
        => _service.AnalyzeRequirementsAsync(command.Id, cancellationToken);
}

public enum TodoPromptActionKind
{
    Status,
    Implement,
    Plan
}

/// <summary>Stream a TODO prompt response (status/implement/plan) from the MCP server.</summary>
public sealed class StreamTodoPromptCommand : ICommand<IAsyncEnumerable<string>>
{
    public string Id { get; }
    public TodoPromptActionKind Action { get; }

    public StreamTodoPromptCommand(string id, TodoPromptActionKind action)
    {
        Id = id;
        Action = action;
    }
}

public sealed class StreamTodoPromptHandler : ICommandHandler<StreamTodoPromptCommand, IAsyncEnumerable<string>>
{
    private readonly McpTodoService _service;
    public StreamTodoPromptHandler(McpTodoService service) => _service = service;

    public Task<IAsyncEnumerable<string>> ExecuteAsync(StreamTodoPromptCommand command, CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<string> stream = command.Action switch
        {
            TodoPromptActionKind.Status => _service.StreamStatusPromptAsync(command.Id, cancellationToken),
            TodoPromptActionKind.Implement => _service.StreamImplementPromptAsync(command.Id, cancellationToken),
            TodoPromptActionKind.Plan => _service.StreamPlanPromptAsync(command.Id, cancellationToken),
            _ => throw new System.ArgumentOutOfRangeException(nameof(command.Action), command.Action, "Unsupported TODO prompt action.")
        };

        return Task.FromResult(stream);
    }
}
