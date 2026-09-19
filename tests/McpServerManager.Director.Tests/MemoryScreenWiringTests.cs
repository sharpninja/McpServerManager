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
        Assert.Equal(typeof(MemoryScreen).Namespace, "McpServerManager.Director.Screens");
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
