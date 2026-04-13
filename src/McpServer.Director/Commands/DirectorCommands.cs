using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using McpServer.Cqrs;
using McpServerManager.UI.Core.Messages;
using Spectre.Console;
using static McpServerManager.Director.Commands.CommandHelpers;

namespace McpServerManager.Director.Commands;

/// <summary>
/// FR-MCP-030: Director CLI commands for health, workspace/agent operations, TODO, and session logs.
/// All server interactions dispatch through UI.Core CQRS handlers.
/// </summary>
internal static class DirectorCommands
{
    private static readonly Option<string?> s_workspaceOption = new("--workspace", "Workspace path (defaults to current directory)");

    /// <summary>Registers all Director commands on the root command.</summary>
    public static void Register(RootCommand root)
    {
        s_workspaceOption.AddAlias("-w");

        root.AddCommand(BuildHealthCommand());
        root.AddCommand(BuildListCommand());
        root.AddCommand(BuildAgentsCommand());
        root.AddCommand(BuildAddCommand());
        root.AddCommand(BuildBanCommand());
        root.AddCommand(BuildUnbanCommand());
        root.AddCommand(BuildDeleteCommand());
        root.AddCommand(BuildValidateCommand());
        root.AddCommand(BuildInitCommand());
        root.AddCommand(BuildAddWorkspaceCommand());
        root.AddCommand(BuildTodoCommand());
        root.AddCommand(BuildSessionLogCommand());
    }

