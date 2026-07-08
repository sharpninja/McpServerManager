using System.Linq;
using McpServerManager.Core.Services;
using Xunit;

namespace McpServerManager.Core.Tests.Services;

/// <summary>
/// UI-TODO-001 / FR-TODO-METAFORM-001 + TR-TODO-EDITOR-BODYONLY-001: verifies the front-matter/body
/// split and recompose helpers that let the metadata form own the YAML front matter while the raw
/// editor shows body sections only. Fixtures are the blank template and composed documents.
/// </summary>
public sealed class TodoMarkdownFrontMatterTests
{
    /// <summary>ExtractBody drops the YAML front matter, keeping the body from the title onward.</summary>
    [Fact]
    public void ExtractBody_DropsFrontMatter()
    {
        var body = TodoMarkdown.ExtractBody(TodoMarkdown.BlankTemplate());
        Assert.DoesNotContain("id: NEW-TODO", body);
        Assert.DoesNotContain("depends-on", body);
        Assert.DoesNotContain("---", body);
        Assert.Contains("#", body);
        Assert.Contains("Implementation Tasks", body);
    }

    /// <summary>ParseFrontMatter reads the structured metadata fields from the document.</summary>
    [Fact]
    public void ParseFrontMatter_ReadsFields()
    {
        var fm = TodoMarkdown.ParseFrontMatter(TodoMarkdown.BlankTemplate());
        Assert.Equal("NEW-TODO", fm.Id);
        Assert.Equal("mvp-app", fm.Section);
        Assert.Equal("low", fm.Priority);
    }

    /// <summary>ComposeDocument rebuilds a document whose front matter and body round-trip.</summary>
    [Fact]
    public void ComposeDocument_RoundTrips()
    {
        var fm = new TodoFrontMatter
        {
            Id = "MCP-API-001",
            Section = "backend",
            Priority = "high",
            Estimate = "2h",
            Phase = "construction",
            DependsOn = new[] { "MCP-API-000", "MCP-API-002" },
        };
        const string body = "# Title\n\nBody line.\n\n## Implementation Tasks\n\n- [ ] do it\n";

        var doc = TodoMarkdown.ComposeDocument(fm, body);

        var parsed = TodoMarkdown.ParseFrontMatter(doc);
        Assert.Equal("MCP-API-001", parsed.Id);
        Assert.Equal("backend", parsed.Section);
        Assert.Equal("high", parsed.Priority);
        Assert.Equal("2h", parsed.Estimate);
        Assert.Equal("construction", parsed.Phase);
        Assert.Equal(new[] { "MCP-API-000", "MCP-API-002" }, parsed.DependsOn.ToArray());

        var extracted = TodoMarkdown.ExtractBody(doc);
        Assert.Contains("Body line.", extracted);
        Assert.DoesNotContain("id: MCP-API-001", extracted);
    }

    /// <summary>A document with no front matter yields the whole text as body and default fields.</summary>
    [Fact]
    public void ExtractBody_NoFrontMatter_ReturnsWhole()
    {
        const string doc = "# Just a title\n\nNo front matter here.";
        Assert.Contains("No front matter here.", TodoMarkdown.ExtractBody(doc));
    }
}
