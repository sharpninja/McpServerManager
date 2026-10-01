using AngleSharp.Html.Dom;
using Bunit;
using McpServerManager.UI.Core.Messages;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using McpServerManager.Web.Tests.TestInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class MemoryPageTests
{
    [Fact]
    public void MemoryListPage_RendersItems_AndLinksAfterTodosPattern()
    {
        var api = Substitute.For<IMemoryApiClient>();
        api.ListMemoriesAsync(Arg.Any<ListMemoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ListMemoriesResult(
                [
                    new MemoryListItem(
                        "mem-1",
                        "prefs",
                        MemoryScope.Workspace,
                        @"E:\repo",
                        "remember this",
                        4,
                        DateTimeOffset.Parse("2026-09-19T00:00:00Z"),
                        "viewer")
                ],
                1));

        using var ctx = CreateTestContext(services => services.AddSingleton(api));
        var cut = ctx.Render<McpServerManager.Web.Pages.Memory.MemoryList>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("mem-1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("/memory/mem-1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("4", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("/memory/new", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MemoryDetailPage_ShowsDisplayOnlyVersion_AndEditor()
    {
        var api = Substitute.For<IMemoryApiClient>();
        api.ListMemoriesAsync(Arg.Any<ListMemoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ListMemoriesResult([], 0));
        api.GetMemoryAsync("mem-1", Arg.Any<CancellationToken>())
            .Returns(new MemoryDetail(
                "mem-1",
                "prefs",
                MemoryScope.Workspace,
                @"E:\repo",
                "remember this",
                4,
                DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-09-19T00:00:00Z"),
                "viewer"));

        using var ctx = CreateTestContext(services => services.AddSingleton(api));
        var cut = ctx.Render<McpServerManager.Web.Pages.Memory.MemoryDetail>(parameters =>
            parameters.Add(p => p.MemoryId, "mem-1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("remember this", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("display only", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("expectedVersion", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Operator", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Global Memory", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("[data-testid='memory-scope']"));
        });
    }

    [Fact]
    public void MemoryDetailPage_DefaultWorkspace_ShowsGlobalMemoryToggle_AndSaveCreatesGlobal()
    {
        AddMemoryCommand? captured = null;
        var api = Substitute.For<IMemoryApiClient>();
        api.ListMemoriesAsync(Arg.Any<ListMemoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ListMemoriesResult([], 0));
        api.AddMemoryAsync(Arg.Any<AddMemoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<AddMemoryCommand>();
                return new MemoryMutationOutcome(
                    true,
                    null,
                    new MemoryDetail(
                        captured.Id ?? "mem-new",
                        captured.Category,
                        captured.Scope,
                        null,
                        captured.Text,
                        1,
                        DateTimeOffset.Parse("2026-09-21T00:00:00Z"),
                        DateTimeOffset.Parse("2026-09-21T00:00:00Z"),
                        captured.UpdatedBy));
            });

        using var ctx = CreateTestContext(services => services.AddSingleton(api));
        ctx.Services.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = null;
        var cut = ctx.Render<McpServerManager.Web.Pages.Memory.MemoryDetail>(parameters =>
            parameters.Add(p => p.MemoryId, "new"));

        cut.WaitForAssertion(() =>
        {
            var toggle = Assert.IsAssignableFrom<IHtmlInputElement>(cut.Find("[data-testid='global-memory-toggle']"));
            Assert.Equal("checkbox", toggle.Type);
            Assert.True(toggle.IsChecked);
            Assert.Contains("Global Memory", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("[data-testid='memory-scope']"));
        });

        cut.Find("[data-testid='memory-category']").Change("prefs");
        cut.Find("[data-testid='memory-text']").Change("remember this");
        cut.Find(".detail-card-expander__actions .btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(captured);
            Assert.Equal(MemoryScope.Global, captured!.Scope);
            Assert.Equal("prefs", captured.Category);
            Assert.Equal("remember this", captured.Text);
        });
    }

    [Fact]
    public void MemoryDetailPage_DefaultWorkspace_GlobalToggleOff_DisablesSave()
    {
        var api = Substitute.For<IMemoryApiClient>();
        api.ListMemoriesAsync(Arg.Any<ListMemoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ListMemoriesResult([], 0));

        using var ctx = CreateTestContext(services => services.AddSingleton(api));
        ctx.Services.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = string.Empty;
        var cut = ctx.Render<McpServerManager.Web.Pages.Memory.MemoryDetail>(parameters =>
            parameters.Add(p => p.MemoryId, "new"));

        cut.WaitForAssertion(() =>
            Assert.True(Assert.IsAssignableFrom<IHtmlInputElement>(cut.Find("[data-testid='global-memory-toggle']")).IsChecked));

        cut.Find("[data-testid='global-memory-toggle']").Change(false);

        cut.WaitForAssertion(() =>
        {
            var toggle = Assert.IsAssignableFrom<IHtmlInputElement>(cut.Find("[data-testid='global-memory-toggle']"));
            Assert.False(toggle.IsChecked);
            Assert.True(cut.Find(".detail-card-expander__actions .btn-primary").HasAttribute("disabled"));
            Assert.Contains(MemoryScopePolicy.WorkspaceScopeRequiresActiveWorkspace, cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AddWebServices_RegistersMemoryApiClientAdapter()
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
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IMemoryApiClient>();
        Assert.IsType<McpServerManager.Web.Adapters.MemoryApiClientAdapter>(client);
    }

    [Fact]
    public void NavMenuMarkup_PlacesMemoryHrefImmediatelyAfterTodos()
    {
        var markup = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "McpServerManager.Web", "Components", "Layout", "NavMenu.razor"));

        var todos = markup.IndexOf("href=\"/todos\"", StringComparison.Ordinal);
        var memory = markup.IndexOf("href=\"/memory\"", StringComparison.Ordinal);
        var triage = markup.IndexOf("href=\"/triage\"", StringComparison.Ordinal);

        Assert.True(todos >= 0, "Todos NavLink is missing.");
        Assert.True(memory >= 0, "Memory NavLink is missing.");
        Assert.True(triage >= 0, "Triage NavLink is missing.");
        Assert.True(todos < memory && memory < triage, "Memory must sit after Todos and before Triage.");
    }

    private static Bunit.BunitContext CreateTestContext(Action<IServiceCollection>? configureServices = null)
    {
        var ctx = new Bunit.BunitContext();
        var quickGridModule = ctx.JSInterop.SetupModule("./_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.razor.js");
        quickGridModule.SetupModule("init", _ => true);
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
        configureServices?.Invoke(ctx.Services);
        ctx.Services.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = @"E:\repo";
        return ctx;
    }
}
