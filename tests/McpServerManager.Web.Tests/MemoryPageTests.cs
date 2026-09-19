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
        });
    }

    [Fact]
    public void MemoryDetailMarkup_ReloadKeyIncludesWorkspaceAndMemoryId()
    {
        var markup = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "McpServerManager.Web", "Pages", "Memory", "MemoryDetail.razor"));

        Assert.Contains("_loadedDetailKey", markup, StringComparison.Ordinal);
        Assert.Contains("WorkspaceContext.ActiveWorkspacePath}|{MemoryId}", markup, StringComparison.Ordinal);
        Assert.Contains("if (string.Equals(ViewModel.Detail?.Id, MemoryId, StringComparison.Ordinal))", markup, StringComparison.Ordinal);
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
