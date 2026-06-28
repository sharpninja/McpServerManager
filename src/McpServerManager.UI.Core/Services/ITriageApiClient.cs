using McpServerManager.UI.Core.Messages;

namespace McpServerManager.UI.Core.Services;

/// <summary>Host-provided API abstraction for triage dashboard operations.</summary>
public interface ITriageApiClient
{
    /// <summary>Loads the dashboard projection for an optional workspace.</summary>
    Task<TriageDashboardSnapshot> GetDashboardAsync(string? workspacePath, CancellationToken cancellationToken = default);

    /// <summary>Lists triage groups by optional status and workspace.</summary>
    Task<TriageGroupQuerySnapshot> QueryGroupsAsync(string? status, string? workspacePath, CancellationToken cancellationToken = default);

    /// <summary>Loads a triage group by ID.</summary>
    Task<TriageGroupSnapshot?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Loads a triage report by ID.</summary>
    Task<TriageReportSnapshot?> GetReportAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Lists triage research runs by optional status, group, and workspace.</summary>
    Task<TriageRunQuerySnapshot> QueryRunsAsync(string? status, string? groupId, string? workspacePath, CancellationToken cancellationToken = default);

    /// <summary>Loads a triage research run by ID.</summary>
    Task<TriageRunSnapshot?> GetRunAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>Loads open TODOs created by triage, hydrating through each TODO's owning workspace.</summary>
    Task<OpenTriageTodosResult> QueryOpenCreatedTodosAsync(string? workspacePath, CancellationToken cancellationToken = default);

    /// <summary>Creates a new triage group from selected reports or groups.</summary>
    Task<TriageGroupEditResultSnapshot> CreateGroupFromSelectionAsync(TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default);

    /// <summary>Moves selected reports or groups into an existing triage group.</summary>
    Task<TriageGroupEditResultSnapshot> ConsolidateIntoGroupAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default);

    /// <summary>Merges selected source groups into an existing triage group.</summary>
    Task<TriageGroupEditResultSnapshot> MergeGroupsAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default);
}

/// <summary>Empty fallback for hosts that do not expose triage UI.</summary>
internal sealed class NoOpTriageApiClient : ITriageApiClient
{
    public Task<TriageDashboardSnapshot> GetDashboardAsync(string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new TriageDashboardSnapshot([], [], [], 0, 0));

    public Task<TriageGroupQuerySnapshot> QueryGroupsAsync(string? status, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new TriageGroupQuerySnapshot([], 0));

    public Task<TriageGroupSnapshot?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default)
        => Task.FromResult<TriageGroupSnapshot?>(null);

    public Task<TriageReportSnapshot?> GetReportAsync(string reportId, CancellationToken cancellationToken = default)
        => Task.FromResult<TriageReportSnapshot?>(null);

    public Task<TriageRunQuerySnapshot> QueryRunsAsync(string? status, string? groupId, string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new TriageRunQuerySnapshot([], 0));

    public Task<TriageRunSnapshot?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
        => Task.FromResult<TriageRunSnapshot?>(null);

    public Task<OpenTriageTodosResult> QueryOpenCreatedTodosAsync(string? workspacePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new OpenTriageTodosResult([], 0, 0, 0));

    public Task<TriageGroupEditResultSnapshot> CreateGroupFromSelectionAsync(TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
        => Task.FromException<TriageGroupEditResultSnapshot>(new NotSupportedException("Triage edits are not available."));

    public Task<TriageGroupEditResultSnapshot> ConsolidateIntoGroupAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
        => Task.FromException<TriageGroupEditResultSnapshot>(new NotSupportedException("Triage edits are not available."));

    public Task<TriageGroupEditResultSnapshot> MergeGroupsAsync(string targetGroupId, TriageGroupSelectionSnapshot selection, CancellationToken cancellationToken = default)
        => Task.FromException<TriageGroupEditResultSnapshot>(new NotSupportedException("Triage edits are not available."));
}
