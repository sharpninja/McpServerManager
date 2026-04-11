using System.CommandLine;
using System.Text;
using System.Text.Json;
using McpServer.Client;
using McpServer.Client.Models;
using McpServerManager.Director.Auth;
using McpServer.McpAgent;
using McpServer.McpAgent.Hosting;
using McpServer.McpAgent.PowerShellSessions;
using McpServer.McpAgent.SessionLog;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAIChatClient = OpenAI.Chat.ChatClient;
using OpenAIClientOptions = OpenAI.OpenAIClientOptions;

namespace McpServerManager.Director.Commands;

internal static class AgentHostCommand
{
    private static readonly Option<string?> s_workspaceOption = new("--workspace", "Workspace path (defaults to current directory)");

    public static void Register(RootCommand root)
    {
        s_workspaceOption.AddAlias("-w");

        var promptArgument = new Argument<string[]>("prompt")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Optional initial prompt. If omitted, launches interactive MCP Agent host mode."
        };

        var command = new Command("agent", "Run Director in hosted MCP Agent console mode")
        {
            s_workspaceOption,
            promptArgument
        };

        command.SetHandler(async (string? workspace, string[] prompt) =>
        {
            using var cancellationSource = new CancellationTokenSource();
            DirectorAgentConsoleApplication? application = null;
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;

                if (application?.TryCancelActivePowerShellCommand() == true)
                    return;

                cancellationSource.Cancel();
            };

            Console.CancelKeyPress += cancelHandler;
            try
            {
                var settings = DirectorAgentSettings.Resolve(workspace);
                using var serviceProvider = DirectorHost.CreateProvider(
                    settings.WorkspacePath,
                    (services, directorContext) => ConfigureHostedAgentServices(services, directorContext, settings));
                application = new DirectorAgentConsoleApplication(
                    serviceProvider.GetRequiredService<IMcpHostedAgentFactory>().CreateHostedAgent(),
                    settings,
                    serviceProvider.GetRequiredService<ReplWorkflowToolAdapter>(),
                    serviceProvider.GetRequiredService<McpServer.Repl.Core.ITodoWorkflow>(),
                    serviceProvider.GetRequiredService<McpServer.Repl.Core.IRequirementsWorkflow>(),
                    serviceProvider.GetRequiredService<McpServer.Repl.Core.IGenericClientPassthrough>());
                using (application)
                {
                    var args = prompt.Length == 0
                        ? Array.Empty<string>()
                        : [string.Join(' ', prompt).Trim()];
                    var exitCode = await application.RunAsync(args, cancellationSource.Token).ConfigureAwait(false);
                    Environment.ExitCode = exitCode;
                }
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Canceled.");
                Environment.ExitCode = 130;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to start the Director MCP Agent host: {exception}");
                Environment.ExitCode = 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }, s_workspaceOption, promptArgument);

        root.AddCommand(command);
    }

    private static void ConfigureHostedAgentServices(
        IServiceCollection services,
        DirectorMcpContext directorContext,
        DirectorAgentSettings settings)
    {
        directorContext.RefreshBearerTokens();

        // The hosted agent operates inside a specific workspace, and that workspace's
        // AGENTS-README-FIRST.yaml marker carries the authoritative per-workspace API key.
        // Prefer ActiveWorkspaceClient (loaded from the marker) over ControlClient, which
        // is intentionally seeded with an empty API key when a default URL is configured.
        var workspaceClient = directorContext.ActiveWorkspaceClient;
        var controlClient = directorContext.ControlClient;
        var authoritativeClient = PickAuthoritativeClient(workspaceClient, controlClient)
            ?? throw new InvalidOperationException(
                "No MCP workspace connection is available. Run Director from a workspace with " +
                "AGENTS-README-FIRST.yaml or configure a default URL with 'director config set-default-url <url>'.");

        var bearerToken = DirectorAgentSettings.TryLoadCachedBearerToken();
        var workspacePath = string.IsNullOrWhiteSpace(directorContext.ActiveWorkspacePath)
            ? (!string.IsNullOrWhiteSpace(authoritativeClient.WorkspacePath) ? authoritativeClient.WorkspacePath : settings.WorkspacePath)
            : directorContext.ActiveWorkspacePath;

        services.AddMcpServerMcpAgent(options =>
        {
            options.BaseUrl = new Uri(authoritativeClient.BaseUrl);
            options.ApiKey = authoritativeClient.ApiKey;
            options.BearerToken = bearerToken;
            options.RequireAuthentication = !string.IsNullOrWhiteSpace(authoritativeClient.ApiKey) || !string.IsNullOrWhiteSpace(bearerToken);
            options.WorkspacePath = workspacePath;
            options.AgentId = settings.AgentId;
            options.AgentName = settings.AgentName;
            options.Description = settings.AgentDescription;
            options.SourceType = settings.SourceType;
        });

        // Register REPL workflow services for richer TODO, requirements, and client passthrough tools.
        services.AddSingleton<McpServer.Repl.Core.ISessionLogWorkflow>(sp =>
            new McpServer.Repl.Core.SessionLogWorkflow(
                sp.GetRequiredService<McpServer.Client.McpServerClient>().SessionLog,
                TimeProvider.System));
        services.AddSingleton<McpServer.Repl.Core.ITodoWorkflow>(sp =>
            new McpServer.Repl.Core.TodoWorkflow(
                sp.GetRequiredService<McpServer.Client.McpServerClient>().Todo));
        services.AddSingleton<McpServer.Repl.Core.IRequirementsWorkflow>(sp =>
            new McpServer.Repl.Core.RequirementsWorkflow(
                sp.GetRequiredService<McpServer.Client.McpServerClient>().Requirements));
        services.AddSingleton<McpServer.Repl.Core.IGenericClientPassthrough>(sp =>
            new McpServer.Repl.Core.GenericClientPassthrough(
                sp.GetRequiredService<McpServer.Client.McpServerClient>()));
        services.AddSingleton<ReplWorkflowToolAdapter>();
    }

    /// <summary>
    /// Chooses the HTTP client that should back the hosted agent's MCP connection.
    /// The workspace-marker client carries the authoritative per-workspace API key and is
    /// preferred whenever it is populated. The control-plane client is used only as a
    /// last-resort fallback (e.g., when running with a configured default URL and no marker).
    /// </summary>
    internal static McpHttpClient? PickAuthoritativeClient(
        McpHttpClient? workspaceClient,
        McpHttpClient? controlClient)
    {
        // Workspace marker has a real API key — always the best choice.
        if (workspaceClient is not null && !string.IsNullOrWhiteSpace(workspaceClient.ApiKey))
            return workspaceClient;

        // Control client has a real API key — fall back to it.
        if (controlClient is not null && !string.IsNullOrWhiteSpace(controlClient.ApiKey))
            return controlClient;

        // Neither has a key — prefer the workspace client when present so the base URL
        // and workspace path reflect the workspace the user asked for; otherwise try
        // the control client as a last resort.
        return workspaceClient ?? controlClient;
    }
}

