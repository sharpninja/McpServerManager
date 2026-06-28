using McpServer.Cqrs;

namespace McpServerManager.UI.Core.Messages;

/// <summary>Query to load the triage dashboard projection.</summary>
public sealed record GetTriageDashboardQuery(string? WorkspacePath) : IQuery<TriageDashboardSnapshot>;

/// <summary>Query to list triage groups.</summary>
public sealed record QueryTriageGroupsQuery(string? Status, string? WorkspacePath) : IQuery<TriageGroupQuerySnapshot>;

/// <summary>Query to load a triage group by ID.</summary>
public sealed record GetTriageGroupQuery(string GroupId) : IQuery<TriageGroupSnapshot?>;

/// <summary>Query to load a triage report by ID.</summary>
public sealed record GetTriageReportQuery(string ReportId) : IQuery<TriageReportSnapshot?>;

/// <summary>Query to list triage research runs.</summary>
public sealed record QueryTriageRunsQuery(string? Status, string? GroupId, string? WorkspacePath) : IQuery<TriageRunQuerySnapshot>;

/// <summary>Query to load a triage research run by ID.</summary>
public sealed record GetTriageRunQuery(string RunId) : IQuery<TriageRunSnapshot?>;

/// <summary>Query to load open TODOs created by triage.</summary>
public sealed record QueryOpenTriageTodosQuery(string? WorkspacePath) : IQuery<OpenTriageTodosResult>;

/// <summary>Command to create a new triage group from selected groups or reports.</summary>
public sealed record CreateTriageGroupFromSelectionCommand(TriageGroupSelectionSnapshot Selection)
    : ICommand<TriageGroupEditResultSnapshot>;

/// <summary>Command to move selected groups or reports into an existing triage group.</summary>
public sealed record ConsolidateTriageSelectionIntoGroupCommand(string TargetGroupId, TriageGroupSelectionSnapshot Selection)
    : ICommand<TriageGroupEditResultSnapshot>;

/// <summary>Command to merge selected source groups into an existing triage group.</summary>
public sealed record MergeTriageGroupsCommand(string TargetGroupId, TriageGroupSelectionSnapshot Selection)
    : ICommand<TriageGroupEditResultSnapshot>;

/// <summary>Command to retry a failed triage group.</summary>
public sealed record RetryTriageGroupCommand(string GroupId)
    : ICommand<TriageGroupSnapshot>;

/// <summary>Dashboard projection for triage queues and run history.</summary>
public sealed record TriageDashboardSnapshot(
    IReadOnlyList<TriageGroupSnapshot> TriageQueue,
    IReadOnlyList<TriageGroupSnapshot> ReportGroupQueue,
    IReadOnlyList<TriageRunSnapshot> RunHistory,
    int TotalGroupCount,
    int TotalRunCount);

/// <summary>Query result for triage groups.</summary>
public sealed record TriageGroupQuerySnapshot(IReadOnlyList<TriageGroupSnapshot> Items, int TotalCount);

/// <summary>Query result for triage runs.</summary>
public sealed record TriageRunQuerySnapshot(IReadOnlyList<TriageRunSnapshot> Items, int TotalCount);

/// <summary>Selected triage groups or reports for group edit operations.</summary>
public sealed record TriageGroupSelectionSnapshot(
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> ReportIds,
    string? Title = null,
    string? Summary = null);

/// <summary>Result returned after moving or merging triage reports.</summary>
public sealed record TriageGroupEditResultSnapshot(
    TriageGroupSnapshot Group,
    IReadOnlyList<string> RemovedGroupIds,
    int MovedReportCount);

/// <summary>Read-only triage group row/detail projection.</summary>
public sealed record TriageGroupSnapshot(
    string GroupId,
    string Status,
    int ReportCount,
    string? WorkspacePath,
    string? Title,
    string? Summary,
    DateTimeOffset QuietDeadlineUtc,
    string? CreatedTodoId,
    string? LastError,
    IReadOnlyList<TriageReportSnapshot> Reports);

/// <summary>Read-only triage report detail projection.</summary>
public sealed record TriageReportSnapshot(
    string ReportId,
    string GroupId,
    string Status,
    string? Title,
    string? Summary,
    string? OriginalWorkspacePath,
    string? WorkspacePath,
    DateTimeOffset CreatedUtc);

/// <summary>Read-only triage research run projection.</summary>
public sealed record TriageRunSnapshot(
    string RunId,
    string GroupId,
    string Status,
    string? WorkspacePath,
    string? GroupStatus,
    string? GroupTitle,
    string? GroupSummary,
    int ReportCount,
    string? PromptTemplateId,
    string? Prompt,
    string? GroupJson,
    string? RawOutput,
    string? ResponseJson,
    string? Error,
    string? CreatedTodoId,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc);

/// <summary>Open TODO items that were created by triage and still exist as active TODOs.</summary>
public sealed record OpenTriageTodoItem(
    string TodoId,
    string Title,
    string WorkspacePath,
    string? Section,
    string? Priority,
    string? GroupId,
    string? RunId,
    string? GroupStatus,
    string? RunStatus,
    DateTimeOffset CreatedAtUtc,
    string? GroupTitle,
    string? GroupSummary,
    int ReportCount,
    DateTimeOffset? QuietDeadlineUtc,
    bool Done = false,
    bool CanOpen = true);

/// <summary>Hydrated open TODO projection plus stale-reference counts.</summary>
public sealed record OpenTriageTodosResult(
    IReadOnlyList<OpenTriageTodoItem> Items,
    int TotalCreatedCount,
    int HiddenCompletedOrMissingCount,
    int HydrationErrorCount);
