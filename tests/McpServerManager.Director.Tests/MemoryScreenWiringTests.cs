using McpServerManager.Director;
using McpServerManager.Director.Auth;
using McpServerManager.Director.Screens;
using McpServerManager.UI.Core.Authorization;
using Xunit;

namespace McpServerManager.Director.Tests;

public sealed class MemoryScreenWiringTests
{
    [Fact]
    public void MemoryScreen_CanBeConstructed_WithListAndDetailViewModels()
    {
        Assert.Equal("McpServerManager.Director.Screens", typeof(MemoryScreen).Namespace);
        Assert.Contains(
            typeof(MemoryScreen).GetConstructors().SelectMany(c => c.GetParameters().Select(p => p.ParameterType.Name)),
            name => name is "MemoryListViewModel" or "MemoryDetailViewModel");
    }

    [Fact]
    public void Viewer_CanViewMemoryArea_AndExecuteFiveVerbs()
    {
        var policy = new DirectorAuthorizationPolicyService(new FakeRoleContext(roles: [McpRoles.Viewer]));

        Assert.True(policy.CanViewArea(McpArea.Memory));
        Assert.Equal(McpRoles.Viewer, policy.GetRequiredRole(McpArea.Memory));
        Assert.Equal(McpRoles.Viewer, policy.GetRequiredRole(McpActionKeys.MemoryList));
        Assert.Equal(McpRoles.Viewer, policy.GetRequiredRole(McpActionKeys.MemoryGet));
        Assert.Equal(McpRoles.Viewer, policy.GetRequiredRole(McpActionKeys.MemoryAdd));
        Assert.Equal(McpRoles.Viewer, policy.GetRequiredRole(McpActionKeys.MemoryUpdate));
        Assert.Equal(McpRoles.Viewer, policy.GetRequiredRole(McpActionKeys.MemoryRemove));
        Assert.True(policy.CanExecuteAction(McpActionKeys.MemoryList));
        Assert.True(policy.CanExecuteAction(McpActionKeys.MemoryGet));
        Assert.True(policy.CanExecuteAction(McpActionKeys.MemoryAdd));
        Assert.True(policy.CanExecuteAction(McpActionKeys.MemoryUpdate));
        Assert.True(policy.CanExecuteAction(McpActionKeys.MemoryRemove));
    }

    [Fact]
    public void TryClearActiveWorkspace_ReturnsToDefaultWorkspace()
    {
        var control = new McpHttpClient("http://127.0.0.1:7147", "key", string.Empty);
        var active = new McpHttpClient("http://127.0.0.1:7147", "key", "/tmp/ws");
        using var context = new DirectorMcpContext(control, active);
        var cleared = 0;
        context.ActiveWorkspaceChanged += (_, _) => cleared++;

        Assert.Equal("/tmp/ws", context.ActiveWorkspacePath);
        Assert.True(context.TryClearActiveWorkspace(out var error));

        Assert.Null(error);
        Assert.Null(context.ActiveWorkspacePath);
        Assert.False(context.HasActiveWorkspaceConnection);
        Assert.True(context.HasControlConnection);
        Assert.Equal(1, cleared);
    }

    private sealed class FakeRoleContext : IRoleContext
    {
        private readonly HashSet<string> _roles;

        public FakeRoleContext(IReadOnlyList<string>? roles = null)
        {
            Roles = roles ?? [];
            _roles = new HashSet<string>(Roles, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsAuthenticated => true;

        public IReadOnlyList<string> Roles { get; }

        public bool HasRole(string role) => _roles.Contains(role);
    }
}
