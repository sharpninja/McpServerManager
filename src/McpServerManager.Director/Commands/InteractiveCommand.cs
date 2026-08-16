using System.CommandLine;
using McpServer.Cqrs;
using McpServer.Cqrs.Mvvm;
using McpServerManager.Director.Helpers;
using McpServerManager.Director.Screens;
using McpServerManager.UI.Core.Authorization;
using McpServerManager.UI.Core.Navigation;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpServerManager.Director.Commands;

/// <summary>
/// FR-MCP-030: Interactive TUI command that launches Terminal.Gui with ViewModel-bound screens.
/// All Director functions are available as Terminal.Gui tabs including auth.
/// </summary>
internal static class InteractiveCommand
{
    private static readonly Option<string?> s_workspaceOption = new("--workspace", "-w")
    {
        Description = "Workspace path (defaults to current directory)",
    };

    /// <summary>Registers the interactive command on the root command.</summary>
    public static void Register(RootCommand root)
    {
        var cmd = new Command("interactive", "Launch interactive Terminal UI for workspace management")
        {
            s_workspaceOption,
        };
        cmd.Aliases.Add("tui");
        cmd.Aliases.Add("ui");

        cmd.SetAction(parseResult =>
        {
            var workspace = parseResult.GetValue(s_workspaceOption);
            using var sp = DirectorHost.CreateProvider(workspace);
            var directorContext = sp.GetRequiredService<DirectorMcpContext>();

            // Resolve ViewModels
            var workspaceListVm = sp.GetRequiredService<WorkspaceListViewModel>();
            var workspaceDetailVm = sp.GetRequiredService<WorkspaceDetailViewModel>();
            var workspacePolicyVm = sp.GetRequiredService<WorkspacePolicyViewModel>();
            var healthVm = sp.GetRequiredService<HealthSnapshotsViewModel>();
            var dispatcherLogsVm = sp.GetRequiredService<DispatcherLogsViewModel>();
            var sessionLogVm = sp.GetRequiredService<SessionLogListViewModel>();
            var sessionLogDetailVm = sp.GetRequiredService<SessionLogDetailViewModel>();
            var todoVm = sp.GetRequiredService<TodoListViewModel>();
            var todoDetailVm = sp.GetRequiredService<TodoDetailViewModel>();
            var tunnelListVm = sp.GetRequiredService<TunnelListViewModel>();
            var templateListVm = sp.GetRequiredService<TemplateListViewModel>();
            var templateDetailVm = sp.GetRequiredService<TemplateDetailViewModel>();
            var toolListVm = sp.GetRequiredService<ToolListViewModel>();
            var toolDetailVm = sp.GetRequiredService<ToolDetailViewModel>();
            var bucketListVm = sp.GetRequiredService<BucketListViewModel>();
            var bucketDetailVm = sp.GetRequiredService<BucketDetailViewModel>();
            var issueListVm = sp.GetRequiredService<IssueListViewModel>();
            var issueDetailVm = sp.GetRequiredService<IssueDetailViewModel>();
            var pullRequestListVm = sp.GetRequiredService<PullRequestListViewModel>();
            var gitHubSyncVm = sp.GetRequiredService<GitHubSyncViewModel>();
            var frListVm = sp.GetRequiredService<FrListViewModel>();
            var frDetailVm = sp.GetRequiredService<FrDetailViewModel>();
            var trListVm = sp.GetRequiredService<TrListViewModel>();
            var trDetailVm = sp.GetRequiredService<TrDetailViewModel>();
            var testListVm = sp.GetRequiredService<TestListViewModel>();
            var testDetailVm = sp.GetRequiredService<TestDetailViewModel>();
            var mappingListVm = sp.GetRequiredService<MappingListViewModel>();
            var requirementsGenerateVm = sp.GetRequiredService<RequirementsGenerateViewModel>();
            var agentDefinitionListVm = sp.GetRequiredService<AgentDefinitionListViewModel>();
            var agentDefinitionDetailVm = sp.GetRequiredService<AgentDefinitionDetailViewModel>();
            var workspaceAgentListVm = sp.GetRequiredService<WorkspaceAgentListViewModel>();
            var workspaceAgentDetailVm = sp.GetRequiredService<WorkspaceAgentDetailViewModel>();
            var agentEventsVm = sp.GetRequiredService<AgentEventsViewModel>();
            var agentPoolVm = sp.GetRequiredService<AgentPoolViewModel>();
            var eventStreamVm = sp.GetRequiredService<EventStreamViewModel>();
            var configurationVm = sp.GetRequiredService<ConfigurationViewModel>();
            var workspaceContextVm = sp.GetRequiredService<WorkspaceContextViewModel>();
            var roleContext = sp.GetRequiredService<IRoleContext>();
            var authorizationPolicy = sp.GetRequiredService<IAuthorizationPolicyService>();
            var dispatcher = sp.GetRequiredService<Dispatcher>();
            var tabRegistry = sp.GetRequiredService<ITabRegistry>();

            // Initialize Terminal.Gui
            Terminal.Gui.App.Application.Init();
            ApplyDarculaTheme();

            try
            {
                var mainScreen = new MainScreen(
                    workspaceListVm,
                    workspaceDetailVm,
                    workspacePolicyVm,
                    healthVm,
                    dispatcherLogsVm,
                    sessionLogVm,
                    sessionLogDetailVm,
                    todoVm,
                    todoDetailVm,
                    tunnelListVm,
                    templateListVm,
                    templateDetailVm,
                    toolListVm,
                    toolDetailVm,
                    bucketListVm,
                    bucketDetailVm,
                    issueListVm,
                    issueDetailVm,
                    pullRequestListVm,
                    gitHubSyncVm,
                    frListVm,
                    frDetailVm,
                    trListVm,
                    trDetailVm,
                    testListVm,
                    testDetailVm,
                    mappingListVm,
                    requirementsGenerateVm,
                    agentDefinitionListVm,
                    agentDefinitionDetailVm,
                    workspaceAgentListVm,
                    workspaceAgentDetailVm,
                    agentEventsVm,
                    agentPoolVm,
                    eventStreamVm,
                    configurationVm,
                    workspaceContextVm,
                    authorizationPolicy,
                    roleContext,
                    directorContext,
                    dispatcher,
                    tabRegistry,
                    sp,
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<IBrowserLauncher>());
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Director.UI");
                Terminal.Gui.App.Application.Run(mainScreen, (ex) =>
                {
                    // Terminal.Gui v2 WordWrapManager.WrapModel throws ArgumentOutOfRangeException
                    // on Tab key press with WordWrap enabled. Swallow the exception to keep running.
                    logger.LogWarning(ex, "Unhandled UI exception (swallowed to keep app alive)");
                    return true;
                });
            }
            finally
            {
                Terminal.Gui.App.Application.Shutdown();
                try
                {
                    Console.Clear();
                }
                catch
                {
                    // Best-effort terminal cleanup on exit.
                }
            }
        });

        root.Subcommands.Add(cmd);
    }

