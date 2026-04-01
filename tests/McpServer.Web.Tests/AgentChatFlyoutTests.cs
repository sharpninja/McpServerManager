using Bunit;
using Bunit.JSInterop;
using System.Security.Claims;
using McpServerManager.UI.Core.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.Web.Components.Layout;
using McpServerManager.Web.Components.Shared;
using McpServerManager.Web.Tests.TestInfrastructure;
using McpServerManager.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class AgentChatFlyoutTests
{
    [Fact]
    public void AddWebServices_RegistersWebVoiceConversationServices_ForAgentChat()
    {
        using var provider = CreateProvider();

        var service = provider.GetRequiredService<IVoiceConversationService>();
        var viewModel = provider.GetRequiredService<WebVoiceConversationViewModel>();

        Assert.IsType<Web.Services.WebVoiceConversationService>(service);
        Assert.Equal("RequestTracker.Web", viewModel.ClientName);
    }

    [Fact]
    public void MainLayout_RendersAgentChatLaunchAndFlyoutShell()
    {
        using var ctx = CreateRenderContext();

        var cut = ctx.Render<MainLayoutHost>();
        cut.Find("#agent-chat-launch").Click();

        Assert.Contains("agent-chat-launch", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Agent Chat", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("agent-chat-flyout", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Type a message...", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_ClickingAgentChatLaunch_OpensFlyoutPanel()
    {
        using var ctx = CreateRenderContext();

        var cut = ctx.Render<MainLayoutHost>();
        var launchButton = cut.Find("#agent-chat-launch");

        Assert.Contains("translateX(100%)", cut.Find("#agent-chat-flyout").GetAttribute("style"), StringComparison.Ordinal);

        launchButton.Click();

        Assert.Contains("translateX(0)", cut.Find("#agent-chat-flyout").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_TogglingAgentChatDisplayMode_RendersInlineFlyout()
    {
        using var ctx = CreateRenderContext();

        var cut = ctx.Render<MainLayoutHost>();
        cut.Find("#agent-chat-launch").Click();

        Assert.Equal("floating", cut.Find("[data-display-mode]").GetAttribute("data-display-mode"));

        cut.Find("#agent-chat-display-mode-toggle").Click();

        Assert.Equal("inline", cut.Find("[data-display-mode]").GetAttribute("data-display-mode"));
        Assert.Contains("app-shell--with-inline-chat", cut.Markup, StringComparison.Ordinal);
        var flyoutStyle = cut.Find("#agent-chat-flyout").GetAttribute("style");
        Assert.Contains("position: fixed", flyoutStyle, StringComparison.Ordinal);
        Assert.Contains("height: calc(100vh - var(--app-header-height, 64px))", flyoutStyle, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_InlineAgentChat_UsesViewportBoundFixedPanelStyles()
    {
        using var ctx = CreateRenderContext();

        var cut = ctx.Render<MainLayoutHost>();
        cut.Find("#agent-chat-launch").Click();
        cut.Find("#agent-chat-display-mode-toggle").Click();

        var flyoutContainer = cut.Find("[data-display-mode='inline']");
        var panelStyle = cut.Find("#agent-chat-flyout").GetAttribute("style");

        Assert.Contains("align-self: stretch", flyoutContainer.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("width: min(420px, 100vw)", flyoutContainer.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("position: fixed", panelStyle, StringComparison.Ordinal);
        Assert.Contains("top: var(--app-header-height, 64px)", panelStyle, StringComparison.Ordinal);
        Assert.Contains("height: calc(100vh - var(--app-header-height, 64px))", panelStyle, StringComparison.Ordinal);
        Assert.Contains("width: min(420px, 100vw)", panelStyle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentChatFlyout_CompleteResize_UpdatesPanelWidth()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult(string.Empty);
        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        Assert.Contains("width: min(420px, 100vw)", cut.Find("#agent-chat-flyout").GetAttribute("style"), StringComparison.Ordinal);
        cut.Find(".agent-chat-resize-handle");

        await cut.Instance.CompleteResizeAsync(560);

        Assert.Contains("width: min(560px, 100vw)", cut.Find("#agent-chat-flyout").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void AgentChatFlyout_TogglingWrapResponses_UpdatesAssistantBubbleClass()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult(string.Empty);
        viewModel.TranscriptItems.Add(new McpVoiceTranscriptEntry
        {
            Role = "assistant",
            Text = "first line\nsecond line",
            TimestampUtc = "2026-03-17T22:55:00Z",
            Category = "message"
        });

        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        Assert.DoesNotContain("agent-chat-message-bubble--no-wrap", cut.Markup, StringComparison.Ordinal);

        cut.Find("#agent-chat-wrap-toggle").Change(false);

        Assert.Contains("agent-chat-message-bubble--no-wrap", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentChatFlyout_InactiveSessionWithoutTranscript_ShowsPreSessionEmptyState()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult(string.Empty);

        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        Assert.Contains("agent-chat-empty-state", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Start a chat to talk to the workspace agent.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-chat-message--assistant", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentChatFlyout_ActiveSessionWithoutTranscript_ShowsAssistantReadyBubble()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult(string.Empty);
        viewModel.IsSessionActive = true;
        viewModel.StatusText = "Loaded 0 transcript item(s).";

        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        Assert.Contains("Agent chat session ready.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Type a message to begin the conversation.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("agent-chat-message--assistant", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-chat-empty-state", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Loaded 0 transcript item(s).", cut.Find(".agent-chat-message-bubble").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Start a chat to talk to the workspace agent.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentChatFlyout_ActiveSessionWithoutTranscript_PersistsReadyBubbleAcrossStatusUpdates()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult(string.Empty);
        viewModel.IsSessionActive = true;
        viewModel.StatusText = "Loaded 0 transcript item(s).";

        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        viewModel.StatusText = "idle (turn active: False)";

        Assert.Contains("Agent chat session ready.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Type a message to begin the conversation.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("idle (turn active: False)", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-chat-empty-state", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentChatFlyout_LoadsStoredWidth_OnFirstRender()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult("640");

        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        Assert.Contains("width: min(640px, 100vw)", cut.Find("#agent-chat-flyout").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void AgentChatFlyout_WithoutStoredWidth_KeepsDefaultWidth()
    {
        using var ctx = CreateRenderContext();
        var viewModel = ctx.Services.GetRequiredService<WebVoiceConversationViewModel>();
        ctx.JSInterop.Setup<string>("agentChatFlyout.getStoredWidth").SetResult(string.Empty);

        var cut = ctx.Render<AgentChatFlyout>(parameters => parameters
            .Add(component => component.ViewModel, viewModel)
            .Add(component => component.IsOpen, true)
            .Add(component => component.IsInline, false));

        Assert.Contains("width: min(420px, 100vw)", cut.Find("#agent-chat-flyout").GetAttribute("style"), StringComparison.Ordinal);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = "test-api-key",
                ["McpServer:WorkspacePath"] = @"E:\\repo"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddWebServices();
        return services.BuildServiceProvider();
    }

    private static BunitContext CreateRenderContext()
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
        ctx.Services.AddAuthorizationCore();
        ctx.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        ctx.Services.AddWebServices();
        ctx.Services.AddSingleton<IHealthApiClient>(new HealthApiClientStub());
        ctx.Services.AddSingleton<IWorkspaceApiClient>(new WorkspaceApiClientStub());

        var workspaceContext = ctx.Services.GetRequiredService<WorkspaceContextViewModel>();
        workspaceContext.ActiveWorkspacePath = @"E:\repo";
        return ctx;
    }

    private sealed class MainLayoutHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<Task<AuthenticationState>>>(0);
            builder.AddAttribute(1, "Value", Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<MainLayout>(0);
                childBuilder.AddAttribute(1, "Body", (RenderFragment)(bodyBuilder =>
                {
                    bodyBuilder.AddMarkupContent(0, "<p>Body</p>");
                }));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
}
