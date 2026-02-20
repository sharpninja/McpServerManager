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
using RequestTracker.Core.Cqrs;
using RequestTracker.Core.Models;
using RequestTracker.Core.Models.Json;
using RequestTracker.Core.Services;
using RequestTracker.Core.ViewModels;

namespace RequestTracker.Core.Commands;

internal static class CommandLogger
{
    internal static readonly ILogger Logger = AppLogService.Instance.CreateLogger("Commands");
}

// --- Initialize from MCP (full tree build + first load) ---

public sealed class InitializeFromMcpCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public InitializeFromMcpCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class InitializeFromMcpHandler : ICommandHandler<InitializeFromMcpCommand>
{
    public Task ExecuteAsync(InitializeFromMcpCommand command, CancellationToken cancellationToken = default)
    {
        var vm = command.ViewModel;
        vm.DispatchToUi(() => vm.StatusMessage = "Loading sessions from MCP...");
        vm._mediator.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                    || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    Services.OllamaLogAgentService.TryStartOllamaIfNeeded();
                }
                await vm.ReloadFromMcpAsyncInternal().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                CommandLogger.Logger.LogError(ex, "InitializeFromMcp failed");
                vm.DispatchToUi(() => vm.StatusMessage = $"Failed to load tree: {ex.Message}");
            }
        }));
        return Task.CompletedTask;
    }
}

// --- Refresh and Load All JSON (aggregated view) ---

public sealed class RefreshAndLoadAllJsonCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public string? PreselectedAgent { get; }
    public RefreshAndLoadAllJsonCommand(MainWindowViewModel vm, string? preselectedAgent = null)
    {
        ViewModel = vm;
        PreselectedAgent = preselectedAgent;
    }
}

public sealed class RefreshAndLoadAllJsonHandler : ICommandHandler<RefreshAndLoadAllJsonCommand>
{
    public Task ExecuteAsync(RefreshAndLoadAllJsonCommand command, CancellationToken cancellationToken = default)
    {
        var vm = command.ViewModel;
        vm.DispatchToUi(() => vm.StatusMessage = "Refreshing from MCP...");
        vm._mediator.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                var sessions = await vm.McpSessionService.GetAllSessionsAsync(cancellationToken).ConfigureAwait(true);
                var byPath = vm.BuildSessionsByPathDict(sessions);
                var uniqueSessions = vm.OrderAndDeduplicateSessions(byPath);

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
                vm.BuildUnifiedSummaryAndIndexInternal(masterLog, summary);
                summary.SummaryLines.Clear();
                summary.SummaryLines.Add($"Type: {masterLog.SourceType}");
                summary.SummaryLines.Add($"Total Entries: {masterLog.EntryCount}");
                summary.SummaryLines.Add($"Total Tokens: {masterLog.TotalTokens:N0}");
                summary.SummaryLines.Add($"Aggregated at: {masterLog.Started}");

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
                var unifiedNode = JsonSerializer.SerializeToNode(masterLog, options);
                var root = new JsonTreeNode("Root", "Aggregated Unified Log", "Object") { IsExpanded = true };
                vm.BuildJsonTreeInternal(unifiedNode, root, null);

                var reqCount = deduped.Count;
                var sessionCount = uniqueSessions.Count;
                var preselectedAgent = command.PreselectedAgent;

                vm.DispatchToUi(() =>
                {
                    try
                    {
                        vm.SetMcpSessionState(uniqueSessions, byPath);
                        vm.JsonLogSummary = summary;
                        vm.JsonTree.Clear();
                        vm.JsonTree.Add(root);
                        vm.UpdateFilteredSearchEntriesInternal();
                        vm.AgentFilter = string.IsNullOrWhiteSpace(preselectedAgent) ? "" : preselectedAgent.Trim();
                        vm.StatusMessage = string.IsNullOrWhiteSpace(preselectedAgent)
                            ? $"Loaded {reqCount} requests from {sessionCount} sessions."
                            : $"Loaded {reqCount} requests from {sessionCount} sessions. Filtered by agent: {vm.AgentFilter}.";
                    }
                    catch (Exception ex)
                    {
                        vm.StatusMessage = $"Error building UI: {ex.Message}";
                        CommandLogger.Logger.LogError(ex, "UI Build Error");
                    }
                });
            }
            catch (Exception ex)
            {
                vm.DispatchToUi(() => vm.StatusMessage = $"Error aggregating MCP sessions: {ex.Message}");
                CommandLogger.Logger.LogError(ex, "Aggregation Error");
            }
        }));
        return Task.CompletedTask;
    }
}

// --- Refresh and Load Agent JSON (filtered by agent) ---

public sealed class RefreshAndLoadAgentJsonCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public string AgentName { get; }
    public RefreshAndLoadAgentJsonCommand(MainWindowViewModel vm, string agentName)
    {
        ViewModel = vm;
        AgentName = agentName;
    }
}

public sealed class RefreshAndLoadAgentJsonHandler : ICommandHandler<RefreshAndLoadAgentJsonCommand>
{
    public Task ExecuteAsync(RefreshAndLoadAgentJsonCommand command, CancellationToken cancellationToken = default)
    {
        // Reuse the AllJson handler with a preselected agent filter
        var inner = new RefreshAndLoadAllJsonCommand(command.ViewModel, command.AgentName);
        return new RefreshAndLoadAllJsonHandler().ExecuteAsync(inner, cancellationToken);
    }
}