internal sealed class DirectorAgentConsoleApplication : IDisposable
{
    private readonly IMcpHostedAgent _hostedAgent;
    private readonly DirectorAgentSettings _settings;
    private readonly IChatClient _chatClient;
    private readonly ChatClientAgent _chatAgent;
    private readonly object _powerShellCommandSync = new();
    private readonly ChatClientAgentRunOptions _runOptions;
    private readonly McpServer.Repl.Core.ITodoWorkflow _replTodo;
    private readonly McpServer.Repl.Core.IRequirementsWorkflow _replRequirements;
    private readonly McpServer.Repl.Core.IGenericClientPassthrough _replPassthrough;
    private CancellationTokenSource? _activePowerShellCommandCancellationSource;
    private AgentSession? _agentSession;
    private string? _powerShellSessionId;
    private string _powerShellCurrentLocation;
    private string _verbosity;
    private bool _shouldInjectSystemPrompt = true;
    private readonly List<string> _inputHistory = new();
    private int _historyBrowseIndex = -1;
    private string _historyScratchLine = "";

    public DirectorAgentConsoleApplication(
        IMcpHostedAgent hostedAgent,
        DirectorAgentSettings settings,
        ReplWorkflowToolAdapter replAdapter,
        McpServer.Repl.Core.ITodoWorkflow replTodo,
        McpServer.Repl.Core.IRequirementsWorkflow replRequirements,
        McpServer.Repl.Core.IGenericClientPassthrough replPassthrough)
    {
        _hostedAgent = hostedAgent ?? throw new ArgumentNullException(nameof(hostedAgent));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _replTodo = replTodo ?? throw new ArgumentNullException(nameof(replTodo));
        _replRequirements = replRequirements ?? throw new ArgumentNullException(nameof(replRequirements));
        _replPassthrough = replPassthrough ?? throw new ArgumentNullException(nameof(replPassthrough));
        _chatClient = CreateChatClient(settings);
        _chatAgent = hostedAgent.CreateChatClientAgent(_chatClient);
        _runOptions = hostedAgent.CreateRunOptions();

        // Merge REPL workflow tools into the agent's tool set
        var chatOptions = _runOptions.ChatOptions ??= new Microsoft.Extensions.AI.ChatOptions();
        chatOptions.Tools ??= new List<AITool>();
        foreach (var tool in replAdapter.CreateTools())
            chatOptions.Tools.Add(tool);

        _powerShellCurrentLocation = settings.WorkspacePath;
        _verbosity = settings.Verbosity;
        LoadConsoleStateFromDisk();
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        WriteBanner();
        Console.WriteLine("Type /help for commands. Prefix a line with ! to run it directly in the local PowerShell session.");
        Console.WriteLine("Use ↑ and ↓ to recall previous inputs.");
        Console.WriteLine();

        EnsurePowerShellSession();
        await StartNewConversationAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (args.Length > 0)
            {
                var commandText = string.Join(' ', args).Trim();
                if (!string.IsNullOrWhiteSpace(commandText))
                    await TryDispatchAsync(commandText, cancellationToken).ConfigureAwait(false);

                return 0;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var input = ReadConsoleLine(BuildConsolePrompt(), cancellationToken, useChatHistory: true);
                if (input is null)
                    break;

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (await TryDispatchAsync(input, cancellationToken).ConfigureAwait(false))
                    break;
            }

            return 0;
        }
        finally
        {
            ClosePowerShellSession();
            await TryCompleteSessionAsync().ConfigureAwait(false);
        }
    }

    public bool TryCancelActivePowerShellCommand()
    {
        CancellationTokenSource? activeCancellation;
        lock (_powerShellCommandSync)
            activeCancellation = _activePowerShellCommandCancellationSource;

        if (activeCancellation is null)
            return false;

        activeCancellation.Cancel();
        return true;
    }

    public void Dispose()
    {
        ClosePowerShellSession();
        _hostedAgent.PowerShellSessions.Dispose();
        _chatClient.Dispose();
    }

    private async Task<bool> TryDispatchAsync(string input, CancellationToken cancellationToken)
    {
        try
        {
            return await DispatchAsync(input, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (McpUnauthorizedException exception)
        {
            WriteExpiredAuthenticationMessage("processing the request", exception.Message);
            return false;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error> {exception.Message}");
            Console.Error.WriteLine();
            return false;
        }
    }

    private async Task<bool> DispatchAsync(string input, CancellationToken cancellationToken)
    {
        if (input.StartsWith("! ", StringComparison.Ordinal) && !TryGetDirectPowerShellCommand(input, out _))
        {
            Console.Error.WriteLine("error> Enter a PowerShell command after '! '.");
            Console.Error.WriteLine();
            return false;
        }

        if (TryGetDirectPowerShellCommand(input, out var powerShellCommand))
        {
            await ExecuteDirectPowerShellAsync(powerShellCommand, cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (TryHandleCommand(input, out var shouldExit, out var commandAction))
        {
            await commandAction(cancellationToken).ConfigureAwait(false);
            return shouldExit;
        }

        await ExecutePromptAsync(input.Trim(), cancellationToken).ConfigureAwait(false);
        return false;
    }

    private bool TryHandleCommand(
        string input,
        out bool shouldExit,
        out Func<CancellationToken, Task> commandAction)
    {
        shouldExit = false;
        commandAction = static _ => Task.CompletedTask;
        if (!input.StartsWith("/", StringComparison.Ordinal))
            return false;

        var command = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        switch (command)
        {
            case "/help":
                commandAction = _ =>
                {
                    WriteHelp();
                    return Task.CompletedTask;
                };
                return true;
            case "/exit":
            case "/quit":
                shouldExit = true;
                return true;
            case "/new":
            case "/reset":
                commandAction = StartNewConversationAsync;
                return true;
            case "/tools":
                commandAction = _ =>
                {
                    WriteToolList();
                    return Task.CompletedTask;
                };
                return true;
            case "/session":
                commandAction = _ =>
                {
                    WriteSessionSummary();
                    return Task.CompletedTask;
                };
                return true;
            case "/v":
                if (!TryParseVerbosityLevel(input, out var verbosity, out var errorMessage))
                {
                    commandAction = _ =>
                    {
                        Console.Error.WriteLine($"error> {errorMessage}");
                        Console.Error.WriteLine();
                        return Task.CompletedTask;
                    };
                    return true;
                }

                commandAction = _ =>
                {
                    _verbosity = verbosity;
                    _shouldInjectSystemPrompt = true;
                    PersistConsoleStateToDisk();
                    Console.WriteLine($"Verbosity set to {_verbosity}.");
                    Console.WriteLine();
                    return Task.CompletedTask;
                };
                return true;
            case "/todo":
                commandAction = ct => HandleTodoCommandAsync(input, ct);
                return true;
            case "/requirements":
            case "/reqs":
                commandAction = ct => HandleRequirementsCommandAsync(input, ct);
                return true;
            case "/client":
                commandAction = ct => HandleClientCommandAsync(input, ct);
                return true;
            default:
                commandAction = _ =>
                {
                    Console.WriteLine($"Unknown command '{command}'. Type /help for the supported commands.");
                    Console.WriteLine();
                    return Task.CompletedTask;
                };
                return true;
        }
    }

    private async Task ExecuteDirectPowerShellAsync(string command, CancellationToken cancellationToken)
    {
        EnsurePowerShellSession();
        using var directCommandCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_powerShellCommandSync)
            _activePowerShellCommandCancellationSource = directCommandCancellationSource;

        try
        {
            var result = await _hostedAgent.PowerShellSessions.ExecuteInteractiveCommandAsync(
                    _powerShellSessionId!,
                    command,
                    token => ReadConsoleLine(string.Empty, token, useChatHistory: false),
                    Console.Out,
                    Console.Error,
                    directCommandCancellationSource.Token)
                .ConfigureAwait(false);

            _powerShellCurrentLocation = result.CurrentLocation ?? _powerShellCurrentLocation;
            WritePowerShellResult(result);
            DrainPendingConsoleInput();
        }
        finally
        {
            lock (_powerShellCommandSync)
                _activePowerShellCommandCancellationSource = null;
        }
    }

    private async Task ExecutePromptAsync(string input, CancellationToken cancellationToken)
    {
        SessionLogTurnContext? turn;
        try
        {
            turn = await _hostedAgent.SessionLog.BeginTurnAsync(
                new SessionLogTurnCreateRequest
                {
                    QueryText = input,
                    QueryTitle = BuildTurnTitle(input),
                    Interpretation = "User prompt submitted through the interactive MCP Agent Director host.",
                    Model = _settings.ModelId,
                    ModelProvider = "openai",
                    Status = "in_progress",
                    Tags = ["director-agent", "cli-chat"],
                    ContextList = [_settings.WorkspacePath],
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (McpUnauthorizedException exception)
        {
            WriteExpiredAuthenticationMessage("starting the session-log turn", exception.Message);
            return;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"warning> Failed to start the session-log turn: {exception.Message}");
            Console.Error.WriteLine();
            return;
        }

        try
        {
            var response = await _chatAgent.RunAsync(
                CreateInputMessages(input),
                _agentSession,
                _runOptions,
                cancellationToken).ConfigureAwait(false);

            var responseText = ExtractResponseText(response);
            var tokenCount = ConvertTokenCount(response.Usage?.TotalTokenCount);

            Console.WriteLine("assistant>");
            Console.WriteLine(responseText);
            Console.WriteLine();

            _shouldInjectSystemPrompt = false;
            await TryCompleteTurnAsync(turn.RequestId, responseText, tokenCount, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TryFailTurnAsync(
                turn.RequestId,
                "The chat turn was canceled.",
                "The interactive MCP Agent Director host canceled the active turn.",
                "The chat turn was canceled.",
                ["The active request was canceled before a response was returned."],
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error> {exception.Message}");
            Console.Error.WriteLine();

            await TryFailTurnAsync(
                turn.RequestId,
                exception.Message,
                "The interactive MCP Agent Director host failed to complete the chat turn.",
                exception.Message,
                [exception.Message],
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task TryCompleteTurnAsync(
        string requestId,
        string responseText,
        int? tokenCount,
        CancellationToken cancellationToken)
    {
        if (!CanMutateTurn(requestId))
            return;

        try
        {
            await _hostedAgent.SessionLog.CompleteTurnAsync(
                new SessionLogTurnCompleteRequest
                {
                    RequestId = requestId,
                    Response = responseText,
                    Interpretation = "Processed through the interactive MCP Agent Director host.",
                    Model = _settings.ModelId,
                    ModelProvider = "openai",
                    TokenCount = tokenCount,
                    Tags = ["director-agent", "cli-chat"],
                    ContextList = [_settings.WorkspacePath],
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (WasTurnReplaced(exception, requestId))
        {
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"warning> Failed to record session-log completion: {exception.Message}");
            Console.Error.WriteLine();
        }
    }

    private async Task TryFailTurnAsync(
        string requestId,
        string response,
        string interpretation,
        string failureNote,
        IReadOnlyList<string> blockers,
        CancellationToken cancellationToken)
    {
        if (!CanMutateTurn(requestId))
            return;

        try
        {
            await _hostedAgent.SessionLog.FailTurnAsync(
                new SessionLogTurnFailureRequest
                {
                    RequestId = requestId,
                    Response = response,
                    Interpretation = interpretation,
                    Model = _settings.ModelId,
                    ModelProvider = "openai",
                    FailureNote = failureNote,
                    Tags = ["director-agent", "cli-chat"],
                    ContextList = [_settings.WorkspacePath],
                    Blockers = [.. blockers],
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (WasTurnReplaced(exception, requestId))
        {
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"warning> Failed to record session-log failure: {exception.Message}");
            Console.Error.WriteLine();
        }
    }

    private bool CanMutateTurn(string requestId) =>
        _hostedAgent.SessionLog.Context?.FindTurn(requestId) is not null;

    private static bool WasTurnReplaced(InvalidOperationException exception, string requestId) =>
        exception.Message.Contains(requestId, StringComparison.Ordinal)
        && exception.Message.Contains("was not found in the current session-log workflow context", StringComparison.Ordinal);

    private async Task StartNewConversationAsync(CancellationToken cancellationToken)
    {
        if (_hostedAgent.SessionLog.Context is not null)
        {
            try
            {
                await _hostedAgent.SessionLog.UpdateSessionAsync(
                    new SessionLogSessionUpdateRequest
                    {
                        Status = "completed",
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (McpUnauthorizedException exception)
            {
                WriteExpiredAuthenticationMessage("closing the previous session log", exception.Message);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"warning> Failed to close the previous session log cleanly: {exception.Message}");
                Console.Error.WriteLine();
            }
        }

        _agentSession = await _chatAgent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        _shouldInjectSystemPrompt = true;
        try
        {
            var context = await _hostedAgent.SessionLog.BootstrapAsync(
                new SessionLogBootstrapRequest
                {
                    Model = _settings.ModelId,
                    SessionIdSuffix = "cli-chat",
                    Status = "in_progress",
                    Title = _settings.SessionTitle,
                    Workspace = CreateWorkspaceInfo(),
                },
                cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"Started session {context.SessionId}.");
            Console.WriteLine();
        }
        catch (McpUnauthorizedException exception)
        {
            WriteExpiredAuthenticationMessage("starting the session log", exception.Message);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"warning> Failed to start the session log: {exception.Message}");
            Console.Error.WriteLine();
        }
    }

    private async Task TryCompleteSessionAsync()
    {
        if (_hostedAgent.SessionLog.Context is null)
            return;

        try
        {
            await _hostedAgent.SessionLog.UpdateSessionAsync(
                new SessionLogSessionUpdateRequest
                {
                    Status = "completed",
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (McpUnauthorizedException exception)
        {
            WriteExpiredAuthenticationMessage("closing the session log cleanly", exception.Message);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"warning> Failed to close the session log cleanly: {exception.Message}");
            Console.Error.WriteLine();
        }
    }

    private WorkspaceInfoDto CreateWorkspaceInfo()
    {
        var workspaceDirectory = new DirectoryInfo(_settings.WorkspacePath);
        return new WorkspaceInfoDto
        {
            Project = string.IsNullOrWhiteSpace(workspaceDirectory.Name)
                ? _settings.WorkspacePath
                : workspaceDirectory.Name,
            TargetFramework = "net9.0",
        };
    }

    private IReadOnlyList<ChatMessage> CreateInputMessages(string input)
    {
        if (_shouldInjectSystemPrompt && !string.IsNullOrWhiteSpace(_settings.SystemPrompt))
        {
            return
            [
                new ChatMessage(ChatRole.System, DirectorAgentSettings.BuildSystemPrompt(_settings.SystemPrompt, _verbosity)),
                new ChatMessage(ChatRole.User, input),
            ];
        }

        return [new ChatMessage(ChatRole.User, input)];
    }

    private static IChatClient CreateChatClient(DirectorAgentSettings settings)
    {
        var options = new OpenAIClientOptions
        {
            NetworkTimeout = settings.ModelNetworkTimeout,
            RetryPolicy = new ClientRetryPolicy(settings.ModelMaxRetries),
        };
        if (settings.ModelEndpoint is not null)
            options.Endpoint = settings.ModelEndpoint;

        var chatClient = new OpenAIChatClient(
            settings.ModelId,
            new ApiKeyCredential(settings.ModelApiKey),
            options);
        return chatClient.AsIChatClient();
    }

    private static string BuildTurnTitle(string input)
    {
        const int maximumLength = 80;
        var singleLine = input.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= maximumLength
            ? singleLine
            : $"{singleLine[..maximumLength].TrimEnd()}...";
    }

    private static int? ConvertTokenCount(long? tokenCount) =>
        tokenCount is > int.MaxValue or < int.MinValue
            ? null
            : tokenCount is null
                ? null
                : (int)tokenCount.Value;

    private static string ExtractResponseText(AgentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!string.IsNullOrWhiteSpace(response.Text))
            return response.Text.Trim();

        var messageText = string.Join(
            Environment.NewLine + Environment.NewLine,
            response.Messages
                .Select(static message => message.Text?.Trim())
                .Where(static text => !string.IsNullOrWhiteSpace(text)));

        return string.IsNullOrWhiteSpace(messageText)
            ? "(The model returned no text response.)"
            : messageText;
    }

    private string BuildConsolePrompt() => $"{BuildConsolePromptLabel()} {_powerShellCurrentLocation}> ";

    private string BuildConsolePromptLabel() => $"{BuildConsolePromptLeadSegment()} [{_verbosity}]";

    private string BuildConsolePromptLeadSegment()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ModelId))
            return _settings.ModelId.Trim();

        if (!string.IsNullOrWhiteSpace(_settings.AgentName))
            return _settings.AgentName.Trim();

        return "PS";
    }

    private void ClosePowerShellSession()
    {
        if (string.IsNullOrWhiteSpace(_powerShellSessionId))
            return;

        _hostedAgent.PowerShellSessions.CloseSession(_powerShellSessionId);
        _powerShellSessionId = null;
        _powerShellCurrentLocation = _settings.WorkspacePath;
    }

    private static void DrainPendingConsoleInput()
    {
        if (Console.IsInputRedirected)
            return;

        while (Console.KeyAvailable)
            _ = Console.ReadKey(intercept: true);
    }

    private void EnsurePowerShellSession()
    {
        if (!string.IsNullOrWhiteSpace(_powerShellSessionId))
            return;

        var session = _hostedAgent.PowerShellSessions.CreateSession(_settings.WorkspacePath);
        if (!session.Success || string.IsNullOrWhiteSpace(session.SessionId))
        {
            throw new InvalidOperationException(
                $"Failed to create the Director host PowerShell session: {session.ErrorMessage ?? "Unknown error."}");
        }

        _powerShellSessionId = session.SessionId;
        _powerShellCurrentLocation = session.CurrentLocation ?? _settings.WorkspacePath;
    }

    private const int MaxInputHistoryEntries = 500;
    private const string ConsoleStateFileName = "director-agent-console-state.json";
    private const string LegacyChatHistoryFileName = "director-agent-chat-history.json";

    private static readonly JsonSerializerOptions s_consoleStateJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private string GetMcpServerDirectoryPath() =>
        Path.Combine(Path.GetFullPath(_settings.WorkspacePath), ".mcpServer");

    private string GetConsoleStateStoragePath() =>
        Path.Combine(GetMcpServerDirectoryPath(), ConsoleStateFileName);

    private string GetLegacyChatHistoryStoragePath() =>
        Path.Combine(GetMcpServerDirectoryPath(), LegacyChatHistoryFileName);

    private static bool IsValidPersistedVerbosity(string value) =>
        value is "concise" or "balanced" or "detailed";

    private void ApplyHistoryEntries(IEnumerable<string> lines)
    {
        _inputHistory.Clear();
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _inputHistory.Add(line);
        }

        while (_inputHistory.Count > MaxInputHistoryEntries)
            _inputHistory.RemoveAt(0);
    }

    private void LoadConsoleStateFromDisk()
    {
        var statePath = GetConsoleStateStoragePath();
        if (File.Exists(statePath))
        {
            try
            {
                var json = File.ReadAllText(statePath);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var state = JsonSerializer.Deserialize<DirectorAgentConsoleState>(json, s_consoleStateJsonOptions);
                if (state is null)
                    return;

                if (!string.IsNullOrWhiteSpace(state.Verbosity) && IsValidPersistedVerbosity(state.Verbosity))
                    _verbosity = state.Verbosity;

                ApplyHistoryEntries(state.Entries ?? []);
            }
            catch (JsonException)
            {
                // Ignore corrupt state file.
            }
            catch (IOException)
            {
            }

            return;
        }

        var legacyPath = GetLegacyChatHistoryStoragePath();
        if (!File.Exists(legacyPath))
            return;

        try
        {
            var json = File.ReadAllText(legacyPath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var list = JsonSerializer.Deserialize<List<string>>(json, s_consoleStateJsonOptions);
            if (list is null)
                return;

            ApplyHistoryEntries(list);
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void PersistConsoleStateToDisk()
    {
        try
        {
            var directory = GetMcpServerDirectoryPath();
            Directory.CreateDirectory(directory);

            var state = new DirectorAgentConsoleState
            {
                Verbosity = _verbosity,
                Entries = [.._inputHistory],
            };

            var path = GetConsoleStateStoragePath();
            var json = JsonSerializer.Serialize(state, s_consoleStateJsonOptions);
            File.WriteAllText(path, json);

            var legacyPath = GetLegacyChatHistoryStoragePath();
            if (File.Exists(legacyPath))
            {
                try
                {
                    File.Delete(legacyPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string? ReadConsoleLine(string prompt, CancellationToken cancellationToken, bool useChatHistory)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var buffer = new StringBuilder();
        var previousRenderedLength = prompt.Length;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var line = buffer.ToString();
                    if (useChatHistory && !string.IsNullOrWhiteSpace(line))
                    {
                        if (_inputHistory.Count == 0 || _inputHistory[^1] != line)
                            _inputHistory.Add(line);
                        while (_inputHistory.Count > MaxInputHistoryEntries)
                            _inputHistory.RemoveAt(0);
                        PersistConsoleStateToDisk();
                    }

                    _historyBrowseIndex = -1;
                    _historyScratchLine = "";
                    return line;

                case ConsoleKey.Backspace:
                    if (buffer.Length == 0)
                        continue;

                    buffer.Length--;
                    Console.Write("\b \b");
                    previousRenderedLength = prompt.Length + buffer.Length;
                    continue;

                case ConsoleKey.UpArrow when useChatHistory && _inputHistory.Count > 0:
                    if (_historyBrowseIndex == -1)
                        _historyScratchLine = buffer.ToString();

                    if (_historyBrowseIndex < 0)
                        _historyBrowseIndex = _inputHistory.Count - 1;
                    else if (_historyBrowseIndex > 0)
                        _historyBrowseIndex--;

                    buffer.Clear();
                    buffer.Append(_inputHistory[_historyBrowseIndex]);
                    RewritePromptInputLine(prompt, buffer, ref previousRenderedLength);
                    continue;

                case ConsoleKey.DownArrow when useChatHistory && _inputHistory.Count > 0:
                    if (_historyBrowseIndex < 0)
                        continue;

                    if (_historyBrowseIndex < _inputHistory.Count - 1)
                    {
                        _historyBrowseIndex++;
                        buffer.Clear();
                        buffer.Append(_inputHistory[_historyBrowseIndex]);
                    }
                    else
                    {
                        _historyBrowseIndex = -1;
                        buffer.Clear();
                        buffer.Append(_historyScratchLine);
                    }

                    RewritePromptInputLine(prompt, buffer, ref previousRenderedLength);
                    continue;

                default:
                    if (char.IsControl(key.KeyChar))
                        continue;

                    if (useChatHistory && _historyBrowseIndex >= 0)
                    {
                        _historyBrowseIndex = -1;
                        _historyScratchLine = "";
                    }

                    buffer.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                    previousRenderedLength = prompt.Length + buffer.Length;
                    continue;
            }
        }
    }

    private static void RewritePromptInputLine(string prompt, StringBuilder buffer, ref int previousTotalLength)
    {
        var text = buffer.ToString();
        var totalLen = prompt.Length + text.Length;
        Console.Write('\r');
        var clearCount = Math.Max(Math.Max(previousTotalLength, totalLen), 1);
        try
        {
            var windowWidth = Console.WindowWidth;
            if (windowWidth > 1)
                clearCount = Math.Min(clearCount, windowWidth - 1);
        }
        catch (IOException)
        {
            // Best effort if the console has no window buffer (e.g. some hosted terminals).
        }

        Console.Write(new string(' ', clearCount));
        Console.Write('\r');
        Console.Write(prompt);
        Console.Write(text);
        previousTotalLength = totalLen;
    }

    private static bool TryGetDirectPowerShellCommand(string input, out string command)
    {
        if (input.StartsWith("! ", StringComparison.Ordinal))
        {
            command = input[2..];
            return !string.IsNullOrWhiteSpace(command);
        }

        command = string.Empty;
        return false;
    }

    private static void WritePowerShellResult(PowerShellSessionCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var wroteAnyOutput = false;
        wroteAnyOutput |= WriteConsoleBlock(result.Output, Console.Out);
        wroteAnyOutput |= WriteConsoleBlock(result.InformationOutput, Console.Out);
        wroteAnyOutput |= WriteConsoleBlock(result.WarningOutput, Console.Out);
        wroteAnyOutput |= WriteConsoleBlock(result.VerboseOutput, Console.Out);
        wroteAnyOutput |= WriteConsoleBlock(result.DebugOutput, Console.Out);
        wroteAnyOutput |= WriteConsoleBlock(result.ErrorOutput, Console.Error);

        if (wroteAnyOutput)
            Console.WriteLine();
    }

    private static bool WriteConsoleBlock(string? value, TextWriter writer)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        writer.WriteLine(value.TrimEnd());
        return true;
    }

    private void WriteBanner()
    {
        Console.WriteLine("MCP Agent host (Director)");
        Console.WriteLine("-----------------------------------");
        Console.WriteLine($"MCP server : {_settings.BaseUrl}");
        Console.WriteLine($"Workspace  : {_settings.WorkspacePath}");
        Console.WriteLine($"Model      : {_settings.ModelId}");
        if (_settings.ModelEndpoint is not null)
            Console.WriteLine($"Model URL  : {_settings.ModelEndpoint}");
        Console.WriteLine($"SourceType : {_settings.SourceType}");
        Console.WriteLine($"Tools      : {string.Join(", ", _hostedAgent.Registration.Tools.Select(static tool => tool.Name))}");
        Console.WriteLine();
    }

    private void WriteHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  /help              Show this help text.");
        Console.WriteLine("  /tools             List the MCP-backed tools attached to the hosted agent.");
        Console.WriteLine("  /session           Show the current MCP session-log identifier.");
        Console.WriteLine("  /v N               Set verbosity level (1=concise, 2=balanced, 3=detailed).");
        Console.WriteLine("  /new               Start a fresh conversation and session log.");
        Console.WriteLine("  /exit              Exit the Director agent host.");
        Console.WriteLine();
        Console.WriteLine("REPL workflow commands:");
        Console.WriteLine("  /todo              List all TODO items.");
        Console.WriteLine("  /todo <keyword>    Search TODOs by keyword.");
        Console.WriteLine("  /todo select <id>  Select a TODO as the active context.");
        Console.WriteLine("  /todo get <id>     Show TODO details.");
        Console.WriteLine("  /requirements      List functional requirements summary.");
        Console.WriteLine("  /reqs              Alias for /requirements.");
        Console.WriteLine("  /client <c>.<m>    Invoke McpServerClient sub-client method (e.g. /client context.SearchAsync).");
        Console.WriteLine();
        Console.WriteLine("Prompt behavior:");
        Console.WriteLine("  - The prompt shows model [verbosity] <location>> (model id from MCP_AGENT_MODEL_NAME or default).");
        Console.WriteLine("  - ↑ / ↓ recall previous inputs; history and verbosity persist in .mcpServer/director-agent-console-state.json.");
        Console.WriteLine("  - Prefix a line with ! to run it directly in the local PowerShell session.");
        Console.WriteLine("  - Any line without ! is sent to the hosted agent as a normal chat prompt.");
        Console.WriteLine();
        Console.WriteLine("You can also execute a single prompt non-interactively:");
        Console.WriteLine(@"  director agent ""List open TODO items""");
        Console.WriteLine(@"  director agent ""! Get-Location""");
        Console.WriteLine();
    }

    private void WriteToolList()
    {
        Console.WriteLine("Attached MCP tools:");
        foreach (var tool in _hostedAgent.Registration.Tools)
            Console.WriteLine($"  - {tool.Name}");

        var replTools = _runOptions.ChatOptions?.Tools?
            .Where(t => t.Name.StartsWith("repl_", StringComparison.Ordinal))
            .ToList();
        if (replTools is { Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("REPL workflow tools:");
            foreach (var tool in replTools)
                Console.WriteLine($"  - {tool.Name}");
        }

        Console.WriteLine();
    }

    private async Task HandleTodoCommandAsync(string input, CancellationToken cancellationToken)
    {
        var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var subCommand = parts.Length > 1 ? parts[1] : null;

        if (string.Equals(subCommand, "select", StringComparison.OrdinalIgnoreCase) && parts.Length > 2)
        {
            await _replTodo.SelectAsync(parts[2], cancellationToken).ConfigureAwait(false);
            var sel = _replTodo.CurrentSelection();
            Console.WriteLine($"Selected: {sel?.Id} — {sel?.Title} [{sel?.Priority}]");
            Console.WriteLine();
            return;
        }

        if (string.Equals(subCommand, "get", StringComparison.OrdinalIgnoreCase) && parts.Length > 2)
        {
            var item = await _replTodo.GetAsync(parts[2], cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"{item.Id}  {item.Title}");
            Console.WriteLine($"  Section: {item.Section}  Priority: {item.Priority}  Done: {item.Done}");
            if (!string.IsNullOrWhiteSpace(item.Estimate)) Console.WriteLine($"  Estimate: {item.Estimate}");
            if (item.Description.Count > 0) Console.WriteLine($"  Description: {string.Join(" ", item.Description)}");
            Console.WriteLine();
            return;
        }

        // Default: query with optional keyword
        var keyword = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;
        var result = await _replTodo.QueryAsync(keyword: keyword, cancellationToken: cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"TODOs ({result.TotalCount} total):");
        foreach (var todo in result.Items)
        {
            var done = todo.Done ? "[x]" : "[ ]";
            Console.WriteLine($"  {done} {todo.Id,-25} {todo.Priority,-8} {todo.Title}");
        }
        Console.WriteLine();
    }

    private async Task HandleRequirementsCommandAsync(string input, CancellationToken cancellationToken)
    {
        var result = await _replRequirements.ListFrAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Functional Requirements ({result.TotalCount} total):");
        foreach (var fr in result.Items)
            Console.WriteLine($"  {fr.Id,-20} {fr.Status,-12} {fr.Title}");
        Console.WriteLine();
    }

    private async Task HandleClientCommandAsync(string input, CancellationToken cancellationToken)
    {
        // Format: /client <clientName>.<methodName> [json-args]
        var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[1].Contains('.'))
        {
            Console.Error.WriteLine("Usage: /client <clientName>.<methodName> [json-args]");
            Console.Error.WriteLine("Example: /client context.SearchAsync {\"query\":\"auth\"}");
            Console.Error.WriteLine();
            return;
        }

        var dotIndex = parts[1].IndexOf('.');
        var clientName = parts[1][..dotIndex];
        var methodName = parts[1][(dotIndex + 1)..];
        var argsJson = parts.Length > 2 ? parts[2] : null;

        var arguments = string.IsNullOrWhiteSpace(argsJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson) ?? new Dictionary<string, object?>();

        var result = await _replPassthrough.InvokeAsync(clientName, methodName, arguments, cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        Console.WriteLine();
    }

    private void WriteSessionSummary()
    {
        var sessionId = _hostedAgent.SessionLog.Context?.SessionId ?? "<not started>";
        Console.WriteLine($"Session log : {sessionId}");
        Console.WriteLine($"Agent name  : {_hostedAgent.Name}");
        Console.WriteLine($"Source type : {_hostedAgent.SourceType}");
        Console.WriteLine();
    }

    private static bool TryParseVerbosityLevel(string input, out string verbosity, out string errorMessage)
    {
        verbosity = string.Empty;
        errorMessage = string.Empty;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            errorMessage = "Usage: /v N where N is 1, 2, or 3.";
            return false;
        }

        verbosity = parts[1] switch
        {
            "1" => "concise",
            "2" => "balanced",
            "3" => "detailed",
            _ => string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(verbosity))
            return true;

        errorMessage = $"Unsupported verbosity level '{parts[1]}'. Use 1, 2, or 3.";
        return false;
    }

    private static void WriteExpiredAuthenticationMessage(string operation, string? serverMessage = null)
    {
        Console.Error.WriteLine(
            $"warning> MCP authentication expired while {operation}. Refresh the Director auth/session configuration and restart `director agent`.");
        if (!string.IsNullOrWhiteSpace(serverMessage))
            Console.Error.WriteLine($"warning> Server detail: {serverMessage}");
        Console.Error.WriteLine();
    }
}

file sealed class DirectorAgentConsoleState
{
    public string Verbosity { get; set; } = "";

    public List<string> Entries { get; set; } = new();
}

internal sealed record DirectorAgentSettings(
    Uri BaseUrl,
    string? ApiKey,
    string? BearerToken,
    bool RequireAuthentication,
    string WorkspacePath,
    string AgentId,
    string AgentName,
    string AgentDescription,
    string SourceType,
    string ModelId,
    Uri? ModelEndpoint,
    string ModelApiKey,
    TimeSpan ModelNetworkTimeout,
    int ModelMaxRetries,
    string Verbosity,
    string SessionTitle,
    string SystemPrompt)
{
    private const string ApiKeyEnvironmentVariable = "MCP_SERVER_API_KEY";
    private const string BearerTokenEnvironmentVariable = "MCP_SERVER_BEARER_TOKEN";
    private const string WorkspacePathEnvironmentVariable = "MCP_SERVER_WORKSPACE_PATH";
    private const string AgentIdEnvironmentVariable = "MCP_AGENT_ID";
    private const string AgentNameEnvironmentVariable = "MCP_AGENT_NAME";
    private const string AgentDescriptionEnvironmentVariable = "MCP_AGENT_DESCRIPTION";
    private const string SourceTypeEnvironmentVariable = "MCP_AGENT_SOURCE_TYPE";
    private const string ModelNameEnvironmentVariable = "MCP_AGENT_MODEL_NAME";
    private const string ModelUrlEnvironmentVariable = "MCP_AGENT_MODEL_URL";
    private const string ModelApiKeyEnvironmentVariable = "MCP_AGENT_API_KEY";
    private const string ModelMaxRetriesEnvironmentVariable = "OPENAI_MAX_RETRIES";
    private const string VerbosityEnvironmentVariable = "MCP_AGENT_VERBOSITY";
    private const string SystemPromptEnvironmentVariable = "MCP_AGENT_SYSTEM_PROMPT";

    public static DirectorAgentSettings Resolve(string? workspaceOverride)
    {
        var explicitWorkspace = Path.GetFullPath(
            string.IsNullOrWhiteSpace(workspaceOverride)
                ? Environment.CurrentDirectory
                : workspaceOverride.Trim());

        var activeWorkspaceClient = McpHttpClient.FromMarkerOnly(explicitWorkspace);
        activeWorkspaceClient?.TrySetCachedBearerToken();
        var controlClient = McpHttpClient.FromDefaultUrlOrMarker(explicitWorkspace);
        controlClient?.TrySetCachedBearerToken();

        // Prefer the workspace-marker client because it carries the authoritative
        // per-workspace API key. Fall back to the control-plane client only when no
        // marker is present in the selected workspace.
        var authoritativeClient = AgentHostCommand.PickAuthoritativeClient(activeWorkspaceClient, controlClient);
        if (authoritativeClient is null || !Uri.TryCreate(authoritativeClient.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            throw new InvalidOperationException(
                "No valid MCP base URL is configured. Run from a workspace with AGENTS-README-FIRST.yaml, set MCP_SERVER_BASE_URL, or use `director config set-default-url <url>`.");
        }

        var bearerToken = FirstNonEmpty(
            ReadEnvironmentVariable(BearerTokenEnvironmentVariable),
            TryLoadCachedBearerToken());
        var apiKey = string.IsNullOrWhiteSpace(bearerToken)
            ? FirstNonEmpty(
                ReadEnvironmentVariable(ApiKeyEnvironmentVariable),
                authoritativeClient.ApiKey)
            : null;
        var requireAuthentication = !string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(bearerToken);

        var workspacePath = FirstNonEmpty(
            ReadEnvironmentVariable(WorkspacePathEnvironmentVariable),
            activeWorkspaceClient?.WorkspacePath,
            authoritativeClient.WorkspacePath,
            explicitWorkspace)!;

        var modelId = FirstNonEmpty(
            ReadEnvironmentVariable(ModelNameEnvironmentVariable),
            "gpt-5.4")!;
        var modelEndpoint = ResolveOptionalModelEndpoint(
            FirstNonEmpty(
                ReadEnvironmentVariable(ModelUrlEnvironmentVariable),
                ReadEnvironmentVariable("OPENAI_BASE_URL")));
        var modelApiKey = ReadEnvironmentVariable(ModelApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(modelApiKey))
        {
            throw new InvalidOperationException(
                $"Configure {ModelApiKeyEnvironmentVariable} before running `director agent`.");
        }

        return new DirectorAgentSettings(
            baseUrl,
            apiKey,
            bearerToken,
            requireAuthentication,
            workspacePath,
            FirstNonEmpty(ReadEnvironmentVariable(AgentIdEnvironmentVariable), McpHostedAgentDefaults.DefaultAgentId)!,
            FirstNonEmpty(ReadEnvironmentVariable(AgentNameEnvironmentVariable), McpHostedAgentDefaults.DefaultAgentName)!,
            FirstNonEmpty(ReadEnvironmentVariable(AgentDescriptionEnvironmentVariable), "Interactive console chat host for McpServer.McpAgent inside Director.")!,
            FirstNonEmpty(ReadEnvironmentVariable(SourceTypeEnvironmentVariable), McpHostedAgentDefaults.DefaultSourceType)!,
            modelId,
            modelEndpoint,
            modelApiKey,
            ResolvePositiveTimeSpanSeconds(
                ReadEnvironmentVariable("OPENAI_NETWORK_TIMEOUT_SECONDS"),
                100,
                "OpenAI network timeout"),
            ResolveNonNegativeInt(
                ReadEnvironmentVariable(ModelMaxRetriesEnvironmentVariable),
                4,
                "OpenAI max retries"),
            ResolveVerbosity(),
            "Director MCP Agent chat",
            FirstNonEmpty(
                ReadEnvironmentVariable(SystemPromptEnvironmentVariable),
                "You are an interactive command-line assistant running inside Director-hosted McpServer.McpAgent. Be helpful and use the available MCP workflow tools whenever they help you inspect TODOs, session logs, repository context, or other MCP-backed information. " +
                "For reading or writing arbitrary paths on the machine where Director runs, use mcp_fs_read, mcp_fs_list, and mcp_fs_write (absolute paths or paths relative to the MCP workspace). Use mcp_repo_* when MCP-server allowlists and auditing should apply.")!);
    }

    internal static string BuildSystemPrompt(string basePrompt, string verbosity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePrompt);

        var sb = new StringBuilder(basePrompt.Trim());
        sb.AppendLine();
        sb.AppendLine();
        sb.Append("Response verbosity: ");
        sb.Append(verbosity);
        sb.AppendLine(".");
        sb.AppendLine(verbosity switch
        {
            "concise" => "Keep answers short by default. Prefer brief paragraphs or bullets and avoid extra detail unless the user asks for it.",
            "balanced" => "Give enough detail to be useful while staying focused. Include key reasoning and next steps without over-explaining.",
            "detailed" => "Be thorough and explicit. Include important context, reasoning, and follow-up details when they help the user.",
            _ => throw new InvalidOperationException($"Unsupported agent host verbosity '{verbosity}'."),
        });

        return sb.ToString();
    }

    private static Uri? ResolveOptionalModelEndpoint(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Scheme)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Model API base URL must be an absolute http or https URI, but was '{rawUrl}'. " +
                $"Set {ModelUrlEnvironmentVariable} or OPENAI_BASE_URL.");
        }

        return uri;
    }

    private static string ResolveVerbosity()
    {
        var configured = FirstNonEmpty(
            ReadEnvironmentVariable(VerbosityEnvironmentVariable),
            "concise")!;

        return configured.Trim().ToLowerInvariant() switch
        {
            "concise" => "concise",
            "balanced" => "balanced",
            "detailed" => "detailed",
            _ => throw new InvalidOperationException(
                $"Unsupported agent host verbosity '{configured}'. Use concise, balanced, or detailed."),
        };
    }

    private static TimeSpan ResolvePositiveTimeSpanSeconds(
        string? environmentValue,
        int defaultSeconds,
        string settingName)
    {
        var configured = FirstNonEmpty(environmentValue);
        if (configured is null)
            return TimeSpan.FromSeconds(defaultSeconds);

        if (!int.TryParse(configured, out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException(
                $"{settingName} must be a positive integer number of seconds, but was '{configured}'.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static int ResolveNonNegativeInt(
        string? environmentValue,
        int defaultValue,
        string settingName)
    {
        var configured = FirstNonEmpty(environmentValue);
        if (configured is null)
            return defaultValue;

        if (!int.TryParse(configured, out var value) || value < 0)
        {
            throw new InvalidOperationException(
                $"{settingName} must be a non-negative integer, but was '{configured}'.");
        }

        return value;
    }

    internal static string? TryLoadCachedBearerToken()
    {
        McpHttpClient.TryRefreshCachedToken();
        var cached = TokenCache.Load();
        return cached is null || cached.IsExpired || string.IsNullOrWhiteSpace(cached.AccessToken)
            ? null
            : cached.AccessToken;
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ReadEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
