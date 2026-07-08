using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using McpServerManager.UI.Core.Messages;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// Converts <see cref="TodoDetail"/> to/from a structured Markdown representation.
/// Reusable across UIs (TR-MCP-DRY-001).
/// </summary>
public static class TodoMarkdownSerializer
{
    private static readonly Regex MetaRowRegex = new(
        @"^\|\s*(?<key>[^|]+?)\s*\|\s*(?<val>[^|]*?)\s*\|",
        RegexOptions.Compiled);

    /// <summary>Serialize a <see cref="TodoDetail"/> to Markdown.</summary>
    /// <param name="detail">The TODO item to serialize.</param>
    /// <returns>A Markdown string representing the TODO item.</returns>
    public static string Serialize(TodoDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var sb = new StringBuilder();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# {detail.Title}");
        sb.AppendLine();
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|-------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| ID | {detail.Id} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Section | {detail.Section} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Priority | {detail.Priority} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Status | {(detail.Done ? "Done" : "Open")} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Estimate | {detail.Estimate ?? ""} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Note | {detail.Note ?? ""} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Phase | {detail.Phase ?? ""} |");
        sb.AppendLine();

        AppendSection(sb, "Description", detail.Description);
        AppendSection(sb, "Technical Details", detail.TechnicalDetails);
        AppendTasksSection(sb, detail.ImplementationTasks);
        AppendSection(sb, "Dependencies", detail.DependsOn);
        AppendSection(sb, "Functional Requirements", detail.FunctionalRequirements);
        AppendSection(sb, "Technical Requirements", detail.TechnicalRequirements);

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>Deserialize Markdown into individual editor field values.</summary>
    /// <param name="markdown">The Markdown text to parse.</param>
    /// <returns>Parsed field values for the ViewModel editor.</returns>
    public static TodoMarkdownFields Deserialize(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var fields = new TodoMarkdownFields();
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string? currentSection = null;
        var sectionContent = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Title: "# ..."
            if (line.StartsWith("# ", StringComparison.Ordinal) && fields.Title is null)
            {
                fields.Title = line[2..].Trim();
                continue;
            }

            // Metadata table row: "| Key | Value |"
            if (line.StartsWith("| ", StringComparison.Ordinal))
            {
                var match = MetaRowRegex.Match(line);
                if (match.Success)
                {
                    var key = match.Groups["key"].Value.Trim();
                    var val = match.Groups["val"].Value.Trim();
                    ApplyMetaField(fields, key, val);
                }

                continue;
            }

            // Section heading: "## ..."
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushSection(fields, currentSection, sectionContent);
                currentSection = line[3..].Trim();
                sectionContent.Clear();
                continue;
            }

            // Table header separators
            if (line.StartsWith("|---", StringComparison.Ordinal))
                continue;

            // Content within a section
            if (currentSection is not null)
            {
                sectionContent.Add(line);
            }
        }

        FlushSection(fields, currentSection, sectionContent);

