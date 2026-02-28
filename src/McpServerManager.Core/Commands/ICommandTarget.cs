using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using McpServerManager.Core.Models;
using McpServerManager.Core.Models.Json;
using McpServerManager.Core.Services;

namespace McpServerManager.Core.Commands;

/// <summary>
/// Abstraction for ViewModel operations invoked by CQRS command handlers.
/// Decouples command/handler definitions from concrete ViewModel types.
/// </summary>
public interface ICommandTarget
{
    // Navigation
    void NavigateBack();
    void NavigateForward();
    Task RefreshAsync();
    void PhoneNavigateSection(string? sectionKey);

    // Request details
    void ShowRequestDetails(SearchableEntry entry);
    void CloseRequestDetails();
    void NavigateToPreviousRequest();
    void NavigateToNextRequest();

    // Selection
    void SelectSearchEntry(SearchableEntry entry);

    // Clipboard
    Task CopyText(string text);
    Task CopyOriginalJson(UnifiedRequestEntry? entry);

    // Preview / Markdown
    void OpenPreviewInBrowser();
    void ToggleShowRawMarkdown();

    // Archive
    void Archive();
    void ArchiveTreeItem(FileNode? node);

    // Tree
    void OpenTreeItem(FileNode? node);
    void TreeItemTapped(FileNode? node);
    void JsonNodeDoubleTapped(JsonTreeNode? node);

    // Search
    void SearchRowTapped(SearchableEntry? entry);
    void SearchRowDoubleTapped(SearchableEntry? entry);

    // Config
    void OpenAgentConfig();
    void OpenPromptTemplates();

    // Async / data operations
    Task ReloadFromMcpAsync();
    void BuildUnifiedSummaryAndIndex(UnifiedSessionLog session, JsonLogSummary summary);
    void BuildJsonTree(JsonNode? node, JsonTreeNode root, string? pathPrefix);
    void LoadJson(string filePath);
    void LoadMarkdownFile(FileNode node);
    void LoadSourceFile(FileNode node);
    void UpdateFilteredSearchEntries();
    void GenerateAndNavigate(FileNode? node);

    // Properties needed by async command handlers
    string StatusMessage { get; set; }
    void DispatchToUi(Action action);
    void TrackBackgroundWork(Task task);
    McpSessionLogService McpSessionService { get; }
    JsonLogSummary JsonLogSummary { get; set; }
    ObservableCollection<JsonTreeNode> JsonTree { get; }
    string AgentFilter { get; set; }

    // Session helpers
    Dictionary<string, UnifiedSessionLog> BuildSessionsByPathDict(IReadOnlyList<UnifiedSessionLog> sessions);
    List<UnifiedSessionLog> OrderAndDeduplicateSessions(Dictionary<string, UnifiedSessionLog> byPath);
    void SetMcpSessionState(List<UnifiedSessionLog> sessions, Dictionary<string, UnifiedSessionLog> byPath);
}
