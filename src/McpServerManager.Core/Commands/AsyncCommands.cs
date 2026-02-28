using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using McpServerManager.Core.Cqrs;
using McpServerManager.Core.Models;
using McpServerManager.Core.Models.Json;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;

namespace McpServerManager.Core.Commands;

internal static class CommandLogger
{
    internal static readonly ILogger Logger = AppLogService.Instance.CreateLogger("Commands");
}

// --- Initialize from MCP (full tree build + first load) ---

public sealed class InitializeFromMcpCommand : ICommand
{
    public ICommandTarget Target { get; }
    public InitializeFromMcpCommand(ICommandTarget target) => Target = target;
}

public sealed class InitializeFromMcpHandler : ICommandHandler<InitializeFromMcpCommand>
{
    public Task ExecuteAsync(InitializeFromMcpCommand command, CancellationToken cancellationToken = default)
    {
        var t = command.Target;
        t.DispatchToUi(() => t.StatusMessage = "Loading sessions from MCP...");
        t.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    Services.OllamaLogAgentService.TryStartOllamaIfNeeded();
                }
                await t.ReloadFromMcpAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                CommandLogger.Logger.LogError(ex, "InitializeFromMcp failed");
                t.DispatchToUi(() => t.StatusMessage = $"Failed to load tree: {ex.Message}");
            }
        }));
        return Task.CompletedTask;
    }
}

// --- Refresh and Load All JSON (aggregated view) ---

public sealed class RefreshAndLoadAllJsonCommand : ICommand
{
    public ICommandTarget Target { get; }
    public string? PreselectedAgent { get; }
    /// <summary>Pre-fetched sessions to avoid a redundant MCP round-trip (e.g. on startup).</summary>
    public IReadOnlyList<UnifiedSessionLog>? CachedSessions { get; init; }
    public RefreshAndLoadAllJsonCommand(ICommandTarget target, string? preselectedAgent = null)
    {
        Target = target;
        PreselectedAgent = preselectedAgent;
    }
}

public sealed class RefreshAndLoadAllJsonHandler : ICommandHandler<RefreshAndLoadAllJsonCommand>
{
    public Task ExecuteAsync(RefreshAndLoadAllJsonCommand command, CancellationToken cancellationToken = default)
    {
        var t = command.Target;
        t.DispatchToUi(() => t.StatusMessage = "Refreshing from MCP...");
        t.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                IReadOnlyList<UnifiedSessionLog> sessions;
                Dictionary<string, UnifiedSessionLog> byPath;
                List<UnifiedSessionLog> uniqueSessions;

                if (command.CachedSessions is { Count: > 0 })
                {
                    // Reuse sessions already fetched by ReloadFromMcpAsync to
                    // avoid a redundant MCP round-trip on startup / tree rebuild.
                    uniqueSessions = command.CachedSessions.ToList();
                    byPath = t.BuildSessionsByPathDict(uniqueSessions);
                }
                else
                {
                    sessions = await t.McpSessionService.GetAllSessionsAsync(cancellationToken).ConfigureAwait(true);
                    byPath = t.BuildSessionsByPathDict(sessions);
                    uniqueSessions = t.OrderAndDeduplicateSessions(byPath);
                }

                // Stamp agent on entries
                foreach (var log in uniqueSessions)
                {
                    var agent = string.IsNullOrWhiteSpace(log.SourceType) ? "Unknown" : log.SourceType.Trim();
                    if (log.Entries == null) continue;
                    foreach (var entry in log.Entries)
                    {
                        if (entry != null && string.IsNullOrWhiteSpace(entry.Agent))
                            entry.Agent = agent;
                    }
                }

                var allEntries = uniqueSessions.SelectMany(l => l.Entries).OrderByDescending(e => e.Timestamp).ToList();
                var deduped = MainWindowViewModel.DeduplicateUnifiedEntries(allEntries);

                var masterLog = new UnifiedSessionLog
                {
                    SourceType = "Aggregated",
                    SessionId = "ALL-JSON",
                    Title = "All Requests",
                    Model = "Various",
                    Started = DateTime.Now,
                    Status = "Aggregated",
                    EntryCount = deduped.Count,
                    Entries = deduped,
                    TotalTokens = uniqueSessions.Sum(l => l.TotalTokens)
                };

                var summary = new JsonLogSummary();
                t.BuildUnifiedSummaryAndIndex(masterLog, summary);
                summary.SummaryLines.Clear();
                summary.SummaryLines.Add($"Type: {masterLog.SourceType}");
                summary.SummaryLines.Add($"Total Entries: {masterLog.EntryCount}");
                summary.SummaryLines.Add($"Total Tokens: {masterLog.TotalTokens:N0}");
                summary.SummaryLines.Add($"Aggregated at: {masterLog.Started}");

                var root = new JsonTreeNode("Root", "Aggregated Unified Log", "Object") { IsExpanded = true };
                // Lightweight summary tree instead of serializing the entire master log
                // (which creates thousands of JsonTreeNodes and causes ANR on Android).
                root.Children.Add(new JsonTreeNode("sourceType", masterLog.SourceType ?? "Aggregated", "String"));
                root.Children.Add(new JsonTreeNode("sessionId", masterLog.SessionId ?? "ALL-JSON", "String"));
                root.Children.Add(new JsonTreeNode("title", masterLog.Title ?? "All Requests", "String"));
                root.Children.Add(new JsonTreeNode("entryCount", masterLog.EntryCount.ToString(), "Number"));
                root.Children.Add(new JsonTreeNode("totalTokens", $"{masterLog.TotalTokens:N0}", "Number"));
                root.Children.Add(new JsonTreeNode("sessions", $"{uniqueSessions.Count} sessions", "Number"));

                var reqCount = deduped.Count;
                var sessionCount = uniqueSessions.Count;
                var preselectedAgent = command.PreselectedAgent;

                t.DispatchToUi(() =>
                {
                    try
                    {
                        t.SetMcpSessionState(uniqueSessions, byPath);
                        t.JsonLogSummary = summary;
                        t.JsonTree.Clear();
                        t.JsonTree.Add(root);
                        t.UpdateFilteredSearchEntries();
                        t.AgentFilter = string.IsNullOrWhiteSpace(preselectedAgent) ? "" : preselectedAgent.Trim();
                        t.StatusMessage = string.IsNullOrWhiteSpace(preselectedAgent)
                            ? $"Loaded {reqCount} requests from {sessionCount} sessions."
                            : $"Loaded {reqCount} requests from {sessionCount} sessions. Filtered by agent: {t.AgentFilter}.";
                    }
                    catch (Exception ex)
                    {
                        t.StatusMessage = $"Error building UI: {ex.Message}";
                        CommandLogger.Logger.LogError(ex, "UI Build Error");
                    }
                });
            }
            catch (Exception ex)
            {
                t.DispatchToUi(() => t.StatusMessage = $"Error aggregating MCP sessions: {ex.Message}");
                CommandLogger.Logger.LogError(ex, "Aggregation Error");
            }
        }));
        return Task.CompletedTask;
    }
}