    private static Command BuildHealthCommand()
    {
        var cmd = new Command("health", "Check MCP server health") { s_workspaceOption };
        cmd.SetHandler(async (string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                try
                {
                    var result = await dispatcher.QueryAsync(new CheckHealthQuery()).ConfigureAwait(true);
                    if (!result.IsSuccess || result.Value is null)
                    {
                        Error(result.Error ?? "Health check failed.");
                        return;
                    }

                    var snapshot = result.Value;
                    var server = snapshot.ServerBaseUrl
                        ?? context.ControlClient?.BaseUrl
                        ?? context.ActiveWorkspaceClient?.BaseUrl
                        ?? "(unknown)";
                    Success($"Server healthy at {server}");
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(snapshot.RawPayload)}[/]");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                    Error($"Server unreachable: {ex.Message}");
                }
            }).ConfigureAwait(true);
        }, s_workspaceOption);
        return cmd;
    }

    private static Command BuildListCommand()
    {
        var cmd = new Command("list", "List all registered workspaces") { s_workspaceOption };
        cmd.SetHandler(async (string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, _) =>
            {
                try
                {
                    var result = await dispatcher.QueryAsync(new ListWorkspacesQuery()).ConfigureAwait(true);
                    if (!result.IsSuccess || result.Value is null)
                    {
                        Error(result.Error ?? "Workspace list failed.");
                        return;
                    }

                    var table = new Table();
                    table.AddColumn("Name");
                    table.AddColumn("Path");
                    table.AddColumn("Enabled");

                    foreach (var item in result.Value.Items)
                    {
                        table.AddRow(
                            Markup.Escape(item.Name),
                            Markup.Escape(item.WorkspacePath),
                            item.IsEnabled ? "[green]Yes[/]" : "[red]No[/]");
                    }

                    AnsiConsole.Write(table);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                    Error(ex.Message);
                }
            }).ConfigureAwait(true);
        }, s_workspaceOption);
        return cmd;
    }

    private static Command BuildAgentsCommand()
    {
        var defCmd = new Command("definitions", "List all agent type definitions");
        defCmd.AddAlias("defs");
        defCmd.AddOption(s_workspaceOption);
        defCmd.SetHandler(async (string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, _) =>
            {
                var result = await dispatcher.QueryAsync(new ListAgentDefinitionsQuery()).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "Failed to load agent definitions.");
                    return;
                }

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Display Name");
                table.AddColumn("Built-In");

                foreach (var item in result.Value.Items)
                {
                    table.AddRow(
                        Markup.Escape(item.Id),
                        Markup.Escape(item.DisplayName),
                        item.IsBuiltIn ? "[green]Yes[/]" : "No");
                }

                AnsiConsole.Write(table);
            }).ConfigureAwait(true);
        }, s_workspaceOption);

        var wsCmd = new Command("workspace", "List agents configured for this workspace");
        wsCmd.AddAlias("ws");
        wsCmd.AddOption(s_workspaceOption);
        wsCmd.SetHandler(async (string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = ResolveWorkspacePathOrError(context);
                if (workspacePath is null)
                    return;

                var result = await dispatcher.QueryAsync(new ListWorkspaceAgentsQuery(workspacePath)).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "Failed to load workspace agents.");
                    return;
                }

                var table = new Table();
                table.AddColumn("Agent ID");
                table.AddColumn("Enabled");
                table.AddColumn("Banned");
                table.AddColumn("Isolation");
                table.AddColumn("Last Launched");

                foreach (var item in result.Value.Items)
                {
                    table.AddRow(
                        Markup.Escape(item.AgentId),
                        item.Enabled ? "[green]Yes[/]" : "[red]No[/]",
                        item.Banned ? $"[red]Yes[/] ({Markup.Escape(item.BannedReason ?? "")})" : "[green]No[/]",
                        Markup.Escape(item.AgentIsolation),
                        item.LastLaunchedAt?.ToString("O") ?? "-");
                }

                AnsiConsole.Write(table);
            }).ConfigureAwait(true);
        }, s_workspaceOption);

        var eventsCmd = new Command("events", "Show agent lifecycle events");
        var agentIdArg = new Argument<string>("agent-id", "Agent type ID");
        var limitOpt = new Option<int>("--limit", () => 20, "Max events to show");
        eventsCmd.AddArgument(agentIdArg);
        eventsCmd.AddOption(s_workspaceOption);
        eventsCmd.AddOption(limitOpt);
        eventsCmd.SetHandler(async (string agentId, string? workspace, int limit) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = ResolveWorkspacePathOrError(context);
                if (workspacePath is null)
                    return;

                var result = await dispatcher.QueryAsync(new GetAgentEventsQuery(agentId, workspacePath, limit)).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "Failed to load events.");
                    return;
                }

                var table = new Table();
                table.AddColumn("Timestamp");
                table.AddColumn("Event");
                table.AddColumn("User");
                table.AddColumn("Details");

                foreach (var item in result.Value.Items)
                {
                    table.AddRow(
                        item.Timestamp.ToString("O"),
                        Markup.Escape(item.EventType.ToString()),
                        Markup.Escape(item.UserId ?? "-"),
                        Markup.Escape(item.Details ?? ""));
                }

                AnsiConsole.Write(table);
            }).ConfigureAwait(true);
        }, agentIdArg, s_workspaceOption, limitOpt);

        var agentsCmd = new Command("agents", "Manage agents (definitions, workspace configs, events)")
        {
            defCmd,
            wsCmd,
            eventsCmd,
        };
        return agentsCmd;
    }

    private static Command BuildAddCommand()
    {
        var agentIdArg = new Argument<string>("agent-id", "Agent type ID to add");
        var isolationOpt = new Option<string>("--isolation", () => "worktree", "Isolation strategy: worktree or clone");
        var enabledOpt = new Option<bool>("--enabled", () => true, "Whether the agent is enabled");

        var cmd = new Command("add", "Add an agent to the current workspace")
        {
            agentIdArg,
            s_workspaceOption,
            isolationOpt,
            enabledOpt,
        };

        cmd.SetHandler(async (string agentId, string? workspace, string isolation, bool enabled) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = ResolveWorkspacePathOrError(context);
                if (workspacePath is null)
                    return;

                var result = await dispatcher.SendAsync(new AssignWorkspaceAgentCommand
                {
                    AgentId = agentId,
                    WorkspacePath = workspacePath,
                    Enabled = enabled,
                    AgentIsolation = isolation,
                }).ConfigureAwait(true);

                PrintAgentMutationOutcome(result, $"Agent '{agentId}' added to workspace.");
            }).ConfigureAwait(true);
        }, agentIdArg, s_workspaceOption, isolationOpt, enabledOpt);

        return cmd;
    }

    private static Command BuildBanCommand()
    {
        var agentIdArg = new Argument<string>("agent-id", "Agent type ID to ban");
        var reasonOpt = new Option<string?>("--reason", "Reason for banning");
        var globalOpt = new Option<bool>("--global", () => false, "Ban globally across all workspaces");
        var prOpt = new Option<int?>("--until-pr", "PR number that must close before unbanning");

        var cmd = new Command("ban", "Ban an agent from a workspace (or globally)")
        {
            agentIdArg,
            s_workspaceOption,
            reasonOpt,
            globalOpt,
            prOpt,
        };

        cmd.SetHandler(async (string agentId, string? workspace, string? reason, bool global, int? untilPr) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = global ? null : ResolveWorkspacePathOrError(context);
                if (!global && workspacePath is null)
                    return;

                var result = await dispatcher.SendAsync(new BanAgentCommand
                {
                    AgentId = agentId,
                    Reason = reason,
                    Global = global,
                    BannedUntilPr = untilPr,
                    WorkspacePath = workspacePath,
                }).ConfigureAwait(true);

                PrintAgentMutationOutcome(result, $"Agent '{agentId}' banned{(global ? " globally" : "")}.");
            }).ConfigureAwait(true);
        }, agentIdArg, s_workspaceOption, reasonOpt, globalOpt, prOpt);

        return cmd;
    }

    private static Command BuildUnbanCommand()
    {
        var agentIdArg = new Argument<string>("agent-id", "Agent type ID to unban");
        var globalOpt = new Option<bool>("--global", () => false, "Unban globally across all workspaces");

        var cmd = new Command("unban", "Unban an agent")
        {
            agentIdArg,
            s_workspaceOption,
            globalOpt,
        };

        cmd.SetHandler(async (string agentId, string? workspace, bool global) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = global ? null : ResolveWorkspacePathOrError(context);
                if (!global && workspacePath is null)
                    return;

                var result = await dispatcher.SendAsync(new UnbanAgentCommand(agentId, workspacePath, global)).ConfigureAwait(true);
                PrintAgentMutationOutcome(result, $"Agent '{agentId}' unbanned{(global ? " globally" : "")}.");
            }).ConfigureAwait(true);
        }, agentIdArg, s_workspaceOption, globalOpt);

        return cmd;
    }

    private static Command BuildDeleteCommand()
    {
        var agentIdArg = new Argument<string>("agent-id", "Agent type ID to remove");

        var cmd = new Command("delete", "Remove an agent from the current workspace")
        {
            agentIdArg,
            s_workspaceOption,
        };

        cmd.SetHandler(async (string agentId, string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = ResolveWorkspacePathOrError(context);
                if (workspacePath is null)
                    return;

                var result = await dispatcher.SendAsync(new DeleteWorkspaceAgentCommand(agentId, workspacePath)).ConfigureAwait(true);
                PrintAgentMutationOutcome(result, $"Agent '{agentId}' removed from workspace.");
            }).ConfigureAwait(true);
        }, agentIdArg, s_workspaceOption);

        return cmd;
    }

    private static Command BuildValidateCommand()
    {
        var cmd = new Command("validate", "Validate the agents.yaml file for a workspace") { s_workspaceOption };
        cmd.SetHandler(async (string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = ResolveWorkspacePathOrError(context);
                if (workspacePath is null)
                    return;

                var result = await dispatcher.QueryAsync(new ValidateAgentQuery(workspacePath)).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "Validation failed.");
                    return;
                }

                if (result.Value.Valid)
                    Success("agents.yaml is valid.");
                else
                {
                    Error("agents.yaml validation failed.");
                    if (!string.IsNullOrWhiteSpace(result.Value.Error))
                        AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(result.Value.Error)}[/]");
                }
            }).ConfigureAwait(true);
        }, s_workspaceOption);
        return cmd;
    }

    private static Command BuildInitCommand()
    {
        var cmd = new Command("init", "Initialize the current workspace for agent management") { s_workspaceOption };
        cmd.SetHandler(async (string? workspace) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, context) =>
            {
                var workspacePath = ResolveWorkspacePathOrError(context);
                if (workspacePath is null)
                    return;

                var result = await dispatcher.SendAsync(new InitWorkspaceCommand(workspacePath)).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "Workspace init failed.");
                    return;
                }

                var seededText = result.Value.SeededDefinitions is int seeded ? $" (seeded {seeded})" : "";
                Success($"Workspace initialized for agent management{seededText}.");
            }).ConfigureAwait(true);
        }, s_workspaceOption);
        return cmd;
    }

    /// <summary>
    /// FR-MCP-030: Adds CWD (or the specified path) as a new workspace on the MCP Server,
    /// waits for the server to write the AGENTS-README-FIRST.yaml marker file, then
    /// verifies trust via HMAC signature check and health nonce echo.
    /// </summary>
    private static Command BuildAddWorkspaceCommand()
    {
        var nameOption = new Option<string?>("--name", "Display name for the workspace (defaults to directory name)");
        nameOption.AddAlias("-n");
        var serverOption = new Option<string>("--server", () => "http://localhost:7147", "MCP Server base URL");
        serverOption.AddAlias("-s");

        var cmd = new Command("add-workspace", "Register CWD as a new MCP Server workspace and verify trust")
        {
            s_workspaceOption,
            nameOption,
            serverOption,
        };

        cmd.SetHandler(async (string? workspace, string? name, string server) =>
        {
            var workspacePath = Path.GetFullPath(
                string.IsNullOrWhiteSpace(workspace) ? Environment.CurrentDirectory : workspace.Trim());
            var markerPath = Path.Combine(workspacePath, "AGENTS-README-FIRST.yaml");

            if (File.Exists(markerPath))
            {
                Info($"Workspace already registered — validating trust for {markerPath}...");
                await ValidateMarkerTrustAsync(workspacePath, markerPath).ConfigureAwait(true);
                return;
            }

            name ??= new DirectoryInfo(workspacePath).Name;
            Info($"Registering workspace '{name}' at {workspacePath}...");

            // Step 1: Obtain an API key with write access.
            // Prefer the primary workspace marker (full read/write), then the default key
            // from GET /api-key (read-only — may be rejected for mutations), then fail.
            string apiKey;
            var primaryClient = McpHttpClient.TryGetPrimaryWorkspaceClient();
            if (primaryClient is not null && !string.IsNullOrWhiteSpace(primaryClient.ApiKey))
            {
                apiKey = primaryClient.ApiKey;
                primaryClient.Dispose();
                Info("Using primary workspace API key for authentication.");
            }
            else
            {
                try
                {
                    using var anonHttp = new HttpClient { BaseAddress = new Uri(server) };
                    var keyResponse = await anonHttp.GetAsync("/api-key").ConfigureAwait(true);
                    keyResponse.EnsureSuccessStatusCode();
                    var keyJson = await keyResponse.Content.ReadAsStringAsync().ConfigureAwait(true);
                    using var doc = JsonDocument.Parse(keyJson);
                    apiKey = doc.RootElement.GetProperty("apiKey").GetString()
                        ?? throw new InvalidOperationException("Server returned null apiKey.");
                    Warn("No primary workspace marker found; using the default read-only key (mutations may be rejected).");
                }
                catch (Exception ex)
                {
                    Error($"Failed to obtain an API key: {ex.Message}");
                    return;
                }
            }

            // Step 2: Register the workspace (with retry for 503 during server startup).
            // Uses raw HttpClient instead of McpHttpClient.PostAsync to get direct access
            // to HttpResponseMessage.StatusCode for the retry decision.
            const int maxRetries = 5;
            using var registrationHttp = new HttpClient { BaseAddress = new Uri(server) };
            registrationHttp.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            var requestBody = JsonSerializer.Serialize(new { workspacePath, name, isEnabled = true });

            var registered = false;
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using var response = await registrationHttp.PostAsync("/mcpserver/workspace", content).ConfigureAwait(true);

                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                    using var resultDoc = JsonDocument.Parse(resultJson);
                    if (resultDoc.RootElement.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        var errorMsg = resultDoc.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
                        Error($"Server rejected workspace registration: {errorMsg}");
                        return;
                    }

                    Success("Workspace registered on the MCP Server.");
                    registered = true;
                    break;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s, 8s
                    Warn($"Server returned 503 (startup in progress). Retrying in {delay.TotalSeconds:0}s... ({attempt}/{maxRetries})");
                    await Task.Delay(delay).ConfigureAwait(true);
                    continue;
                }

                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                Error($"Failed to register workspace: HTTP {(int)response.StatusCode} {response.StatusCode}: {errorBody}");
                return;
            }

            if (!registered)
            {
                Error("Failed to register workspace after all retry attempts.");
                return;
            }

            // Step 3: Watch for AGENTS-README-FIRST.yaml to be created by the server
            Info("Waiting for AGENTS-README-FIRST.yaml...");
            var appeared = await WaitForFileAsync(markerPath, TimeSpan.FromSeconds(30)).ConfigureAwait(true);
            if (!appeared)
            {
                Warn("Marker file was not created within 30 seconds. Check the MCP Server logs.");
                return;
            }

            // Step 4: Validate trust on the new marker
            await ValidateMarkerTrustAsync(workspacePath, markerPath).ConfigureAwait(true);
        }, s_workspaceOption, nameOption, serverOption);

        return cmd;
    }

    /// <summary>Waits for a file to appear on disk using FileSystemWatcher + polling fallback.</summary>
    private static async Task<bool> WaitForFileAsync(string filePath, TimeSpan timeout)
    {
        if (File.Exists(filePath))
            return true;

        var dir = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileName(filePath);
        using var tcs = new CancellationTokenSource(timeout);

        try
        {
            using var watcher = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };

            var taskCompletion = new TaskCompletionSource<bool>();
            watcher.Created += (_, _) => taskCompletion.TrySetResult(true);

            // Polling fallback (FSW can miss events on some filesystems)
            _ = Task.Run(async () =>
            {
                while (!tcs.Token.IsCancellationRequested)
                {
                    if (File.Exists(filePath))
                    {
                        taskCompletion.TrySetResult(true);
                        return;
                    }

                    await Task.Delay(500, tcs.Token).ConfigureAwait(false);
                }
            }, tcs.Token);

            tcs.Token.Register(() => taskCompletion.TrySetResult(false));
            return await taskCompletion.Task.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return File.Exists(filePath);
        }
    }

    /// <summary>
    /// Reads the marker file, verifies its HMAC-SHA256 signature, and performs
    /// a health nonce echo check. Prints success/error via <see cref="CommandHelpers"/>.
    /// </summary>
    private static async Task ValidateMarkerTrustAsync(string workspacePath, string markerPath)
    {
        var markerClient = McpHttpClient.FromMarkerOnly(workspacePath);
        if (markerClient is null)
        {
            Error("Marker file could not be parsed.");
            return;
        }

        // Signature verification
        var lines = File.ReadAllLines(markerPath);
        var markerFields = ParseMarkerFields(lines);
        if (!VerifyMarkerSignature(markerFields, out var sigError))
        {
            Error($"Marker signature INVALID: {sigError}");
            markerClient.Dispose();
            return;
        }

        Success("Marker signature verified.");

        // Health nonce echo verification
        var nonce = Guid.NewGuid().ToString("N");
        try
        {
            var healthResult = await markerClient.GetAsync<JsonElement>(
                $"/health?nonce={Uri.EscapeDataString(nonce)}").ConfigureAwait(true);
            var echoedNonce = healthResult.TryGetProperty("nonce", out var nonceProp)
                ? nonceProp.GetString() : null;
            if (!string.Equals(echoedNonce, nonce, StringComparison.Ordinal))
            {
                Error($"Health nonce mismatch: sent '{nonce}', got '{echoedNonce}'.");
                markerClient.Dispose();
                return;
            }

            Success("Health nonce verified.");
        }
        catch (Exception ex)
        {
            Error($"Health check failed: {ex.Message}");
            markerClient.Dispose();
            return;
        }

        Success($"Workspace trusted: {workspacePath}");
        Info($"Marker: {markerPath}");
        Info($"API key: {markerClient.ApiKey[..Math.Min(8, markerClient.ApiKey.Length)]}...");
        markerClient.Dispose();
    }

    /// <summary>
    /// Parses the flat and dotted fields from the marker YAML for signature verification.
    /// Returns a dictionary mapping dotted keys (e.g. "endpoints.health") to their string values.
    /// </summary>
    private static Dictionary<string, string> ParseMarkerFields(string[] lines)
    {
        // The AGENTS-README-FIRST.yaml has this structure:
        //   port: 7147                <- indent 0, top-level field
        //   baseUrl: http://...       <- indent 0, top-level field
        //   endpoints:                <- indent 0, section header (no value)
        //     health: /health         <- indent 2, nested → "endpoints.health"
        //   signature:                <- indent 0, section header (no value)
        //     algorithm: HMAC-SHA256  <- indent 2, nested → "signature.algorithm"
        //     value: 07BD...          <- indent 2, nested → "signature.value"
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var trimmed = line.TrimEnd();
            var indent = trimmed.Length - trimmed.TrimStart().Length;
            trimmed = trimmed.Trim();

            if (!trimmed.Contains(':'))
                continue;

            var colonIdx = trimmed.IndexOf(':');
            var key = trimmed[..colonIdx].Trim();
            var value = trimmed[(colonIdx + 1)..].Trim();

            if (indent == 0)
            {
                if (string.IsNullOrEmpty(value))
                {
                    // Section header at root level (e.g. "endpoints:", "signature:")
                    currentSection = key;
                }
                else
                {
                    // Top-level scalar field (e.g. "port: 7147", "baseUrl: http://...")
                    fields[key] = value;
                    currentSection = null;
                }
            }
            else if (indent == 2 && currentSection is not null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    // Sub-section header (e.g. "fields:" under "signature:") — skip
                    continue;
                }

                // Nested field under the current section
                fields[$"{currentSection}.{key}"] = value;
            }
            // Deeper indentation (indent 4+) is ignored — list items, multi-line values, etc.
        }

        return fields;
    }

    /// <summary>
    /// Verifies the HMAC-SHA256 marker signature using the canonical payload format
    /// from MarkerFileService.BuildSignaturePayload (lib/McpServer/src/McpServer.Services/Services/MarkerFileService.cs:288).
    /// </summary>
    private static bool VerifyMarkerSignature(Dictionary<string, string> fields, out string error)
    {
        error = string.Empty;

        if (!fields.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            error = "Marker file is missing apiKey.";
            return false;
        }

        if (!fields.TryGetValue("signature.value", out var expectedSignature) || string.IsNullOrWhiteSpace(expectedSignature))
        {
            error = "Marker file is missing signature.value.";
            return false;
        }

        // Build canonical payload matching MarkerFileService.BuildSignaturePayload (lines 288-321)
        string[] signatureFields =
        [
            "signature.canonicalization", "port", "baseUrl", "apiKey", "workspace", "workspacePath",
            "pid", "startedAt", "markerWrittenAtUtc", "serverStartedAtUtc",
            "endpoints.health", "endpoints.swagger", "endpoints.swaggerUi", "endpoints.mcpTransport",
            "endpoints.sessionLog", "endpoints.sessionLogDialog",
            "endpoints.contextSearch", "endpoints.contextPack", "endpoints.contextSources",
            "endpoints.todo", "endpoints.repo", "endpoints.desktop", "endpoints.gitHub",
            "endpoints.tools", "endpoints.workspace", "endpoints.serverStartupUtc",
            "endpoints.markerFileTimestamp",
        ];

        // The server uses the plain key names (without the "signature." prefix for canonicalization)
        var payload = new StringBuilder();
        foreach (var field in signatureFields)
        {
            var lookupKey = field;
            var payloadKey = field;

            // "signature.canonicalization" is stored under key "canonicalization" in the payload
            // but looked up as "signature.canonicalization" in our parsed fields
            if (field == "signature.canonicalization")
                payloadKey = "canonicalization";

            var value = fields.TryGetValue(lookupKey, out var v) ? v : string.Empty;
            payload.Append(payloadKey).Append('=').Append(value.ReplaceLineEndings("\n")).Append('\n');
        }

        var keyBytes = Encoding.UTF8.GetBytes(apiKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload.ToString());
        using var hmac = new HMACSHA256(keyBytes);
        var computed = Convert.ToHexString(hmac.ComputeHash(payloadBytes));

        if (!string.Equals(computed, expectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Computed {computed[..16]}... does not match expected {expectedSignature[..16]}...";
            return false;
        }

        return true;
    }

    private static Command BuildTodoCommand()
    {
        var listCmd = new Command("list", "List TODO items") { s_workspaceOption };
        var sectionOpt = new Option<string?>("--section", "Filter by section");
        listCmd.AddOption(sectionOpt);
        listCmd.SetHandler(async (string? workspace, string? section) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, _) =>
            {
                var result = await dispatcher.QueryAsync(new ListTodosQuery { Section = section }).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "TODO list failed.");
                    return;
                }

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Title");
                table.AddColumn("Section");
                table.AddColumn("Priority");
                table.AddColumn("Done");

                foreach (var item in result.Value.Items)
                {
                    table.AddRow(
                        Markup.Escape(item.Id),
                        Markup.Escape(item.Title),
                        Markup.Escape(item.Section),
                        Markup.Escape(item.Priority),
                        item.Done ? "[green]✓[/]" : "○");
                }

                AnsiConsole.Write(table);
                Info($"{result.Value.Items.Count} items");
            }).ConfigureAwait(true);
        }, s_workspaceOption, sectionOpt);

        var todoCmd = new Command("todo", "Manage TODO items") { listCmd };
        return todoCmd;
    }

    private static Command BuildSessionLogCommand()
    {
        var listCmd = new Command("list", "List recent session logs") { s_workspaceOption };
        var limitOpt = new Option<int>("--limit", () => 10, "Max logs to show");
        listCmd.AddOption(limitOpt);
        listCmd.SetHandler(async (string? workspace, int limit) =>
        {
            await RunWithDispatcherAsync(workspace, async (_, dispatcher, _) =>
            {
                var result = await dispatcher.QueryAsync(new ListSessionLogsQuery
                {
                    Limit = limit,
                    Offset = 0
                }).ConfigureAwait(true);

                if (!result.IsSuccess || result.Value is null)
                {
                    Error(result.Error ?? "Session log query failed.");
                    return;
                }

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Source");
                table.AddColumn("Title");
                table.AddColumn("Status");
                table.AddColumn("Updated");

                foreach (var item in result.Value.Items)
                {
                    table.AddRow(
                        Markup.Escape(item.SessionId),
                        Markup.Escape(item.SourceType),
                        Markup.Escape(item.Title),
                        string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase)
                            ? "[green]completed[/]"
                            : $"[yellow]{Markup.Escape(item.Status)}[/]",
                        Markup.Escape(item.LastUpdated ?? string.Empty));
                }

                AnsiConsole.Write(table);
            }).ConfigureAwait(true);
        }, s_workspaceOption, limitOpt);

        var slCmd = new Command("session-log", "View session logs") { listCmd };
        slCmd.AddAlias("sl");
        return slCmd;
    }

    private static void PrintAgentMutationOutcome(Result<AgentMutationOutcome> result, string successMessage)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            Error(result.Error ?? "Operation failed.");
            return;
        }

        if (result.Value.Success)
        {
            Success(successMessage);
            return;
        }

        Error(result.Value.Error ?? "Operation failed.");
    }
}
