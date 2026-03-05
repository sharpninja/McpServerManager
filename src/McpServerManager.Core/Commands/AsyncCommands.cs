using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using McpServer.Cqrs;
using Microsoft.Extensions.Logging;
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

public sealed record InitializeFromMcpCommand() : ICommand<bool>;

public sealed class InitializeFromMcpHandler(ICommandTarget target) : ICommandHandler<InitializeFromMcpCommand, bool>
{
    public Task<Result<bool>> HandleAsync(InitializeFromMcpCommand command, CallContext context)
    {
        target.DispatchToUi(() => target.StatusMessage = "Loading sessions from MCP...");
        target.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    Services.OllamaLogAgentService.TryStartOllamaIfNeeded();
                }
                await target.ReloadFromMcpAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                CommandLogger.Logger.LogError(ex, "InitializeFromMcp failed");
                target.DispatchToUi(() => target.StatusMessage = $"Failed to load tree: {ex.Message}");
            }
        }));
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Refresh and Load All JSON (aggregated view) ---

public sealed record RefreshAndLoadAllJsonCommand(string? PreselectedAgent = null) : ICommand<bool>
{
    /// <summary>Pre-fetched sessions to avoid a redundant MCP round-trip (e.g. on startup).</summary>
    public IReadOnlyList<UnifiedSessionLog>? CachedSessions { get; init; }
}

public sealed class RefreshAndLoadAllJsonHandler(ICommandTarget target) : ICommandHandler<RefreshAndLoadAllJsonCommand, bool>
{
    public Task<Result<bool>> HandleAsync(RefreshAndLoadAllJsonCommand command, CallContext context)
    {
        target.DispatchToUi(() => target.StatusMessage = "Refreshing from MCP...");
        target.TrackBackgroundWork(Task.Run(async () =>
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
                    byPath = target.BuildSessionsByPathDict(uniqueSessions);
                }
                else
                {
                    sessions = await target.McpSessionService.GetAllSessionsAsync(context.CancellationToken).ConfigureAwait(true);
                    byPath = target.BuildSessionsByPathDict(sessions);
                    uniqueSessions = target.OrderAndDeduplicateSessions(byPath);
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
                target.BuildUnifiedSummaryAndIndex(masterLog, summary);
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

                target.DispatchToUi(() =>
                {
                    try
                    {
                        target.SetMcpSessionState(uniqueSessions, byPath);
                        target.JsonLogSummary = summary;
                        target.JsonTree.Clear();
                        target.JsonTree.Add(root);
                        target.UpdateFilteredSearchEntries();
                        target.AgentFilter = string.IsNullOrWhiteSpace(preselectedAgent) ? "" : preselectedAgent.Trim();
                        target.StatusMessage = string.IsNullOrWhiteSpace(preselectedAgent)
                            ? $"Loaded {reqCount} requests from {sessionCount} sessions."
                            : $"Loaded {reqCount} requests from {sessionCount} sessions. Filtered by agent: {target.AgentFilter}.";
                    }
                    catch (Exception ex)
                    {
                        target.StatusMessage = $"Error building UI: {ex.Message}";
                        CommandLogger.Logger.LogError(ex, "UI Build Error");
                    }
                });
            }
            catch (Exception ex)
            {
                target.DispatchToUi(() => target.StatusMessage = $"Error aggregating MCP sessions: {ex.Message}");
                CommandLogger.Logger.LogError(ex, "Aggregation Error");
            }
        }));
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Refresh and Load Agent JSON (filtered by agent) ---

public sealed record RefreshAndLoadAgentJsonCommand(string AgentName) : ICommand<bool>;

public sealed class RefreshAndLoadAgentJsonHandler(ICommandTarget target) : ICommandHandler<RefreshAndLoadAgentJsonCommand, bool>
{
    public Task<Result<bool>> HandleAsync(RefreshAndLoadAgentJsonCommand command, CallContext context)
    {
        // Reuse the AllJson handler with a preselected agent filter
        var inner = new RefreshAndLoadAllJsonCommand(command.AgentName);
        return new RefreshAndLoadAllJsonHandler(target).HandleAsync(inner, context);
    }
}

// --- Refresh and Load Single Session ---

