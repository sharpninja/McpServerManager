using System.Threading;
using System.Threading.Tasks;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.Commands;

public sealed class QueryWorkspacesQuery : IQuery<McpWorkspaceQueryResult>;

public sealed class QueryWorkspacesHandler : IQueryHandler<QueryWorkspacesQuery, McpWorkspaceQueryResult>
{
    private readonly McpWorkspaceService _service;
    public QueryWorkspacesHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceQueryResult> ExecuteAsync(QueryWorkspacesQuery query, CancellationToken cancellationToken = default)
        => _service.QueryAsync(cancellationToken);
}

public sealed class GetWorkspaceByIdQuery : IQuery<McpWorkspaceItem?>
{
    public string Key { get; }
    public GetWorkspaceByIdQuery(string key) => Key = key;
}

public sealed class GetWorkspaceByIdHandler : IQueryHandler<GetWorkspaceByIdQuery, McpWorkspaceItem?>
{
    private readonly McpWorkspaceService _service;
    public GetWorkspaceByIdHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceItem?> ExecuteAsync(GetWorkspaceByIdQuery query, CancellationToken cancellationToken = default)
        => _service.GetByIdAsync(query.Key, cancellationToken);
}

public sealed class GetWorkspaceStatusQuery : IQuery<McpWorkspaceProcessStatus>
{
    public string Key { get; }
    public GetWorkspaceStatusQuery(string key) => Key = key;
}

public sealed class GetWorkspaceStatusHandler : IQueryHandler<GetWorkspaceStatusQuery, McpWorkspaceProcessStatus>
{
    private readonly McpWorkspaceService _service;
    public GetWorkspaceStatusHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceProcessStatus> ExecuteAsync(GetWorkspaceStatusQuery query, CancellationToken cancellationToken = default)
        => _service.GetStatusAsync(query.Key, cancellationToken);
}

public sealed class GetWorkspaceHealthQuery : IQuery<McpWorkspaceHealthResult>
{
    public string Key { get; }
    public GetWorkspaceHealthQuery(string key) => Key = key;
}

public sealed class GetWorkspaceHealthHandler : IQueryHandler<GetWorkspaceHealthQuery, McpWorkspaceHealthResult>
{
    private readonly McpWorkspaceService _service;
    public GetWorkspaceHealthHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceHealthResult> ExecuteAsync(GetWorkspaceHealthQuery query, CancellationToken cancellationToken = default)
        => _service.GetHealthAsync(query.Key, cancellationToken);
}

public sealed class GetWorkspaceGlobalPromptQuery : IQuery<McpWorkspaceGlobalPromptResult>;

public sealed class GetWorkspaceGlobalPromptHandler : IQueryHandler<GetWorkspaceGlobalPromptQuery, McpWorkspaceGlobalPromptResult>
{
    private readonly McpWorkspaceService _service;
    public GetWorkspaceGlobalPromptHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceGlobalPromptResult> ExecuteAsync(
        GetWorkspaceGlobalPromptQuery query,
        CancellationToken cancellationToken = default)
        => _service.GetGlobalPromptAsync(cancellationToken);
}

public sealed class CreateWorkspaceCommand : ICommand<McpWorkspaceMutationResult>
{
    public McpWorkspaceCreateRequest Request { get; }
    public CreateWorkspaceCommand(McpWorkspaceCreateRequest request) => Request = request;
}

public sealed class CreateWorkspaceHandler : ICommandHandler<CreateWorkspaceCommand, McpWorkspaceMutationResult>
{
    private readonly McpWorkspaceService _service;
    public CreateWorkspaceHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceMutationResult> ExecuteAsync(CreateWorkspaceCommand command, CancellationToken cancellationToken = default)
        => _service.CreateAsync(command.Request, cancellationToken);
}