// --- Refresh and Load Single Session ---

public sealed class RefreshAndLoadSessionCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public string VirtualPath { get; }
    public RefreshAndLoadSessionCommand(MainWindowViewModel vm, string virtualPath)
    {
        ViewModel = vm;
        VirtualPath = virtualPath;
    }
}

public sealed class RefreshAndLoadSessionHandler : ICommandHandler<RefreshAndLoadSessionCommand>
{
    public Task ExecuteAsync(RefreshAndLoadSessionCommand command, CancellationToken cancellationToken = default)
    {
        var vm = command.ViewModel;
        vm.DispatchToUi(() => vm.StatusMessage = "Refreshing from MCP...");
        vm._mediator.TrackBackgroundWork(Task.Run(async () =>
        {
            try
            {
                var sessions = await vm.McpSessionService.GetAllSessionsAsync(cancellationToken).ConfigureAwait(true);
                var byPath = vm.BuildSessionsByPathDict(sessions);
                var uniqueSessions = vm.OrderAndDeduplicateSessions(byPath);

                if (!byPath.TryGetValue(command.VirtualPath, out var session))
                {
                    vm.DispatchToUi(() => vm.StatusMessage = "Session not found. Click Refresh.");
                    return;
                }

                vm.DispatchToUi(() => vm.StatusMessage = $"Loading {session.SourceType}/{session.SessionId}...");

                var summary = new JsonLogSummary();
                vm.BuildUnifiedSummaryAndIndexInternal(session, summary);
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
                vm.BuildJsonTreeInternal(unifiedNode, root, null);

                vm.DispatchToUi(() =>
                {
                    vm.SetMcpSessionState(uniqueSessions, byPath);
                    vm.JsonTree.Clear();
                    vm.SearchableEntries.Clear();
                    vm.JsonLogSummary = summary;
                    vm.JsonTree.Add(root);
                    vm.UpdateFilteredSearchEntriesInternal();
                    vm.StatusMessage = $"Loaded {session.SourceType}/{session.SessionId}";
                });
            }
            catch (Exception ex)
            {
                vm.DispatchToUi(() => vm.StatusMessage = $"Error loading session: {ex.Message}");
            }
        }));
        return Task.CompletedTask;
    }
}

// --- Load JSON File (local file) ---

public sealed class LoadJsonFileCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public string FilePath { get; }
    public LoadJsonFileCommand(MainWindowViewModel vm, string filePath)
    {
        ViewModel = vm;
        FilePath = filePath;
    }
}

public sealed class LoadJsonFileHandler : ICommandHandler<LoadJsonFileCommand>
{
    public Task ExecuteAsync(LoadJsonFileCommand command, CancellationToken cancellationToken = default)
    {
        var vm = command.ViewModel;
        vm.DispatchToUi(() => vm.StatusMessage = "Loading JSON...");
        vm.LoadJsonInternal(command.FilePath);
        return Task.CompletedTask;
    }
}

// --- Navigate to Node (GenerateAndNavigate) ---

public sealed class NavigateToNodeCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public FileNode? Node { get; }
    public NavigateToNodeCommand(MainWindowViewModel vm, FileNode? node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class NavigateToNodeHandler : ICommandHandler<NavigateToNodeCommand>
{
    public Task ExecuteAsync(NavigateToNodeCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.GenerateAndNavigateInternal(command.Node);
        return Task.CompletedTask;
    }
}

// --- Archive Current File ---

public sealed class ArchiveCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public ArchiveCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class ArchiveHandler : ICommandHandler<ArchiveCommand>
{
    public Task ExecuteAsync(ArchiveCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.ArchiveInternal();
        return Task.CompletedTask;
    }
}

// --- Refresh View ---

public sealed class RefreshCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public RefreshCommand(MainWindowViewModel vm) => ViewModel = vm;
}

public sealed class RefreshHandler : ICommandHandler<RefreshCommand>
{
    public Task ExecuteAsync(RefreshCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.RefreshInternal();
        return Task.CompletedTask;
    }
}

// --- Load Markdown File ---

public sealed class LoadMarkdownFileCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public FileNode Node { get; }
    public LoadMarkdownFileCommand(MainWindowViewModel vm, FileNode node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class LoadMarkdownFileHandler : ICommandHandler<LoadMarkdownFileCommand>
{
    public Task ExecuteAsync(LoadMarkdownFileCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.LoadMarkdownFileInternal(command.Node);
        return Task.CompletedTask;
    }
}

// --- Load Source File ---

public sealed class LoadSourceFileCommand : ICommand
{
    public MainWindowViewModel ViewModel { get; }
    public FileNode Node { get; }
    public LoadSourceFileCommand(MainWindowViewModel vm, FileNode node)
    {
        ViewModel = vm;
        Node = node;
    }
}

public sealed class LoadSourceFileHandler : ICommandHandler<LoadSourceFileCommand>
{
    public Task ExecuteAsync(LoadSourceFileCommand command, CancellationToken cancellationToken = default)
    {
        command.ViewModel.LoadSourceFileInternal(command.Node);
        return Task.CompletedTask;
    }
}