    /// <summary>Applies a Darcula-inspired dark color scheme to all Terminal.Gui color scheme slots.</summary>
    private static void ApplyDarculaTheme()
    {
        // Darcula palette: text brighter, borders dimmer
        var bg = new Terminal.Gui.Drawing.Color(40, 40, 40);
        var fg = new Terminal.Gui.Drawing.Color(210, 210, 210);
        var accent = new Terminal.Gui.Drawing.Color(120, 170, 210);
        var hotKey = new Terminal.Gui.Drawing.Color(220, 140, 65);
        var focusBg = new Terminal.Gui.Drawing.Color(48, 48, 48);
        var dialogBg = new Terminal.Gui.Drawing.Color(55, 57, 59);
        var menuBg = new Terminal.Gui.Drawing.Color(45, 47, 49);
        var errorFg = new Terminal.Gui.Drawing.Color(255, 120, 115);

        var baseScheme = new Terminal.Gui.Drawing.Scheme
        {
            Normal = new Terminal.Gui.Drawing.Attribute(fg, bg),
            Focus = new Terminal.Gui.Drawing.Attribute(accent, focusBg),
            HotNormal = new Terminal.Gui.Drawing.Attribute(hotKey, bg),
            HotFocus = new Terminal.Gui.Drawing.Attribute(hotKey, focusBg),
            Disabled = new Terminal.Gui.Drawing.Attribute(fg, bg),
        };

        var dialogScheme = new Terminal.Gui.Drawing.Scheme
        {
            Normal = new Terminal.Gui.Drawing.Attribute(fg, dialogBg),
            Focus = new Terminal.Gui.Drawing.Attribute(accent, focusBg),
            HotNormal = new Terminal.Gui.Drawing.Attribute(hotKey, dialogBg),
            HotFocus = new Terminal.Gui.Drawing.Attribute(hotKey, focusBg),
            Disabled = new Terminal.Gui.Drawing.Attribute(fg, dialogBg),
        };

        var menuScheme = new Terminal.Gui.Drawing.Scheme
        {
            Normal = new Terminal.Gui.Drawing.Attribute(fg, menuBg),
            Focus = new Terminal.Gui.Drawing.Attribute(accent, focusBg),
            HotNormal = new Terminal.Gui.Drawing.Attribute(hotKey, menuBg),
            HotFocus = new Terminal.Gui.Drawing.Attribute(hotKey, focusBg),
            Disabled = new Terminal.Gui.Drawing.Attribute(fg, menuBg),
        };

        var errorScheme = new Terminal.Gui.Drawing.Scheme
        {
            Normal = new Terminal.Gui.Drawing.Attribute(errorFg, bg),
            Focus = new Terminal.Gui.Drawing.Attribute(errorFg, focusBg),
            HotNormal = new Terminal.Gui.Drawing.Attribute(hotKey, bg),
            HotFocus = new Terminal.Gui.Drawing.Attribute(hotKey, focusBg),
            Disabled = new Terminal.Gui.Drawing.Attribute(errorFg, bg),
        };

        Terminal.Gui.Configuration.SchemeManager.AddScheme("Base", baseScheme);
        Terminal.Gui.Configuration.SchemeManager.AddScheme("TopLevel", baseScheme);
        Terminal.Gui.Configuration.SchemeManager.AddScheme("Dialog", dialogScheme);
        Terminal.Gui.Configuration.SchemeManager.AddScheme("Menu", menuScheme);
        Terminal.Gui.Configuration.SchemeManager.AddScheme("Error", errorScheme);

        // Editable text fields use accent blue for normal text to distinguish from labels
        var editableScheme = new Terminal.Gui.Drawing.Scheme
        {
            Normal = new Terminal.Gui.Drawing.Attribute(accent, bg),
            Focus = new Terminal.Gui.Drawing.Attribute(accent, focusBg),
            HotNormal = new Terminal.Gui.Drawing.Attribute(hotKey, bg),
            HotFocus = new Terminal.Gui.Drawing.Attribute(hotKey, focusBg),
            Disabled = new Terminal.Gui.Drawing.Attribute(fg, bg),
        };
        Terminal.Gui.Configuration.SchemeManager.AddScheme("Editable", editableScheme);
    }
}
