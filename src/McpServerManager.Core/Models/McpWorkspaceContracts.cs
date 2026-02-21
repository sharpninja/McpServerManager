using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace McpServerManager.Core.Models;

public sealed class McpWorkspaceQueryResult
{
    [JsonPropertyName("items")]
    public List<McpWorkspaceItem> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

public sealed class McpWorkspaceItem
{
    [JsonPropertyName("workspacePath")]
    public string? WorkspacePath { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("todoPath")]
    public string? TodoPath { get; set; }

    [JsonPropertyName("workspacePort")]
    public int WorkspacePort { get; set; }

    [JsonPropertyName("tunnelProvider")]
    public string? TunnelProvider { get; set; }

    [JsonPropertyName("dateTimeCreated")]
    public DateTimeOffset? DateTimeCreated { get; set; }

    [JsonPropertyName("dateTimeModified")]
    public DateTimeOffset? DateTimeModified { get; set; }

    [JsonPropertyName("runAs")]
    public string? RunAs { get; set; }
}

public sealed class McpWorkspaceCreateRequest
{
    [JsonPropertyName("workspacePath")]
    public string? WorkspacePath { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("workspacePort")]
    public int? WorkspacePort { get; set; }

    [JsonPropertyName("todoPath")]
    public string? TodoPath { get; set; }

    [JsonPropertyName("tunnelProvider")]
    public string? TunnelProvider { get; set; }

    [JsonPropertyName("runAs")]
    public string? RunAs { get; set; }
}

public sealed class McpWorkspaceUpdateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("todoPath")]
    public string? TodoPath { get; set; }

    [JsonPropertyName("workspacePort")]
    public int? WorkspacePort { get; set; }

    [JsonPropertyName("tunnelProvider")]
    public string? TunnelProvider { get; set; }

    [JsonPropertyName("runAs")]
    public string? RunAs { get; set; }
}

public sealed class McpWorkspaceMutationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("workspace")]
    public McpWorkspaceItem? Workspace { get; set; }
}

public sealed class McpWorkspaceProcessStatus
{
    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("uptime")]
    public string? Uptime { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class McpWorkspaceInitResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("filesCreated")]
    public List<string>? FilesCreated { get; set; }
}
