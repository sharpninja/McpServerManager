using Bunit;
using Bunit.TestDoubles;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.Web.Components.Layout;
using McpServerManager.Web.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class NavigationAuthTests
{
    [Fact]
    public void NavMenu_WithoutAuth_NavigatesToPublicFeaturesAndHidesRoleGatedFeatures()
    {
        using var ctx = CreateContext(CreateAnonymousPrincipal());

        var cut = RenderNavMenu(ctx);

        FollowLinkAndAssertPath(ctx, cut, "/dashboard");
        FollowLinkAndAssertPath(ctx, cut, "/workspaces");
        FollowLinkAndAssertPath(ctx, cut, "/todos");
        FollowLinkAndAssertPath(ctx, cut, "/sessions");
        FollowLinkAndAssertPath(ctx, cut, "/templates");
        FollowLinkAndAssertPath(ctx, cut, "/context/search");
        FollowLinkAndAssertPath(ctx, cut, "/health-dashboard");
        AssertLinkMissing(cut, "/triage");
        AssertLinkMissing(cut, "/auth/config");
    }

    [Fact]
    public void Routes_WithoutAuth_NavigatingToTriageRedirectsToLogin()
    {
        using var ctx = CreateContext(CreateAnonymousPrincipal());
        var navigation = ctx.Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/triage");

        var cut = RenderRoutes(ctx);

        cut.WaitForAssertion(() =>
        {
            var current = new Uri(navigation.Uri);
            Assert.Equal("/login", current.AbsolutePath);
            Assert.Contains("returnUrl=", current.Query, StringComparison.Ordinal);
            Assert.Contains(Uri.EscapeDataString("http://localhost/triage"), current.Query, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MainLayout_WithoutAuth_NavigatesToSignInAndHidesRoleGatedFeatures()
    {
        using var ctx = CreateContext(CreateAnonymousPrincipal());

        var cut = RenderMainLayout(ctx);

        FollowHeaderLinkAndAssertPath(ctx, cut, "/login");
        AssertLinkMissing(cut, "/triage");
        AssertLinkMissing(cut, "/auth/config");
        Assert.Empty(cut.FindAll("a.Header-link[href='/logout']"));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("agent-manager")]
    public void NavMenu_WithAuthorizedRole_NavigatesToRoleGatedFeatures(string role)
    {
        using var ctx = CreateContext(CreatePrincipal("mcp-user", role));

        var cut = RenderNavMenu(ctx);

        FollowLinkAndAssertPath(ctx, cut, "/triage");
        FollowLinkAndAssertPath(ctx, cut, "/auth/config");
    }

    [Fact]
    public void MainLayout_WithAuthorizedRole_NavigatesToSignOutAndRoleGatedFeatures()
    {
        using var ctx = CreateContext(CreatePrincipal("mcp-user", "admin"));

        var cut = RenderMainLayout(ctx);

        Assert.Contains("mcp-user", cut.Markup, StringComparison.Ordinal);
        FollowHeaderLinkAndAssertPath(ctx, cut, "/logout");
        FollowLinkAndAssertPath(ctx, cut, "/triage");
        FollowLinkAndAssertPath(ctx, cut, "/auth/config");
        Assert.Empty(cut.FindAll("a.Header-link[href='/login']"));
    }

    [Fact]
    public void MainLayout_BackButton_InvokesBrowserHistoryBack()
    {
        using var ctx = CreateContext(CreatePrincipal("mcp-user", "admin"));

        var cut = RenderMainLayout(ctx);
        cut.Find("#app-back-button").Click();

        Assert.Contains(ctx.JSInterop.Invocations, invocation => invocation.Identifier == "mcpServerAppShell.goBack");
    }

    [Fact]
    public void MainLayout_CtrlW_OpensWorkspacePicker_AndSelectionInvalidatesWorkspaceState()
    {
        using var ctx = CreateContext(
            CreatePrincipal("mcp-user", "admin"),
            services => services.AddSingleton<IWorkspaceApiClient>(new LayoutWorkspaceApiClientStub(
                new WorkspaceSummary(@"E:\repo", "Repo", true, true),
                new WorkspaceSummary(@"E:\next", "Next", true, true))));
        var workspaceContext = ctx.Services.GetRequiredService<WorkspaceContextViewModel>();

        var cut = RenderMainLayout(ctx);
        var initialVersion = cut.Find("main.app-content").GetAttribute("data-workspace-version");

        cut.Find("[data-app-shell]").KeyDown(new KeyboardEventArgs { Key = "w", CtrlKey = true });

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#workspace-picker-panel select")));
        cut.Find("#workspace-picker-panel select").Change(@"E:\next");

        cut.WaitForAssertion(() => Assert.Equal(@"E:\next", workspaceContext.ActiveWorkspacePath));
        cut.WaitForAssertion(() => Assert.NotEqual(initialVersion, cut.Find("main.app-content").GetAttribute("data-workspace-version")));
    }

    [Fact]
    public void NavMenu_WithAuthenticatedUserWithoutRequiredRole_NavigatesToPublicFeaturesAndHidesRoleGatedFeatures()
    {
        using var ctx = CreateContext(CreatePrincipal("viewer-user", "viewer"));

        var cut = RenderNavMenu(ctx);

        FollowLinkAndAssertPath(ctx, cut, "/todos");
        FollowLinkAndAssertPath(ctx, cut, "/templates");
        AssertLinkMissing(cut, "/triage");
        AssertLinkMissing(cut, "/auth/config");
    }

    private static BunitContext CreateContext(ClaimsPrincipal user, Action<IServiceCollection>? configureServices = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = "test-api-key",
                ["McpServer:WorkspacePath"] = @"E:\\repo"
            })
            .Build();

        ctx.Services.AddSingleton<IConfiguration>(config);
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ctx.Services.AddWebServices();
        ctx.Services.AddSingleton<IHealthApiClient>(new HealthApiClientStub());
        ctx.Services.AddSingleton<IWorkspaceApiClient>(new WorkspaceApiClientStub());
        configureServices?.Invoke(ctx.Services);
        ctx.Services.AddAuthorization();
        ctx.Services.RemoveAll<IAuthorizationService>();
        ctx.Services.AddSingleton<IAuthorizationService, TestAuthorizationService>();
        ctx.Services.AddSingleton<AuthenticationStateProvider>(new FixedAuthenticationStateProvider(user));
        ctx.Services.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = @"E:\repo";
        return ctx;
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderNavMenu(BunitContext ctx)
        => ctx.Render<CascadingAuthenticationState>(parameters => parameters.Add(
            component => component.ChildContent,
            (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            })));

    private static IRenderedComponent<CascadingAuthenticationState> RenderMainLayout(BunitContext ctx)
        => ctx.Render<CascadingAuthenticationState>(parameters => parameters.Add(
            component => component.ChildContent,
            (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<MainLayout>(0);
                childBuilder.AddAttribute(1, "Body", (RenderFragment)(bodyBuilder =>
                {
                    bodyBuilder.AddMarkupContent(0, "<p>Body</p>");
                }));
                childBuilder.CloseComponent();
            })));

    private static IRenderedComponent<CascadingAuthenticationState> RenderRoutes(BunitContext ctx)
        => ctx.Render<CascadingAuthenticationState>(parameters => parameters.Add(
            component => component.ChildContent,
            (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<Routes>(0);
                childBuilder.CloseComponent();
            })));

    private static ClaimsPrincipal CreateAnonymousPrincipal()
        => new(new ClaimsIdentity());

    private static ClaimsPrincipal CreatePrincipal(string name, string role)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, role)],
            "Test",
            ClaimTypes.Name,
            ClaimTypes.Role));

    private static void FollowLinkAndAssertPath(
        BunitContext ctx,
        IRenderedComponent<CascadingAuthenticationState> cut,
        string href)
    {
        var link = cut.Find($"a.header-nav-tab[href='{href}']");

        var navigation = ctx.Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo(link.GetAttribute("href")!);
        Assert.Equal(href, new Uri(navigation.Uri).AbsolutePath);
    }

    private static void FollowHeaderLinkAndAssertPath(
        BunitContext ctx,
        IRenderedComponent<CascadingAuthenticationState> cut,
        string href)
    {
        var link = cut.Find($"a.Header-link[href='{href}']");

        var navigation = ctx.Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo(link.GetAttribute("href")!);
        Assert.Equal(href, new Uri(navigation.Uri).AbsolutePath);
    }

    private static void AssertLinkMissing(IRenderedComponent<CascadingAuthenticationState> cut, string href)
        => Assert.Empty(cut.FindAll($"a.header-nav-tab[href='{href}']"));

    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public FixedAuthenticationStateProvider(ClaimsPrincipal user)
        {
            _state = new AuthenticationState(user);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);
    }

    private sealed class TestAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            foreach (var requirement in requirements)
            {
                if (requirement is DenyAnonymousAuthorizationRequirement &&
                    user.Identity?.IsAuthenticated != true)
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }

                if (requirement is RolesAuthorizationRequirement rolesRequirement &&
                    !rolesRequirement.AllowedRoles.Any(user.IsInRole))
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }
            }

            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class LayoutWorkspaceApiClientStub(params WorkspaceSummary[] workspaces) : IWorkspaceApiClient
    {
        public Task<ListWorkspacesResult> ListWorkspacesAsync(CancellationToken ct = default)
            => Task.FromResult(new ListWorkspacesResult(workspaces, workspaces.Length));

        public Task<WorkspaceDetail?> GetWorkspaceAsync(string workspacePath, CancellationToken ct = default) => Task.FromResult<WorkspaceDetail?>(null);
        public Task<bool> UpdateWorkspacePolicyAsync(UpdateWorkspacePolicyCommand command, CancellationToken ct = default) => Task.FromResult(false);
        public Task<WorkspaceMutationOutcome> CreateWorkspaceAsync(CreateWorkspaceCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceMutationOutcome> UpdateWorkspaceAsync(UpdateWorkspaceCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceMutationOutcome> DeleteWorkspaceAsync(DeleteWorkspaceCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceProcessState> GetWorkspaceStatusAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceProcessState> StartWorkspaceAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceProcessState> StopWorkspaceAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceHealthState> CheckWorkspaceHealthAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceGlobalPromptState> GetWorkspaceGlobalPromptAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceGlobalPromptState> UpdateWorkspaceGlobalPromptAsync(UpdateWorkspaceGlobalPromptCommand command, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkspaceInitInfo> InitWorkspaceAsync(string workspacePath, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
