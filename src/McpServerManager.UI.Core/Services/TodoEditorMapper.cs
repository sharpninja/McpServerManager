using McpServerManager.UI.Core.Messages;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Immutable capture of the TODO editor field state. Read from <c>TodoDetailViewModel</c> at
/// dispatch time so the ViewModel holds state only (TR-VM-STATE-ONLY, PLAN-C2-TODO-FAMILY-001).
/// </summary>
public sealed record TodoEditorSnapshot
{
    /// <summary>Editor id field.</summary>
    public string? Id { get; init; }

    /// <summary>Editor title field.</summary>
    public string? Title { get; init; }

    /// <summary>Editor section field.</summary>
    public string? Section { get; init; }

    /// <summary>Editor priority field.</summary>
    public string? Priority { get; init; }

    /// <summary>Editor done flag.</summary>
    public bool Done { get; init; }

    /// <summary>Editor estimate field.</summary>
    public string? Estimate { get; init; }

    /// <summary>Editor note field.</summary>
    public string? Note { get; init; }

    /// <summary>Editor completed-date field.</summary>
    public string? CompletedDate { get; init; }

    /// <summary>Editor done-summary field.</summary>
    public string? DoneSummary { get; init; }

    /// <summary>Editor remaining field.</summary>
    public string? Remaining { get; init; }

    /// <summary>Editor phase field.</summary>
    public string? Phase { get; init; }

    /// <summary>Editor description multi-line text.</summary>
    public string? DescriptionText { get; init; }

    /// <summary>Editor technical-details multi-line text.</summary>
    public string? TechnicalDetailsText { get; init; }

    /// <summary>Editor implementation-tasks multi-line text.</summary>
    public string? ImplementationTasksText { get; init; }

    /// <summary>Editor depends-on multi-line text.</summary>
    public string? DependsOnText { get; init; }

    /// <summary>Editor functional-requirements multi-line text.</summary>
    public string? FunctionalRequirementsText { get; init; }

    /// <summary>Editor technical-requirements multi-line text.</summary>
    public string? TechnicalRequirementsText { get; init; }

    /// <summary>The currently loaded TODO id (fallback for the active id).</summary>
    public string? TodoId { get; init; }
}

/// <summary>
/// Pure mapping from a <see cref="TodoEditorSnapshot"/> to CQRS command/query records.
/// Extracted from <c>TodoDetailViewModel</c> (Build*Command / RequireTrimmed / Normalize /
/// GetActiveTodoId) so the ViewModel holds no mapping/validation logic
/// (PLAN-C2-TODO-FAMILY-001, TR-HANDLER-EXTRACTION, FR-VM-HANDLER-MUTATION).
/// </summary>
public static class TodoEditorMapper
{
    /// <summary>Builds a <see cref="CreateTodoCommand"/> from the editor snapshot.</summary>
    /// <param name="s">The editor snapshot.</param>
    /// <returns>The mapped create command.</returns>
    public static CreateTodoCommand ToCreateCommand(TodoEditorSnapshot s) => new()
    {
        Id = RequireTrimmed(s.Id),
        Title = RequireTrimmed(s.Title),
        Section = RequireTrimmed(s.Section),
        Priority = RequireTrimmed(s.Priority),
        Estimate = Normalize(s.Estimate),
        Note = Normalize(s.Note),
        Remaining = Normalize(s.Remaining),
        Phase = Normalize(s.Phase),
        Description = TodoMarkdownSerializer.ParseLines(s.DescriptionText),
        TechnicalDetails = TodoMarkdownSerializer.ParseLines(s.TechnicalDetailsText),
        ImplementationTasks = TodoMarkdownSerializer.ParseTasks(s.ImplementationTasksText),
        DependsOn = TodoMarkdownSerializer.ParseLines(s.DependsOnText),
        FunctionalRequirements = TodoMarkdownSerializer.ParseLines(s.FunctionalRequirementsText),
        TechnicalRequirements = TodoMarkdownSerializer.ParseLines(s.TechnicalRequirementsText),
    };

    /// <summary>Builds an <see cref="UpdateTodoCommand"/> from the editor snapshot.</summary>
    /// <param name="s">The editor snapshot.</param>
    /// <returns>The mapped update command.</returns>
    public static UpdateTodoCommand ToUpdateCommand(TodoEditorSnapshot s) => new()
    {
        TodoId = RequireTrimmed(s.Id),
        Title = Normalize(s.Title),
        Section = Normalize(s.Section),
        Priority = Normalize(s.Priority),
        Done = s.Done,
        Estimate = Normalize(s.Estimate),
        Note = Normalize(s.Note),
        CompletedDate = Normalize(s.CompletedDate),
        DoneSummary = Normalize(s.DoneSummary),
        Remaining = Normalize(s.Remaining),
        Phase = Normalize(s.Phase),
        Description = TodoMarkdownSerializer.ParseLines(s.DescriptionText),
        TechnicalDetails = TodoMarkdownSerializer.ParseLines(s.TechnicalDetailsText),
        ImplementationTasks = TodoMarkdownSerializer.ParseTasks(s.ImplementationTasksText),
        DependsOn = TodoMarkdownSerializer.ParseLines(s.DependsOnText),
        FunctionalRequirements = TodoMarkdownSerializer.ParseLines(s.FunctionalRequirementsText),
        TechnicalRequirements = TodoMarkdownSerializer.ParseLines(s.TechnicalRequirementsText),
    };

    /// <summary>Builds a <see cref="DeleteTodoCommand"/> from the editor snapshot.</summary>
    /// <param name="s">The editor snapshot.</param>
    /// <returns>The mapped delete command.</returns>
    public static DeleteTodoCommand ToDeleteCommand(TodoEditorSnapshot s) => new(RequireTrimmed(s.Id));

    /// <summary>Resolves the active TODO id: the editor id when present, else the loaded id.</summary>
    /// <param name="s">The editor snapshot.</param>
    /// <returns>The active TODO id (may be empty).</returns>
    public static string ActiveTodoId(TodoEditorSnapshot s)
    {
        var editorId = RequireTrimmed(s.Id);
        return !string.IsNullOrEmpty(editorId) ? editorId : RequireTrimmed(s.TodoId);
    }

    private static string RequireTrimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