public sealed class UpdateWorkspaceCommand : ICommand<McpWorkspaceMutationResult>
{
    public string Key { get; }
    public McpWorkspaceUpdateRequest Request { get; }
    public UpdateWorkspaceCommand(string key, McpWorkspaceUpdateRequest request)
    {
        Key = key;
        Request = request;
    }
}

public sealed class UpdateWorkspaceHandler : ICommandHandler<UpdateWorkspaceCommand, McpWorkspaceMutationResult>
{
    private readonly McpWorkspaceService _service;
    public UpdateWorkspaceHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceMutationResult> ExecuteAsync(UpdateWorkspaceCommand command, CancellationToken cancellationToken = default)
        => _service.UpdateAsync(command.Key, command.Request, cancellationToken);
}

public sealed class DeleteWorkspaceCommand : ICommand<McpWorkspaceMutationResult>
{
    public string Key { get; }
    public DeleteWorkspaceCommand(string key) => Key = key;
}

public sealed class DeleteWorkspaceHandler : ICommandHandler<DeleteWorkspaceCommand, McpWorkspaceMutationResult>
{
    private readonly McpWorkspaceService _service;
    public DeleteWorkspaceHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceMutationResult> ExecuteAsync(DeleteWorkspaceCommand command, CancellationToken cancellationToken = default)
        => _service.DeleteAsync(command.Key, cancellationToken);
}

public sealed class InitWorkspaceCommand : ICommand<McpWorkspaceInitResult>
{
    public string Key { get; }
    public InitWorkspaceCommand(string key) => Key = key;
}

public sealed class InitWorkspaceHandler : ICommandHandler<InitWorkspaceCommand, McpWorkspaceInitResult>
{
    private readonly McpWorkspaceService _service;
    public InitWorkspaceHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceInitResult> ExecuteAsync(InitWorkspaceCommand command, CancellationToken cancellationToken = default)
        => _service.InitAsync(command.Key, cancellationToken);
}

public sealed class StartWorkspaceCommand : ICommand<McpWorkspaceProcessStatus>
{
    public string Key { get; }
    public StartWorkspaceCommand(string key) => Key = key;
}

public sealed class StartWorkspaceHandler : ICommandHandler<StartWorkspaceCommand, McpWorkspaceProcessStatus>
{
    private readonly McpWorkspaceService _service;
    public StartWorkspaceHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceProcessStatus> ExecuteAsync(StartWorkspaceCommand command, CancellationToken cancellationToken = default)
        => _service.StartAsync(command.Key, cancellationToken);
}

public sealed class StopWorkspaceCommand : ICommand<McpWorkspaceProcessStatus>
{
    public string Key { get; }
    public StopWorkspaceCommand(string key) => Key = key;
}

public sealed class StopWorkspaceHandler : ICommandHandler<StopWorkspaceCommand, McpWorkspaceProcessStatus>
{
    private readonly McpWorkspaceService _service;
    public StopWorkspaceHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceProcessStatus> ExecuteAsync(StopWorkspaceCommand command, CancellationToken cancellationToken = default)
        => _service.StopAsync(command.Key, cancellationToken);
}

public sealed class UpdateWorkspaceGlobalPromptCommand : ICommand<McpWorkspaceGlobalPromptResult>
{
    public string? Template { get; }
    public UpdateWorkspaceGlobalPromptCommand(string? template) => Template = template;
}

public sealed class UpdateWorkspaceGlobalPromptHandler : ICommandHandler<UpdateWorkspaceGlobalPromptCommand, McpWorkspaceGlobalPromptResult>
{
    private readonly McpWorkspaceService _service;
    public UpdateWorkspaceGlobalPromptHandler(McpWorkspaceService service) => _service = service;

    public Task<McpWorkspaceGlobalPromptResult> ExecuteAsync(
        UpdateWorkspaceGlobalPromptCommand command,
        CancellationToken cancellationToken = default)
        => _service.UpdateGlobalPromptAsync(
            new McpWorkspaceGlobalPromptUpdateRequest { Template = command.Template },
            cancellationToken);
}
