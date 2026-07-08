using System.Linq;
using McpServerManager.UI.Core.Services;
using Xunit;

namespace McpServerManager.UI.Core.Tests.Services;

/// <summary>
/// PLAN-C2-TODO-FAMILY-001 / TR-HANDLER-EXTRACTION: verifies the command-mapping logic
/// (Build*Command / RequireTrimmed / Normalize / GetActiveTodoId) extracted from
/// <c>TodoDetailViewModel</c> into the pure <see cref="TodoEditorMapper"/>.
/// </summary>
public sealed class TodoEditorMapperTests
{
    /// <summary>Required fields are trimmed; optional fields normalized to null when blank; lists parsed.</summary>
    [Fact]
    public void ToCreateCommand_TrimsRequired_NormalizesOptional_ParsesLists()
    {
        var snapshot = new TodoEditorSnapshot
        {
            Id = "  T-1 ",
            Title = " Title ",
            Section = " sec ",
            Priority = " high ",
            Estimate = "   ",
            DependsOnText = "T-0\n\nT-9",
            ImplementationTasksText = "[x] a\n[ ] b",
        };

        var cmd = TodoEditorMapper.ToCreateCommand(snapshot);

        Assert.Equal("T-1", cmd.Id);
        Assert.Equal("Title", cmd.Title);
        Assert.Equal("sec", cmd.Section);
        Assert.Equal("high", cmd.Priority);
        Assert.Null(cmd.Estimate);
        Assert.Equal(new[] { "T-0", "T-9" }, cmd.DependsOn);
        Assert.NotNull(cmd.ImplementationTasks);
        Assert.Equal(2, cmd.ImplementationTasks!.Count);
        Assert.True(cmd.ImplementationTasks![0].Done);
    }

    /// <summary>Update command carries the done flag and completion fields.</summary>
    [Fact]
    public void ToUpdateCommand_CarriesDoneAndCompletionFields()
    {
        var snapshot = new TodoEditorSnapshot
        {
            Id = "T-2",
            Done = true,
            CompletedDate = "2026-07-07",
            DoneSummary = "done",
        };

        var cmd = TodoEditorMapper.ToUpdateCommand(snapshot);

        Assert.Equal("T-2", cmd.TodoId);
        Assert.True(cmd.Done);
        Assert.Equal("2026-07-07", cmd.CompletedDate);
        Assert.Equal("done", cmd.DoneSummary);
    }

    /// <summary>Delete command uses the trimmed editor id.</summary>
    [Fact]
    public void ToDeleteCommand_UsesTrimmedId()
        => Assert.Equal("T-3", TodoEditorMapper.ToDeleteCommand(new TodoEditorSnapshot { Id = " T-3 " }).TodoId);

    /// <summary>Active id prefers the editor id, falling back to the loaded TodoId.</summary>
    [Theory]
    [InlineData("T-ED", "T-LOADED", "T-ED")]
    [InlineData("   ", "T-LOADED", "T-LOADED")]
    [InlineData(null, "T-LOADED", "T-LOADED")]
    public void ActiveTodoId_PrefersEditorIdThenTodoId(string? editorId, string? todoId, string expected)
        => Assert.Equal(expected, TodoEditorMapper.ActiveTodoId(new TodoEditorSnapshot { Id = editorId, TodoId = todoId }));
}