// --- Refresh and Load Agent JSON (filtered by agent) ---

public sealed class RefreshAndLoadAgentJsonCommand : ICommand
{
    public ICommandTarget Target { get; }
    public string AgentName { get; }
    public RefreshAndLoadAgentJsonCommand(ICommandTarget target, string agentName)
    {
        Target = target;
        AgentName = agentName;
    }
}

public sealed class RefreshAndLoadAgentJsonHandler : ICommandHandler<RefreshAndLoadAgentJsonCommand>
{
    public Task ExecuteAsync(RefreshAndLoadAgentJsonCommand command, CancellationToken cancellationToken = default)
    {
        // Reuse the AllJson handler with a preselected agent filter
        var inner = new RefreshAndLoadAllJsonCommand(command.Target, command.AgentName);
        return new RefreshAndLoadAllJsonHandler().ExecuteAsync(inner, cancellationToken);
    }
}

// --- Refresh and Load Single Session ---

public sealed class RefreshAndLoadSessionCommand : ICommand
{
    public ICommandTarget Target { get; }
    public string VirtualPath { get; }
    public RefreshAndLoadSessionCommand(ICommandTarget target, string virtualPath)
    {
        Target = target;
        VirtualPath = virtualPath;
    }
}

public sealed class RefreshAndLoadSessionHandler : ICommandHandler<RefreshAndLoadSessionCommand>
{
    public Task ExecuteAsync(RefreshAndLoadSessionCommand command, CancellationToken cancellationToken = default)
    {
        var t = command.Target;
        t.DispatchToUi(() => t.StatusMessage = "Refreshing from MCP...");
        t.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                var sessions = await t.McpSessionService.GetAllSessionsAsync(cancellationToken).ConfigureAwait(true);
                var byPath = t.BuildSessionsByPathDict(sessions);
                var uniqueSessions = t.OrderAndDeduplicateSessions(byPath);

                if (!byPath.TryGetValue(command.VirtualPath, out var session))
                {
                    t.DispatchToUi(() => t.StatusMessage = "Session not found. Click Refresh.");
                    return;
                }

                t.DispatchToUi(() => t.StatusMessage = $"Loading {session.SourceType}/{session.SessionId}...");

                var summary = new JsonLogSummary();
                t.BuildUnifiedSummaryAndIndex(session, summary);
                summary.SummaryLines.Clear();
                summary.SummaryLines.Add($"Type: {session.SourceType}");
                summary.SummaryLines.Add($"Session: {session.SessionId}");
                summary.SummaryLines.Add($"Entries: {session.EntryCount}");
                if (!string.IsNullOrEmpty(session.Model))
                    summary.SummaryLines.Add($"Model: {session.Model}");
                if (session.LastUpdated.HasValue)
                    summary.SummaryLines.Add($"Last Updated: {session.LastUpdated}");

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
                var unifiedNode = JsonSerializer.SerializeToNode(session, options);
                var root = new JsonTreeNode("Root", $"{session.SourceType} (Unified)", "Object") { IsExpanded = true };
                t.BuildJsonTree(unifiedNode, root, null);

                t.DispatchToUi(() =>
                {
                    t.SetMcpSessionState(uniqueSessions, byPath);
                    t.JsonTree.Clear();
                    t.JsonLogSummary = summary;
                    t.JsonTree.Add(root);
                    t.UpdateFilteredSearchEntries();
                    t.StatusMessage = $"Loaded {session.SourceType}/{session.SessionId}";
                });
            }
            catch (Exception ex)
            {
                t.DispatchToUi(() => t.StatusMessage = $"Error loading session: {ex.Message}");
            }
        }));
        return Task.CompletedTask;
    }
}

