using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using McpServerManager.Core.Models;

namespace McpServerManager.Core.Services;

/// <summary>Round-trip between McpTodoFlatItem and YAML front matter + markdown (same format as VS extension).</summary>
public static class TodoMarkdown
{
    public static string BlankTemplate()
    {
        return string.Join("\n",
            "---",
            "id: NEW-TODO",
            "section: mvp-app",
            "priority: low",
            "estimate: ",
            "phase: ",
            "depends-on: []",
            "---",
            "",
            "# ",
            "",
            "Description goes here.",
            "",
            "## Technical Details",
            "",
            "- ",
            "",
            "## Implementation Tasks",
            "",
            "- [ ] ",
            "");
    }

    public static string ToMarkdown(McpTodoFlatItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        var fm = new List<string> { "---" };
        fm.Add($"id: {item.Id}");
        fm.Add($"section: {item.Section ?? ""}");
        fm.Add($"priority: {item.Priority ?? ""}");
        if (item.Done) fm.Add("done: true");
        if (!string.IsNullOrEmpty(item.Estimate)) fm.Add($"estimate: {YamlScalar(item.Estimate!)}");
        if (!string.IsNullOrEmpty(item.Note)) fm.Add($"note: {YamlScalar(item.Note!)}");
        if (!string.IsNullOrEmpty(item.CompletedDate)) fm.Add($"completed: {item.CompletedDate}");
        if (!string.IsNullOrEmpty(item.DoneSummary)) fm.Add($"done-summary: {YamlScalar(item.DoneSummary!)}");
        if (!string.IsNullOrEmpty(item.Remaining)) fm.Add($"remaining: {YamlScalar(item.Remaining!)}");
        if (!string.IsNullOrEmpty(item.Phase)) fm.Add($"phase: {YamlScalar(item.Phase!)}");
        if (item.DependsOn?.Count > 0)
        {
            fm.Add("depends-on:");
            foreach (var d in item.DependsOn) fm.Add($"  - {d ?? ""}");
        }
        if (item.FunctionalRequirements?.Count > 0)
        {
            fm.Add("functional-requirements:");
            foreach (var fr in item.FunctionalRequirements) fm.Add($"  - {fr}");
        }
        if (item.TechnicalRequirements?.Count > 0)
        {
            fm.Add("technical-requirements:");
            foreach (var tr in item.TechnicalRequirements) fm.Add($"  - {tr}");
        }
        fm.Add("---");

        var body = new List<string> { "" };
        body.Add($"# {item.Title ?? ""}");
        body.Add("");

        if (item.Description?.Count > 0)
        {
            body.AddRange(item.Description);
            body.Add("");
        }
        if (item.TechnicalDetails?.Count > 0)
        {
            body.Add("## Technical Details");
            body.Add("");
            foreach (var d in item.TechnicalDetails) body.Add($"- {d}");
            body.Add("");
        }
        if (item.ImplementationTasks?.Count > 0)
        {
            body.Add("## Implementation Tasks");
            body.Add("");
            foreach (var t in item.ImplementationTasks)
                body.Add($"- [{(t.Done ? 'x' : ' ')}] {t.Task ?? ""}");
            body.Add("");
        }

        return string.Join("\n", fm) + string.Join("\n", body).TrimEnd();
    }

    public static McpTodoUpdateRequest FromMarkdown(string markdown)
    {
        if (markdown == null) throw new ArgumentNullException(nameof(markdown));
        var req = new McpTodoUpdateRequest();
        var (fm, bodyLines) = SplitFrontMatter(markdown);

        string? currentListKey = null;
        List<string>? currentList = null;

        foreach (var line in fm)
        {
            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal) && currentListKey != null)
            {
                var listValue = line.TrimStart().Substring(2).Trim();
                if (!string.IsNullOrEmpty(listValue))
                    currentList?.Add(listValue);
                continue;
            }

