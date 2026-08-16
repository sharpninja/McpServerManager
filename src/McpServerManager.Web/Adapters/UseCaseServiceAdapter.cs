using McpServer.Client.Models;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.Web.Adapters;

internal sealed class UseCaseServiceAdapter : IUseCaseService
{
    private readonly WebMcpContext _context;

    public UseCaseServiceAdapter(WebMcpContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<UseCaseSummary>> ListAsync(string? title, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.ListAsync(Normalize(title), ct), cancellationToken);

    public Task<UseCaseDetail> GetAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.GetAsync(useCaseId, ct), cancellationToken);

    public Task<UseCaseDetail> CreateAsync(CreateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.CreateAsync(request, ct), cancellationToken);

    public Task<UseCaseDetail> UpdateAsync(long useCaseId, UpdateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.UpdateAsync(useCaseId, request, ct), cancellationToken);

    public Task DeleteAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, async (client, ct) =>
        {
            await client.UseCases.DeleteAsync(useCaseId, ct).ConfigureAwait(true);
            return true;
        }, cancellationToken);

    public Task<UseCaseActor> AttachActorAsync(long useCaseId, AttachUseCaseActorRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.AttachActorAsync(useCaseId, request, ct), cancellationToken);

    public Task<UseCaseFlow> AddFlowAsync(long useCaseId, AddUseCaseFlowRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.AddFlowAsync(useCaseId, request, ct), cancellationToken);

    public Task<UseCaseStep> AddStepAsync(long useCaseId, long flowId, AddUseCaseStepRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.AddStepAsync(useCaseId, flowId, request, ct), cancellationToken);

    public Task<UseCaseFrLink> LinkFrAsync(long useCaseId, LinkUseCaseToFrRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.LinkFrAsync(useCaseId, request, ct), cancellationToken);

    public Task UnlinkFrAsync(long useCaseId, string frId, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, async (client, ct) =>
        {
            await client.UseCases.UnlinkFrAsync(useCaseId, frId, ct).ConfigureAwait(true);
            return true;
        }, cancellationToken);

    public Task<UseCaseDetail> SetApprovalAsync(long useCaseId, SetUseCaseApprovalRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.SetApprovalAsync(useCaseId, request, ct), cancellationToken);

    public Task<UseCaseDetail> SetProductAsync(long useCaseId, SetUseCaseProductRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.SetProductAsync(useCaseId, request, ct), cancellationToken);

    public Task<UseCaseFrCoverage> GetCoverageAsync(string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.GetCoverageAsync(ct), cancellationToken);

    public Task<UseCaseDiagram> GetDiagramAsync(long useCaseId, string format, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.GetDiagramAsync(useCaseId, format, "usecase", ct), cancellationToken);

    public Task<UseCaseDiagramGraph> GetDiagramGraphAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.GetDiagramGraphAsync(useCaseId, ct), cancellationToken);

    public Task<UseCaseDiagramGraph> PutDiagramGraphAsync(long useCaseId, UseCaseDiagramGraph graph, string? workspacePath, CancellationToken cancellationToken = default)
        => UseWorkspaceAsync(workspacePath, (client, ct) => client.UseCases.PutDiagramGraphAsync(useCaseId, graph, ct), cancellationToken);

    private Task<T> UseWorkspaceAsync<T>(string? workspacePath, Func<McpServer.Client.McpServerClient, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var resolvedWorkspacePath = string.IsNullOrWhiteSpace(workspacePath) ? _context.ActiveWorkspacePath : workspacePath;
        if (string.IsNullOrWhiteSpace(resolvedWorkspacePath))
            throw new InvalidOperationException("Workspace required. Select a workspace before editing use cases.");

        return _context.UseWorkspaceApiClientAsync(resolvedWorkspacePath, operation, cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
