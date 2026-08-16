using McpServer.Client.Models;

namespace McpServerManager.UI.Core.Services;

/// <summary>Host-provided API abstraction for use-case management.</summary>
public interface IUseCaseService
{
    Task<IReadOnlyList<UseCaseSummary>> ListAsync(string? title, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDetail> GetAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDetail> CreateAsync(CreateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDetail> UpdateAsync(long useCaseId, UpdateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseActor> AttachActorAsync(long useCaseId, AttachUseCaseActorRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseFlow> AddFlowAsync(long useCaseId, AddUseCaseFlowRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseStep> AddStepAsync(long useCaseId, long flowId, AddUseCaseStepRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseFrLink> LinkFrAsync(long useCaseId, LinkUseCaseToFrRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task UnlinkFrAsync(long useCaseId, string frId, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDetail> SetApprovalAsync(long useCaseId, SetUseCaseApprovalRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDetail> SetProductAsync(long useCaseId, SetUseCaseProductRequest request, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseFrCoverage> GetCoverageAsync(string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDiagram> GetDiagramAsync(long useCaseId, string format, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDiagramGraph> GetDiagramGraphAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default);

    Task<UseCaseDiagramGraph> PutDiagramGraphAsync(long useCaseId, UseCaseDiagramGraph graph, string? workspacePath, CancellationToken cancellationToken = default);
}

internal sealed class NoOpUseCaseService : IUseCaseService
{
    public Task<IReadOnlyList<UseCaseSummary>> ListAsync(string? title, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UseCaseSummary>>(Array.Empty<UseCaseSummary>());

    public Task<UseCaseDetail> GetAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDetail { UseCaseId = useCaseId, Title = string.Empty });

    public Task<UseCaseDetail> CreateAsync(CreateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDetail { UseCaseId = 0, Title = request.Title, BriefDescription = request.BriefDescription });

    public Task<UseCaseDetail> UpdateAsync(long useCaseId, UpdateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDetail { UseCaseId = useCaseId, Title = request.Title ?? string.Empty, BriefDescription = request.BriefDescription });

    public Task DeleteAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<UseCaseActor> AttachActorAsync(long useCaseId, AttachUseCaseActorRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseActor { ActorId = request.ActorId ?? 0, Name = request.Name ?? string.Empty, Type = request.Type, IsPrimary = request.IsPrimary });

    public Task<UseCaseFlow> AddFlowAsync(long useCaseId, AddUseCaseFlowRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseFlow { FlowId = 0, FlowType = string.IsNullOrWhiteSpace(request.FlowType) ? "Basic" : request.FlowType, Name = request.Name, SequenceNumber = request.SequenceNumber ?? 0 });

    public Task<UseCaseStep> AddStepAsync(long useCaseId, long flowId, AddUseCaseStepRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseStep { StepId = 0, FlowId = flowId, StepNumber = request.StepNumber ?? 0, ActorId = request.ActorId, Action = request.Action, SystemResponse = request.SystemResponse, DataEntities = request.DataEntities });

    public Task<UseCaseFrLink> LinkFrAsync(long useCaseId, LinkUseCaseToFrRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseFrLink { FrId = request.FrId, LinkType = string.IsNullOrWhiteSpace(request.LinkType) ? "Realizes" : request.LinkType });

    public Task UnlinkFrAsync(long useCaseId, string frId, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<UseCaseDetail> SetApprovalAsync(long useCaseId, SetUseCaseApprovalRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDetail { UseCaseId = useCaseId, ApprovalStatus = request.Status });

    public Task<UseCaseDetail> SetProductAsync(long useCaseId, SetUseCaseProductRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDetail { UseCaseId = useCaseId, ProductKey = request.ProductKey });

    public Task<UseCaseFrCoverage> GetCoverageAsync(string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseFrCoverage());

    public Task<UseCaseDiagram> GetDiagramAsync(long useCaseId, string format, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDiagram { UseCaseId = useCaseId, Format = format, Content = string.Empty });

    public Task<UseCaseDiagramGraph> GetDiagramGraphAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new UseCaseDiagramGraph());

    public Task<UseCaseDiagramGraph> PutDiagramGraphAsync(long useCaseId, UseCaseDiagramGraph graph, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(graph);
}
