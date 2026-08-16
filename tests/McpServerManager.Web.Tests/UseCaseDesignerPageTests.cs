using Bunit;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class UseCaseDesignerPageTests
{
    private const string WorkspacePath = @"F:\GitHub\TargetWorkspace";

    [Fact]
    public void UseCaseList_RendersUseCasesAndNavigatesToDetail()
    {
        var service = new UseCaseServiceStub();
        using var ctx = CreateContext(service);

        var cut = ctx.Render<McpServerManager.Web.Pages.UseCases.UseCaseList>();

        cut.WaitForAssertion(() => Assert.Contains("Checkout", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Active workspace: F:\\GitHub\\TargetWorkspace", cut.Markup, StringComparison.Ordinal);
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Open").Click();
        Assert.EndsWith("/usecases/42", ctx.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Equal(WorkspacePath, service.LastWorkspacePath);
    }

    [Fact]
    public void UseCaseDetail_EditsMetadataAndUsesSemanticActions()
    {
        var service = new UseCaseServiceStub();
        using var ctx = CreateContext(service);

        var cut = ctx.Render<McpServerManager.Web.Pages.UseCases.UseCaseDetail>(parameters => parameters.Add(component => component.UseCaseId, 42));

        cut.WaitForAssertion(() => Assert.Contains("Existing actor", cut.Markup, StringComparison.Ordinal));
        cut.Find("input.form-control").Change("Checkout updated");
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Save").Click();
        cut.WaitForAssertion(() => Assert.Equal("Checkout updated", service.LastUpdateRequest?.Title));

        cut.Find("input[placeholder='Actor name']").Change("Customer");
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Add actor").Click();
        cut.WaitForAssertion(() => Assert.Equal("Customer", service.LastActorRequest?.Name));

        cut.Find("input[placeholder='FR id']").Change("FR-CHECKOUT-001");
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Link FR").Click();
        cut.WaitForAssertion(() => Assert.Equal("FR-CHECKOUT-001", service.LastFrLinkRequest?.FrId));
    }

    [Fact]
    public void UseCaseDiagram_RendersSvgDesignerAndSavesGraphEdits()
    {
        var service = new UseCaseServiceStub { Graph = new UseCaseDiagramGraph() };
        using var ctx = CreateContext(service);

        var cut = ctx.Render<McpServerManager.Web.Pages.UseCases.UseCaseDiagram>(parameters => parameters.Add(component => component.UseCaseId, 42));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("svg.usecase-svg-canvas")));
        cut.FindAll("button").First(button => button.TextContent.Trim() == "System Boundary").Click();
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Actor").Click();
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Use Case").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("g.usecase-node").Count));

        cut.FindAll("g.usecase-node")[0].Click();
        cut.FindAll("select.form-select").Last().Change(service.Graph.Nodes[1].Id);
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Connect").Click();
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Save Diagram").Click();

        cut.WaitForAssertion(() => Assert.NotNull(service.SavedGraph));
        Assert.NotNull(service.SavedGraph!.SystemBoundary);
        Assert.Equal(2, service.SavedGraph.Nodes.Count);
        Assert.Single(service.SavedGraph.Edges);
    }

    [Fact]
    public void UseCaseDiagram_ShowsLoadError()
    {
        var service = new UseCaseServiceStub { ThrowOnGraphLoad = true };
        using var ctx = CreateContext(service);

        var cut = ctx.Render<McpServerManager.Web.Pages.UseCases.UseCaseDiagram>(parameters => parameters.Add(component => component.UseCaseId, 42));

        cut.WaitForAssertion(() => Assert.Contains("Diagram operation failed", cut.Markup, StringComparison.Ordinal));
    }

    private static BunitContext CreateContext(IUseCaseService service)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = "test-api-key",
                ["McpServer:WorkspacePath"] = WorkspacePath
            })
            .Build();
        ctx.Services.AddSingleton<IConfiguration>(config);
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ctx.Services.AddWebServices();
        ctx.Services.AddSingleton(service);
        ctx.Services.GetRequiredService<WorkspaceContextViewModel>().ActiveWorkspacePath = WorkspacePath;
        return ctx;
    }

    private sealed class UseCaseServiceStub : IUseCaseService
    {
        public string? LastWorkspacePath { get; private set; }
        public UpdateUseCaseRequest? LastUpdateRequest { get; private set; }
        public AttachUseCaseActorRequest? LastActorRequest { get; private set; }
        public LinkUseCaseToFrRequest? LastFrLinkRequest { get; private set; }
        public UseCaseDiagramGraph Graph { get; set; } = CreateGraph();
        public UseCaseDiagramGraph? SavedGraph { get; private set; }
        public bool ThrowOnGraphLoad { get; set; }

        public Task<IReadOnlyList<UseCaseSummary>> ListAsync(string? title, string? workspacePath, CancellationToken cancellationToken = default)
        {
            LastWorkspacePath = workspacePath;
            return Task.FromResult<IReadOnlyList<UseCaseSummary>>([
                new() { UseCaseId = 42, Title = "Checkout", Scope = "Web", Priority = 1, UpdatedAtUtc = DateTimeOffset.UtcNow }
            ]);
        }

        public Task<UseCaseDetail> GetAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        {
            LastWorkspacePath = workspacePath;
            return Task.FromResult(CreateDetail(useCaseId));
        }

        public Task<UseCaseDetail> CreateAsync(CreateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDetail(99, request.Title));

        public Task<UseCaseDetail> UpdateAsync(long useCaseId, UpdateUseCaseRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            return Task.FromResult(CreateDetail(useCaseId, request.Title ?? "Checkout"));
        }

        public Task DeleteAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UseCaseActor> AttachActorAsync(long useCaseId, AttachUseCaseActorRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        {
            LastActorRequest = request;
            return Task.FromResult(new UseCaseActor { ActorId = 2, Name = request.Name ?? string.Empty, Type = request.Type });
        }

        public Task<UseCaseFlow> AddFlowAsync(long useCaseId, AddUseCaseFlowRequest request, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new UseCaseFlow { FlowId = 8, FlowType = request.FlowType, Name = request.Name });

        public Task<UseCaseStep> AddStepAsync(long useCaseId, long flowId, AddUseCaseStepRequest request, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new UseCaseStep { StepId = 9, FlowId = flowId, Action = request.Action });

        public Task<UseCaseFrLink> LinkFrAsync(long useCaseId, LinkUseCaseToFrRequest request, string? workspacePath, CancellationToken cancellationToken = default)
        {
            LastFrLinkRequest = request;
            return Task.FromResult(new UseCaseFrLink { FrId = request.FrId, LinkType = request.LinkType ?? "Realizes" });
        }

        public Task UnlinkFrAsync(long useCaseId, string frId, string? workspacePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UseCaseDetail> SetApprovalAsync(long useCaseId, SetUseCaseApprovalRequest request, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDetail(useCaseId));

        public Task<UseCaseDetail> SetProductAsync(long useCaseId, SetUseCaseProductRequest request, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDetail(useCaseId));

        public Task<UseCaseFrCoverage> GetCoverageAsync(string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new UseCaseFrCoverage());

        public Task<UseCaseDiagram> GetDiagramAsync(long useCaseId, string format, string? workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new UseCaseDiagram { UseCaseId = useCaseId, Format = format, Content = "usecase Checkout" });

        public Task<UseCaseDiagramGraph> GetDiagramGraphAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken = default)
        {
            if (ThrowOnGraphLoad)
                throw new InvalidOperationException("graph unavailable");
            return Task.FromResult(Graph);
        }

        public Task<UseCaseDiagramGraph> PutDiagramGraphAsync(long useCaseId, UseCaseDiagramGraph graph, string? workspacePath, CancellationToken cancellationToken = default)
        {
            SavedGraph = graph;
            return Task.FromResult(graph);
        }

        private static UseCaseDetail CreateDetail(long id, string title = "Checkout")
            => new()
            {
                UseCaseId = id,
                Title = title,
                BriefDescription = "Customer checks out",
                ApprovalStatus = "Draft",
                ProductKey = "web",
                Actors = [new UseCaseActor { ActorId = 1, Name = "Existing actor", Type = "Primary", IsPrimary = true }],
                Flows = [new UseCaseFlow { FlowId = 7, FlowType = "Basic", Name = "Main", Steps = [new UseCaseStep { StepId = 1, FlowId = 7, StepNumber = 1, Action = "Start" }] }],
                FrLinks = [new UseCaseFrLink { FrId = "FR-CHECKOUT-000", LinkType = "Realizes" }]
            };

        private static UseCaseDiagramGraph CreateGraph()
            => new()
            {
                SystemBoundary = new UseCaseDiagramBoundary { Id = "system", Label = "System", X = 200, Y = 80, Width = 500, Height = 360 },
                Nodes =
                [
                    new UseCaseDiagramNode { Id = "actor-1", Type = "actor", Label = "Customer", X = 120, Y = 220 },
                    new UseCaseDiagramNode { Id = "usecase-1", Type = "usecase", Label = "Checkout", X = 430, Y = 220 }
                ],
                Edges = [new UseCaseDiagramEdge { Id = "edge-1", Type = "association", Source = "actor-1", Target = "usecase-1" }]
            };
    }
}