            if (currentListKey != null && currentList != null)
            {
                AssignListField(req, currentListKey, currentList);
                currentListKey = null;
                currentList = null;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line.Substring(0, colon).Trim().ToLowerInvariant();
            var value = line.Substring(colon + 1).Trim();

            if (key == "id") continue;

            if (value == "" || value == "[]")
            {
                currentListKey = key;
                currentList = new List<string>();
                if (value == "[]") AssignListField(req, key, currentList);
                continue;
            }

            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                var inner = value.Substring(1, value.Length - 2).Trim();
                var items = string.IsNullOrEmpty(inner)
                    ? new List<string>()
                    : new List<string>(inner.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                AssignListField(req, key, items);
                continue;
            }

            AssignScalarField(req, key, value);
        }

        if (currentListKey != null && currentList != null)
            AssignListField(req, currentListKey, currentList);

        // Parse body
        var description = new List<string>();
        var technicalDetails = new List<string>();
        var tasks = new List<McpTodoFlatTask>();
        var currentSection = "description";

        foreach (var line in bodyLines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("# ", StringComparison.Ordinal) && !trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                req.Title = trimmed.Substring(2).Trim();
                currentSection = "description";
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                var heading = trimmed.Substring(3).Trim().ToUpperInvariant();
                if (heading.Contains("TECHNICAL")) currentSection = "technical-details";
                else if (heading.Contains("IMPLEMENTATION") || heading.Contains("TASK")) currentSection = "implementation-tasks";
                else currentSection = "description";
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            switch (currentSection)
            {
                case "technical-details":
                    var bulletTd = Regex.Match(trimmed, @"^-\s+(.+)$");
                    technicalDetails.Add(bulletTd.Success ? bulletTd.Groups[1].Value : trimmed);
                    break;
                case "implementation-tasks":
                    var taskMatch = Regex.Match(trimmed, @"^-\s*\[([ xX])\]\s+(.+)$");
                    if (taskMatch.Success)
                        tasks.Add(new McpTodoFlatTask { Task = taskMatch.Groups[2].Value, Done = taskMatch.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase) });
                    else
                    {
                        var plainBullet = Regex.Match(trimmed, @"^-\s+(.+)$");
                        if (plainBullet.Success)
                            tasks.Add(new McpTodoFlatTask { Task = plainBullet.Groups[1].Value, Done = false });
                    }
                    break;
                default:
                    description.Add(trimmed);
                    break;
            }
        }

        if (description.Count > 0) req.Description = description;
        if (technicalDetails.Count > 0) req.TechnicalDetails = technicalDetails;
        if (tasks.Count > 0) req.ImplementationTasks = tasks;

        return req;
    }

    /// <summary>
    /// Returns the body of the document (everything after the YAML front matter), or the whole
    /// document when there is no front matter. UI-TODO-001: the raw editor shows body only.
    /// </summary>
    /// <param name="markdown">The full TODO markdown.</param>
    /// <returns>The body text with leading blank lines trimmed.</returns>
    public static string ExtractBody(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var (_, body) = SplitFrontMatter(markdown);
        return string.Join("\n", body).TrimStart('\r', '\n').TrimEnd();
    }

    /// <summary>
    /// Parses the structured front-matter fields for the metadata form (UI-TODO-001).
    /// </summary>
    /// <param name="markdown">The full TODO markdown.</param>
    /// <returns>The parsed front-matter fields.</returns>
    public static TodoFrontMatter ParseFrontMatter(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var req = FromMarkdown(markdown);
        return new TodoFrontMatter
        {
            Id = ExtractId(markdown) ?? "",
            Section = req.Section ?? "",
            Priority = req.Priority ?? "",
            Estimate = req.Estimate,
            Phase = req.Phase,
            Done = req.Done ?? false,
            DependsOn = req.DependsOn is { Count: > 0 } d ? d.ToList() : new List<string>(),
        };
    }

    /// <summary>
    /// Rebuilds a full document from structured front-matter fields plus a body (UI-TODO-001 save path).
    /// </summary>
    /// <param name="fm">The metadata form values.</param>
    /// <param name="body">The body markdown (from the raw editor).</param>
    /// <returns>The composed document with a YAML front-matter block.</returns>
    public static string ComposeDocument(TodoFrontMatter fm, string body)
    {
        ArgumentNullException.ThrowIfNull(fm);
        var lines = new List<string>
        {
            "---",
            $"id: {fm.Id}",
            $"section: {fm.Section}",
            $"priority: {fm.Priority}",
        };
        if (fm.Done) lines.Add("done: true");
        lines.Add(string.IsNullOrEmpty(fm.Estimate) ? "estimate: " : $"estimate: {YamlScalar(fm.Estimate!)}");
        lines.Add(string.IsNullOrEmpty(fm.Phase) ? "phase: " : $"phase: {YamlScalar(fm.Phase!)}");
        if (fm.DependsOn.Count > 0)
        {
            lines.Add("depends-on:");
            foreach (var d in fm.DependsOn) lines.Add($"  - {d}");
        }
        else
        {
            lines.Add("depends-on: []");
        }

        lines.Add("---");
        var bodyText = (body ?? string.Empty).TrimStart('\r', '\n');
        return string.Join("\n", lines) + "\n\n" + bodyText;
    }

    /// <summary>Extract the todo ID from YAML front matter.</summary>
    public static string? ExtractId(string markdown)
    {
        var (fm, _) = SplitFrontMatter(markdown);
        foreach (var line in fm)
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line.Substring(0, colon).Trim().ToLowerInvariant();
            if (key == "id") return line.Substring(colon + 1).Trim();
        }
        return null;
    }

    private static (List<string> frontMatter, List<string> body) SplitFrontMatter(string doc)
    {
        var lines = doc.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        int start = -1, end = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                if (start < 0) start = i;
                else { end = i; break; }
            }
        }
        if (start < 0 || end < 0)
            return (new List<string>(), lines.ToList());
        return (lines.Skip(start + 1).Take(end - start - 1).ToList(), lines.Skip(end + 1).ToList());
    }

    private static string YamlScalar(string s)
    {
        if (Regex.IsMatch(s, @"[:#\[\]{}&*!|>'""% @`]") || s.Contains("\n"))
            return $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        return s;
    }

    private static void AssignScalarField(McpTodoUpdateRequest req, string key, string value)
    {
        switch (key)
        {
            case "section": req.Section = value; break;
            case "priority": req.Priority = value; break;
            case "done": req.Done = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase); break;
            case "estimate": req.Estimate = value; break;
            case "note": req.Note = value; break;
            case "completed": req.CompletedDate = value; break;
            case "done-summary": req.DoneSummary = value; break;
            case "remaining": req.Remaining = value; break;
            case "phase": req.Phase = value; break;
        }
    }

    private static void AssignListField(McpTodoUpdateRequest req, string key, List<string> items)
    {
        switch (key)
        {
            case "depends-on": req.DependsOn = items; break;
            case "functional-requirements": req.FunctionalRequirements = items; break;
            case "technical-requirements": req.TechnicalRequirements = items; break;
        }
    }
}

/// <summary>
/// Structured TODO front-matter fields surfaced to the metadata form (UI-TODO-001,
/// FR-TODO-METAFORM-001). Immutable; composed back into a document via
/// <see cref="TodoMarkdown.ComposeDocument"/>.
/// </summary>
public sealed record TodoFrontMatter
{
    /// <summary>TODO id.</summary>
    public string Id { get; init; } = "NEW-TODO";

    /// <summary>Section label.</summary>
    public string Section { get; init; } = string.Empty;

    /// <summary>Priority (high/medium/low).</summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>Optional estimate.</summary>
    public string? Estimate { get; init; }

    /// <summary>Optional phase.</summary>
    public string? Phase { get; init; }

    /// <summary>Done flag.</summary>
    public bool Done { get; init; }

    /// <summary>Dependency ids.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = new List<string>();
}