// --- Load JSON File (local file) ---

public sealed class LoadJsonFileCommand : ICommand
{
    public ICommandTarget Target { get; }
    public string FilePath { get; }
    public LoadJsonFileCommand(ICommandTarget target, string filePath)
    {
        Target = target;
        FilePath = filePath;
    }
}

public sealed class LoadJsonFileHandler : ICommandHandler<LoadJsonFileCommand>
{
    public Task ExecuteAsync(LoadJsonFileCommand command, CancellationToken cancellationToken = default)
    {
        var t = command.Target;
        t.DispatchToUi(() => t.StatusMessage = "Loading JSON...");
        t.LoadJson(command.FilePath);
        return Task.CompletedTask;
    }
}

// --- Navigate to Node (GenerateAndNavigate) ---

public sealed class NavigateToNodeCommand : ICommand
{
    public ICommandTarget Target { get; }
    public FileNode? Node { get; }
    public NavigateToNodeCommand(ICommandTarget target, FileNode? node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class NavigateToNodeHandler : ICommandHandler<NavigateToNodeCommand>
{
    public Task ExecuteAsync(NavigateToNodeCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.GenerateAndNavigate(command.Node);
        return Task.CompletedTask;
    }
}

// --- Archive Current File ---

public sealed class ArchiveCommand : ICommand
{
    public ICommandTarget Target { get; }
    public ArchiveCommand(ICommandTarget target) => Target = target;
}

public sealed class ArchiveHandler : ICommandHandler<ArchiveCommand>
{
    public Task ExecuteAsync(ArchiveCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.Archive();
        return Task.CompletedTask;
    }
}

// --- Refresh View ---

public sealed class RefreshCommand : ICommand
{
    public ICommandTarget Target { get; }
    public RefreshCommand(ICommandTarget target) => Target = target;
}

public sealed class RefreshHandler : ICommandHandler<RefreshCommand>
{
    public async Task ExecuteAsync(RefreshCommand command, CancellationToken cancellationToken = default)
    {
        await command.Target.RefreshAsync();
    }
}

// --- Load Markdown File ---

public sealed class LoadMarkdownFileCommand : ICommand
{
    public ICommandTarget Target { get; }
    public FileNode Node { get; }
    public LoadMarkdownFileCommand(ICommandTarget target, FileNode node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class LoadMarkdownFileHandler : ICommandHandler<LoadMarkdownFileCommand>
{
    public Task ExecuteAsync(LoadMarkdownFileCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.LoadMarkdownFile(command.Node);
        return Task.CompletedTask;
    }
}

// --- Load Source File ---

public sealed class LoadSourceFileCommand : ICommand
{
    public ICommandTarget Target { get; }
    public FileNode Node { get; }
    public LoadSourceFileCommand(ICommandTarget target, FileNode node)
    {
        Target = target;
        Node = node;
    }
}

public sealed class LoadSourceFileHandler : ICommandHandler<LoadSourceFileCommand>
{
    public Task ExecuteAsync(LoadSourceFileCommand command, CancellationToken cancellationToken = default)
    {
        command.Target.LoadSourceFile(command.Node);
        return Task.CompletedTask;
    }
}
