using McpServer.Director.Auth;
using McpServer.Director.Screens;
using McpServer.UI.Core.Authorization;
using McpServer.UI.Core.Navigation;

namespace McpServer.Director.Tests;

public sealed class MainScreenTabOrderingTests
{
    [Fact]
    public void GetVisibleTabRegistrations_AdminWithControlConnection_MovesNonWorkspaceTabsToEnd()
    {
        const bool hasWorkspace = false;
        const bool hasControl = true;
        var registrations = CreateRegistrations(hasWorkspace, hasControl);
        var policy = new DirectorAuthorizationPolicyService(new FakeRoleContext(roles: [McpRoles.Admin]));

        var ordered = MainScreen.GetVisibleTabRegistrations(registrations, policy, EmptyServiceProvider.Instance);

        Assert.Equal(
        [
            "TODO",
            "Sessions",
            "Agents",
            "Agent Pool",
            "Chat",
            "Tunnels",
            "Templates",
            "Context",
            "Repo",
            "Tools",
            "GitHub",
            "Requirements",
            "Events",
            "Health",
            "Workspaces",
            "Policy",
            "Logs",
            "Config",
        ], ordered.Select(registration => registration.DisplayText));
    }

    [Fact]
    public void GetVisibleTabRegistrations_AgentManagerWithWorkspaceOnly_LeavesLogsLast()
    {
        const bool hasWorkspace = true;
        const bool hasControl = false;
        var registrations = CreateRegistrations(hasWorkspace, hasControl);
        var policy = new DirectorAuthorizationPolicyService(new FakeRoleContext(roles: [McpRoles.AgentManager]));

        var ordered = MainScreen.GetVisibleTabRegistrations(registrations, policy, EmptyServiceProvider.Instance);

        Assert.Equal(
        [
            "TODO",
            "Sessions",
            "Agents",
            "Agent Pool",
            "Chat",
            "Tunnels",
            "Templates",
            "Context",
            "Repo",
            "Tools",
            "GitHub",
            "Requirements",
            "Events",
            "Logs",
        ], ordered.Select(registration => registration.DisplayText));
    }

    [Fact]
    public void GetVisibleTabRegistrations_ViewerWithControlConnection_HidesAdminTabsButKeepsTrailingOrder()
    {
        const bool hasWorkspace = false;
        const bool hasControl = true;
        var registrations = CreateRegistrations(hasWorkspace, hasControl);
        var policy = new DirectorAuthorizationPolicyService(new FakeRoleContext(roles: [McpRoles.Viewer]));

        var ordered = MainScreen.GetVisibleTabRegistrations(registrations, policy, EmptyServiceProvider.Instance);

        Assert.Equal(
        [
            "TODO",
            "Sessions",
            "Tunnels",
            "Templates",
            "Context",
            "Repo",
            "Tools",
            "GitHub",
            "Requirements",
            "Events",
            "Health",
            "Logs",
        ], ordered.Select(registration => registration.DisplayText));
    }

    private static IReadOnlyList<TabRegistration> CreateRegistrations(bool hasWorkspace, bool hasControl)
    {
        bool HasWorkspaceOrControl(IServiceProvider _) => hasWorkspace || hasControl;
        bool HasControlConnection(IServiceProvider _) => hasControl;
        bool Always(IServiceProvider _) => true;

        return
        [
            new(McpArea.Todo, "TODO", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.SessionLogs, "Sessions", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Health, "Health", McpRoles.Viewer, _ => new object(), HasControlConnection, TabPlacementGroup.NonWorkspaceTrailing),
            new(McpArea.Workspaces, "Workspaces", McpRoles.Admin, _ => new object(), HasControlConnection, TabPlacementGroup.NonWorkspaceTrailing),
            new(McpArea.Agents, "Agents", McpRoles.AgentManager, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Agents, "Agent Pool", McpRoles.AgentManager, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Agents, "Chat", McpRoles.AgentManager, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Policy, "Policy", McpRoles.Admin, _ => new object(), HasControlConnection, TabPlacementGroup.NonWorkspaceTrailing),
            new(McpArea.Tunnels, "Tunnels", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Templates, "Templates", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Context, "Context", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Repo, "Repo", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.ToolRegistry, "Tools", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.GitHub, "GitHub", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Requirements, "Requirements", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.Events, "Events", McpRoles.Viewer, _ => new object(), HasWorkspaceOrControl),
            new(McpArea.DispatcherLogs, "Logs", McpRoles.Viewer, _ => new object(), Always, TabPlacementGroup.NonWorkspaceTrailing),
            new(McpArea.Configuration, "Config", McpRoles.Admin, _ => new object(), HasControlConnection, TabPlacementGroup.NonWorkspaceTrailing),
        ];
    }

    private sealed class FakeRoleContext : IRoleContext
    {
        private readonly HashSet<string> _roles;

        public FakeRoleContext(bool authenticated = true, IReadOnlyList<string>? roles = null)
        {
            IsAuthenticated = authenticated;
            Roles = roles ?? [];
            _roles = new HashSet<string>(Roles, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsAuthenticated { get; }

        public IReadOnlyList<string> Roles { get; }

        public bool HasRole(string role) => _roles.Contains(role);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
