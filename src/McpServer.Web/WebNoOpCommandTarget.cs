using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using McpServer.UI.Core.Commands;
using McpServer.UI.Core.Models;
using McpServer.UI.Core.Models.Json;
using McpServer.UI.Core.Services;

namespace McpServer.Web;

/// <summary>
/// Minimal command-target implementation for hosts that do not provide the desktop shell command surface.
/// </summary>
internal sealed class WebNoOpCommandTarget : ICommandTarget
{
    public string StatusMessage { get; set; } = string.Empty;

    public McpSessionLogService McpSessionService
        => throw new NotSupportedException("Session data commands are not supported in the Web host.");

    public JsonLogSummary JsonLogSummary { get; set; } = new();

    public ObservableCollection<JsonTreeNode> JsonTree { get; } = new();

    public string AgentFilter { get; set; } = string.Empty;

    public void NavigateBack()
    {
    }

    public void NavigateForward()
    {
    }

    public Task RefreshAsync() => Task.CompletedTask;

    public void PhoneNavigateSection(string? sectionKey)
    {
    }

    public void GenerateAndNavigate(FileNode? node)
    {
    }

    public void OpenTreeItem(FileNode? node)
    {
    }

    public void TreeItemTapped(FileNode? node)
    {
    }

    public void JsonNodeDoubleTapped(JsonTreeNode? node)
    {
    }

    public void ShowRequestDetails(SearchableEntry entry)
    {
    }

    public void CloseRequestDetails()
    {
    }

    public void NavigateToPreviousRequest()
    {
    }

    public void NavigateToNextRequest()
    {
    }

    public void SelectSearchEntry(SearchableEntry entry)
    {
    }

    public void SearchRowTapped(SearchableEntry? entry)
    {
    }

    public void SearchRowDoubleTapped(SearchableEntry? entry)
    {
    }

    public void OpenPreviewInBrowser()
    {
    }

    public void ToggleShowRawMarkdown()
    {
    }

    public void Archive()
    {
    }

    public void ArchiveTreeItem(FileNode? node)
    {
    }

    public Task ReloadFromMcpAsync() => Task.CompletedTask;

    public void BuildUnifiedSummaryAndIndex(UnifiedSessionLog session, JsonLogSummary summary)
    {
    }

    public void BuildJsonTree(JsonNode? node, JsonTreeNode root, string? pathPrefix)
    {
    }

    public void LoadJson(string filePath)
    {
    }

    public void LoadMarkdownFile(FileNode node)
    {
    }

    public void LoadSourceFile(FileNode node)
    {
    }

    public void UpdateFilteredSearchEntries()
    {
    }

    public Dictionary<string, UnifiedSessionLog> BuildSessionsByPathDict(IReadOnlyList<UnifiedSessionLog> sessions) => new();

    public List<UnifiedSessionLog> OrderAndDeduplicateSessions(Dictionary<string, UnifiedSessionLog> byPath) => new();

    public void SetMcpSessionState(List<UnifiedSessionLog> sessions, Dictionary<string, UnifiedSessionLog> byPath)
    {
    }

    public Task CopyText(string text) => Task.CompletedTask;

    public Task CopyOriginalJson(UnifiedRequestEntry? entry) => Task.CompletedTask;

    public void OpenAgentConfig()
    {
    }

    public void OpenPromptTemplates()
    {
    }

    public void DispatchToUi(Action action) => action();

    public void TrackBackgroundWork(Task task)
    {
    }

    public Task CopilotStatusAsync() => Task.CompletedTask;

    public Task CopilotPlanAsync() => Task.CompletedTask;

    public Task CopilotImplementAsync() => Task.CompletedTask;
}