public sealed record RefreshAndLoadSessionCommand(string VirtualPath) : ICommand<bool>;

public sealed class RefreshAndLoadSessionHandler(ICommandTarget target) : ICommandHandler<RefreshAndLoadSessionCommand, bool>
{
    public Task<Result<bool>> HandleAsync(RefreshAndLoadSessionCommand command, CallContext context)
    {
        target.DispatchToUi(() => target.StatusMessage = "Refreshing from MCP...");
        target.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                var sessions = await target.McpSessionService.GetAllSessionsAsync(context.CancellationToken).ConfigureAwait(true);
                var byPath = target.BuildSessionsByPathDict(sessions);
                var uniqueSessions = target.OrderAndDeduplicateSessions(byPath);

                if (!byPath.TryGetValue(command.VirtualPath, out var session))
                {
                    target.DispatchToUi(() => target.StatusMessage = "Session not found. Click Refresh.");
                    return;
                }

                target.DispatchToUi(() => target.StatusMessage = $"Loading {session.SourceType}/{session.SessionId}...");

                var summary = new JsonLogSummary();
                target.BuildUnifiedSummaryAndIndex(session, summary);
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
                target.BuildJsonTree(unifiedNode, root, null);

                target.DispatchToUi(() =>
                {
                    target.SetMcpSessionState(uniqueSessions, byPath);
                    target.JsonTree.Clear();
                    target.JsonLogSummary = summary;
                    target.JsonTree.Add(root);
                    target.UpdateFilteredSearchEntries();
                    target.StatusMessage = $"Loaded {session.SourceType}/{session.SessionId}";
                });
            }
            catch (Exception ex)
            {
                target.DispatchToUi(() => target.StatusMessage = $"Error loading session: {ex.Message}");
            }
        }));
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Load JSON File (local file) ---

public sealed record LoadJsonFileCommand(string FilePath) : ICommand<bool>;

public sealed class LoadJsonFileHandler(ICommandTarget target) : ICommandHandler<LoadJsonFileCommand, bool>
{
    public Task<Result<bool>> HandleAsync(LoadJsonFileCommand command, CallContext context)
    {
        target.DispatchToUi(() => target.StatusMessage = "Loading JSON...");
        target.LoadJson(command.FilePath);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Navigate to Node (GenerateAndNavigate) ---

public sealed record NavigateToNodeCommand(FileNode? Node) : ICommand<bool>;

public sealed class NavigateToNodeHandler(ICommandTarget target) : ICommandHandler<NavigateToNodeCommand, bool>
{
    public Task<Result<bool>> HandleAsync(NavigateToNodeCommand command, CallContext context)
    {
        target.GenerateAndNavigate(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Archive Current File ---

public sealed record ArchiveCommand() : ICommand<bool>;

public sealed class ArchiveHandler(ICommandTarget target) : ICommandHandler<ArchiveCommand, bool>
{
    public Task<Result<bool>> HandleAsync(ArchiveCommand command, CallContext context)
    {
        target.Archive();
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Refresh View ---

public sealed record RefreshCommand() : ICommand<bool>;

public sealed class RefreshHandler(ICommandTarget target) : ICommandHandler<RefreshCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RefreshCommand command, CallContext context)
    {
        await target.RefreshAsync();
        return Result<bool>.Success(true);
    }
}

// --- Load Markdown File ---

public sealed record LoadMarkdownFileCommand(FileNode Node) : ICommand<bool>;

public sealed class LoadMarkdownFileHandler(ICommandTarget target) : ICommandHandler<LoadMarkdownFileCommand, bool>
{
    public Task<Result<bool>> HandleAsync(LoadMarkdownFileCommand command, CallContext context)
    {
        target.LoadMarkdownFile(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

// --- Load Source File ---

public sealed record LoadSourceFileCommand(FileNode Node) : ICommand<bool>;

public sealed class LoadSourceFileHandler(ICommandTarget target) : ICommandHandler<LoadSourceFileCommand, bool>
{
    public Task<Result<bool>> HandleAsync(LoadSourceFileCommand command, CallContext context)
    {
        target.LoadSourceFile(command.Node);
        return Task.FromResult(Result<bool>.Success(true));
    }
}