        return fields;
    }

    private static void AppendSection(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"## {heading}");
        sb.AppendLine();
        if (items.Count == 0)
        {
            sb.AppendLine("*(none)*");
        }
        else
        {
            foreach (var item in items)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {item}");
            }
        }

        sb.AppendLine();
    }

    private static void AppendTasksSection(StringBuilder sb, IReadOnlyList<TodoTaskDetail> tasks)
    {
        sb.AppendLine("## Implementation Tasks");
        sb.AppendLine();
        if (tasks.Count == 0)
        {
            sb.AppendLine("*(none)*");
        }
        else
        {
            foreach (var task in tasks)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- [{(task.Done ? "x" : " ")}] {task.Task}");
            }
        }

        sb.AppendLine();
    }

    private static void ApplyMetaField(TodoMarkdownFields fields, string key, string val)
    {
        switch (key)
        {
            case "ID":
                fields.Id = val;
                break;
            case "Section":
                fields.Section = val;
                break;
            case "Priority":
                fields.Priority = val;
                break;
            case "Status":
                fields.Done = string.Equals(val, "Done", StringComparison.OrdinalIgnoreCase);
                break;
            case "Estimate":
                fields.Estimate = string.IsNullOrWhiteSpace(val) ? null : val;
                break;
            case "Note":
                fields.Note = string.IsNullOrWhiteSpace(val) ? null : val;
                break;
            case "Phase":
                fields.Phase = string.IsNullOrWhiteSpace(val) ? null : val;
                break;
        }
    }

    private static void FlushSection(TodoMarkdownFields fields, string? section, List<string> content)
    {
        if (section is null)
            return;

        var lines = content
            .Select(l => l.TrimStart().TrimStart('-').Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) &&
                        !string.Equals(l, "*(none)*", StringComparison.Ordinal))
            .ToList();

        switch (section)
        {
            case "Description":
                fields.DescriptionText = JoinNonEmpty(lines);
                break;
            case "Technical Details":
                fields.TechnicalDetailsText = JoinNonEmpty(lines);
                break;
            case "Implementation Tasks":
                fields.ImplementationTasksText = ParseTaskLines(content);
                break;
            case "Dependencies":
                fields.DependsOnText = JoinNonEmpty(lines);
                break;
            case "Functional Requirements":
                fields.FunctionalRequirementsText = JoinNonEmpty(lines);
                break;
            case "Technical Requirements":
                fields.TechnicalRequirementsText = JoinNonEmpty(lines);
                break;
        }
    }

    private static string? ParseTaskLines(List<string> rawLines)
    {
        var tasks = new List<string>();
        foreach (var raw in rawLines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) ||
                string.Equals(line, "*(none)*", StringComparison.Ordinal))
                continue;

            // Normalize "- [x] task" or "- [ ] task" into "[x] task" / "[ ] task"
            if (line.StartsWith("- [", StringComparison.Ordinal))
                line = line[2..].Trim();

            tasks.Add(line);
        }

        return tasks.Count == 0 ? null : string.Join(Environment.NewLine, tasks);
    }

    private static string? JoinNonEmpty(List<string> lines)
        => lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);

    /// <summary>
    /// Splits multi-line editor text into trimmed, non-empty lines.
    /// TR-HANDLER-EXTRACTION: shared parse logic lifted out of <c>TodoDetailViewModel</c>.
    /// </summary>
    /// <param name="text">Raw multi-line text.</param>
    /// <returns>The parsed lines, or <see langword="null"/> when the input is blank.</returns>
    public static IReadOnlyList<string>? ParseLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return lines.Count == 0 ? null : lines;
    }

    /// <summary>
    /// Parses checkbox/dash task lines (<c>[x]</c>, <c>[ ]</c>, or <c>- </c> prefixes) into task items.
    /// TR-HANDLER-EXTRACTION: shared parse logic lifted out of <c>TodoDetailViewModel</c>.
    /// </summary>
    /// <param name="text">Raw multi-line task text.</param>
    /// <returns>The parsed tasks, or <see langword="null"/> when the input is blank.</returns>
    public static IReadOnlyList<TodoTaskDetail>? ParseTasks(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var tasks = new List<TodoTaskDetail>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var done = false;
            if (line.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
            {
                done = true;
                line = line.Substring(3).Trim();
            }
            else if (line.StartsWith("[ ]", StringComparison.OrdinalIgnoreCase))
            {
                line = line.Substring(3).Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                line = line.Substring(2).Trim();
            }

            if (!string.IsNullOrWhiteSpace(line))
                tasks.Add(new TodoTaskDetail(line, done));
        }

        return tasks.Count == 0 ? null : tasks;
    }

    /// <summary>Joins non-empty line values into multi-line editor text, or <see langword="null"/> when empty.</summary>
    /// <param name="values">Line values.</param>
    /// <returns>The joined text, or <see langword="null"/> when empty.</returns>
    public static string? FormatLines(IReadOnlyList<string> values)
        => values.Count == 0 ? null : string.Join(Environment.NewLine, values);

    /// <summary>Formats task items as checkbox lines, or <see langword="null"/> when empty.</summary>
    /// <param name="tasks">Task items.</param>
    /// <returns>The checkbox text, or <see langword="null"/> when empty.</returns>
    public static string? FormatTasks(IReadOnlyList<TodoTaskDetail> tasks)
        => tasks.Count == 0
            ? null
            : string.Join(Environment.NewLine, tasks.Select(t => $"{(t.Done ? "[x]" : "[ ]")} {t.Task}"));
}

/// <summary>
/// Parsed fields from a TODO Markdown document.
/// Used to populate the ViewModel editor properties.
/// </summary>
public sealed class TodoMarkdownFields
{
    /// <summary>Parsed title.</summary>
    public string? Title { get; set; }

    /// <summary>Parsed ID.</summary>
    public string? Id { get; set; }

    /// <summary>Parsed section.</summary>
    public string? Section { get; set; }

    /// <summary>Parsed priority.</summary>
    public string? Priority { get; set; }

    /// <summary>Parsed done status.</summary>
    public bool Done { get; set; }

    /// <summary>Parsed estimate.</summary>
    public string? Estimate { get; set; }

    /// <summary>Parsed note.</summary>
    public string? Note { get; set; }

    /// <summary>Parsed phase label.</summary>
    public string? Phase { get; set; }

    /// <summary>Parsed description as multi-line text.</summary>
    public string? DescriptionText { get; set; }

    /// <summary>Parsed technical details as multi-line text.</summary>
    public string? TechnicalDetailsText { get; set; }

    /// <summary>Parsed implementation tasks as checkbox text.</summary>
    public string? ImplementationTasksText { get; set; }

    /// <summary>Parsed dependency IDs as multi-line text.</summary>
    public string? DependsOnText { get; set; }

    /// <summary>Parsed functional requirement IDs as multi-line text.</summary>
    public string? FunctionalRequirementsText { get; set; }

    /// <summary>Parsed technical requirement IDs as multi-line text.</summary>
    public string? TechnicalRequirementsText { get; set; }
}
