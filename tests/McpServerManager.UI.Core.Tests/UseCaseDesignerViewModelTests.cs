using McpServer.Client.Models;
using McpServerManager.UI.Core.Services;
using McpServerManager.UI.Core.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace McpServerManager.UI.Core.Tests;

public sealed class UseCaseDesignerViewModelTests
{
    private const string WorkspacePath = @"F:\GitHub\TargetWorkspace";

    [Fact]
    public async Task LoadListAsync_UsesActiveWorkspaceAndReplacesItems()
    {
        var service = Substitute.For<IUseCaseService>();
        service.ListAsync(null, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UseCaseSummary>>([
                new() { UseCaseId = 12, Title = "Checkout", UpdatedAtUtc = DateTimeOffset.UtcNow }
            ]));
        var vm = CreateViewModel(service);

        await vm.LoadListAsync();

        Assert.Single(vm.UseCases);
        Assert.Equal("Checkout", vm.UseCases[0].Title);
        await service.Received(1).ListAsync(null, WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDetailAsync_UpdatesHeaderApprovalAndProduct()
    {
        var service = Substitute.For<IUseCaseService>();
        service.GetAsync(7, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 7, Title = "Old", ApprovalStatus = "Draft", ProductKey = "old" }));
        service.UpdateAsync(7, Arg.Any<UpdateUseCaseRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 7, Title = "New", ApprovalStatus = "Draft", ProductKey = "old" }));
        service.SetApprovalAsync(7, Arg.Any<SetUseCaseApprovalRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 7, Title = "New", ApprovalStatus = "Approved", ProductKey = "old" }));
        service.SetProductAsync(7, Arg.Any<SetUseCaseProductRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 7, Title = "New", ApprovalStatus = "Approved", ProductKey = "product-a" }));
        var vm = CreateViewModel(service);
        await vm.LoadDetailAsync(7);

        vm.EditorTitle = "New";
        vm.EditorApprovalStatus = "Approved";
        vm.EditorProductKey = "product-a";
        await vm.SaveDetailAsync();

        await service.Received(1).UpdateAsync(
            7,
            Arg.Is<UpdateUseCaseRequest>(request => request.Title == "New"),
            WorkspacePath,
            Arg.Any<CancellationToken>());
        await service.Received(1).SetApprovalAsync(
            7,
            Arg.Is<SetUseCaseApprovalRequest>(request => request.Status == "Approved"),
            WorkspacePath,
            Arg.Any<CancellationToken>());
        await service.Received(1).SetProductAsync(
            7,
            Arg.Is<SetUseCaseProductRequest>(request => request.ProductKey == "product-a"),
            WorkspacePath,
            Arg.Any<CancellationToken>());
        Assert.False(vm.DetailDirty);
    }

    [Fact]
    public async Task AddChildRecords_UseTypedAppendAndLinkMethodsThenReload()
    {
        var service = Substitute.For<IUseCaseService>();
        service.GetAsync(3, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 3, Title = "Search", ApprovalStatus = "Draft" }));
        service.AttachActorAsync(3, Arg.Any<AttachUseCaseActorRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseActor { ActorId = 1, Name = "User" }));
        service.AddFlowAsync(3, Arg.Any<AddUseCaseFlowRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseFlow { FlowId = 2, FlowType = "Basic" }));
        service.AddStepAsync(3, 2, Arg.Any<AddUseCaseStepRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseStep { StepId = 4, FlowId = 2, Action = "Search" }));
        service.LinkFrAsync(3, Arg.Any<LinkUseCaseToFrRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseFrLink { FrId = "FR-SEARCH-001" }));
        var vm = CreateViewModel(service);
        await vm.LoadDetailAsync(3);

        await vm.AddActorAsync("User", "Primary", true);
        await vm.AddFlowAsync("Basic", "Main");
        await vm.AddStepAsync(2, "Search", "Results appear");
        await vm.LinkFrAsync("FR-SEARCH-001", "Realizes");
        await vm.UnlinkFrAsync("FR-SEARCH-001");

        await service.Received(1).AttachActorAsync(3, Arg.Is<AttachUseCaseActorRequest>(r => r.Name == "User"), WorkspacePath, Arg.Any<CancellationToken>());
        await service.Received(1).AddFlowAsync(3, Arg.Is<AddUseCaseFlowRequest>(r => r.Name == "Main"), WorkspacePath, Arg.Any<CancellationToken>());
        await service.Received(1).AddStepAsync(3, 2, Arg.Is<AddUseCaseStepRequest>(r => r.Action == "Search"), WorkspacePath, Arg.Any<CancellationToken>());
        await service.Received(1).LinkFrAsync(3, Arg.Is<LinkUseCaseToFrRequest>(r => r.FrId == "FR-SEARCH-001"), WorkspacePath, Arg.Any<CancellationToken>());
        await service.Received(1).UnlinkFrAsync(3, "FR-SEARCH-001", WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiagramEditing_AddsBoundaryNodesEdgesAndSavesGraph()
    {
        var service = Substitute.For<IUseCaseService>();
        service.GetAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 5, Title = "Login", ApprovalStatus = "Draft" }));
        service.GetDiagramGraphAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagramGraph()));
        service.GetDiagramAsync(5, "mermaid", WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagram { UseCaseId = 5, Format = "mermaid", Content = "usecase Login" }));
        service.PutDiagramGraphAsync(5, Arg.Any<UseCaseDiagramGraph>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<UseCaseDiagramGraph>(1)));
        var vm = CreateViewModel(service);

        await vm.LoadDiagramAsync(5);
        vm.EnsureSystemBoundary();
        vm.AddDiagramNode("actor", 100, 200);
        vm.AddDiagramNode("usecase", 420, 200);
        var actor = vm.DiagramGraph.Nodes[0];
        var useCase = vm.DiagramGraph.Nodes[1];
        vm.RenameDiagramNode(useCase.Id, "Authenticate");
        vm.MoveDiagramNode(actor.Id, 120, 220);
        vm.AddDiagramEdge("include", actor.Id, useCase.Id);
        await vm.SaveDiagramAsync();

        Assert.NotNull(vm.DiagramGraph.SystemBoundary);
        Assert.Equal("Authenticate", vm.DiagramGraph.Nodes[1].Label);
        Assert.Single(vm.DiagramGraph.Edges);
        Assert.False(vm.DiagramDirty);
        await service.Received(1).PutDiagramGraphAsync(
            5,
            Arg.Is<UseCaseDiagramGraph>(graph => graph.SystemBoundary != null && graph.Nodes.Count == 2 && graph.Edges.Count == 1),
            WorkspacePath,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadListAsync_WhenWorkspaceChangesClearsSelectedState()
    {
        var service = Substitute.For<IUseCaseService>();
        service.ListAsync(Arg.Is<string?>(value => value == null), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UseCaseSummary>>(Array.Empty<UseCaseSummary>()));
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = WorkspacePath };
        var vm = new UseCaseDesignerViewModel(service, workspaceContext, NullLogger<UseCaseDesignerViewModel>.Instance)
        {
            SelectedUseCase = new UseCaseDetail { UseCaseId = 1, Title = "Old" }
        };
        vm.AddDiagramNode("actor", 1, 1);
        await vm.LoadListAsync();

        workspaceContext.ActiveWorkspacePath = @"F:\GitHub\Other";
        await vm.LoadListAsync();

        Assert.Null(vm.SelectedUseCase);
        Assert.Empty(vm.DiagramGraph.Nodes);
    }


    [Fact]
    public async Task StartNewUseCase_ClearsPreviousEditorAndDiagramState()
    {
        var service = Substitute.For<IUseCaseService>();
        service.GetAsync(9, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail
            {
                UseCaseId = 9,
                Title = "Existing",
                BriefDescription = "old description",
                ApprovalStatus = "Approved",
                ProductKey = "old-product"
            }));
        var vm = CreateViewModel(service);
        await vm.LoadDetailAsync(9);
        vm.AddDiagramNode("actor", 1, 1);

        vm.StartNewUseCase();

        Assert.Null(vm.SelectedUseCase);
        Assert.Equal(string.Empty, vm.EditorTitle);
        Assert.Null(vm.EditorBriefDescription);
        Assert.Equal("Draft", vm.EditorApprovalStatus);
        Assert.Null(vm.EditorProductKey);
        Assert.Empty(vm.DiagramGraph.Nodes);
        Assert.False(vm.DetailDirty);
        Assert.False(vm.DiagramDirty);
    }

    [Fact]
    public async Task CreateAsync_AppliesApprovalAndProductSelections()
    {
        var service = Substitute.For<IUseCaseService>();
        service.CreateAsync(Arg.Any<CreateUseCaseRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 22, Title = "Create", ApprovalStatus = "Draft" }));
        service.SetApprovalAsync(22, Arg.Any<SetUseCaseApprovalRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 22, Title = "Create", ApprovalStatus = "Approved" }));
        service.SetProductAsync(22, Arg.Any<SetUseCaseProductRequest>(), WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 22, Title = "Create", ApprovalStatus = "Approved", ProductKey = "web" }));
        var vm = CreateViewModel(service);
        vm.StartNewUseCase();
        vm.EditorTitle = "Create";
        vm.EditorApprovalStatus = "Approved";
        vm.EditorProductKey = "web";

        var createdId = await vm.CreateAsync();

        Assert.Equal(22, createdId);
        await service.Received(1).SetApprovalAsync(22, Arg.Is<SetUseCaseApprovalRequest>(r => r.Status == "Approved"), WorkspacePath, Arg.Any<CancellationToken>());
        await service.Received(1).SetProductAsync(22, Arg.Is<SetUseCaseProductRequest>(r => r.ProductKey == "web"), WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDetailAsync_WhenWorkspaceChanges_RefusesStaleSave()
    {
        var service = Substitute.For<IUseCaseService>();
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = WorkspacePath };
        service.GetAsync(7, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 7, Title = "Old", ApprovalStatus = "Draft" }));
        var vm = new UseCaseDesignerViewModel(service, workspaceContext, NullLogger<UseCaseDesignerViewModel>.Instance);
        await vm.LoadDetailAsync(7);
        vm.EditorTitle = "Changed";
        workspaceContext.ActiveWorkspacePath = @"F:\GitHub\Other";

        await vm.SaveDetailAsync();

        await service.DidNotReceive().UpdateAsync(Arg.Any<long>(), Arg.Any<UpdateUseCaseRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.Null(vm.SelectedUseCase);
        Assert.Contains("Workspace changed", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadDiagramAsync_LoadsServiceGraphAndDoesNotFetchGraphWhenDetailFails()
    {
        var service = Substitute.For<IUseCaseService>();
        var graph = new UseCaseDiagramGraph
        {
            SystemBoundary = new UseCaseDiagramBoundary { Id = "system", Label = "Loaded", X = 1, Y = 2, Width = 3, Height = 4 },
            Nodes = [new UseCaseDiagramNode { Id = "actor-1", Type = "actor", Label = "User", X = 10, Y = 20 }],
            Edges = []
        };
        service.GetAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 5, Title = "Login", ApprovalStatus = "Draft" }));
        service.GetDiagramGraphAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(graph));
        service.GetDiagramAsync(5, "mermaid", WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagram { UseCaseId = 5, Format = "mermaid", Content = "loaded diagram" }));
        var vm = CreateViewModel(service);

        await vm.LoadDiagramAsync(5);

        Assert.Equal("Loaded", vm.DiagramGraph.SystemBoundary?.Label);
        Assert.Single(vm.DiagramGraph.Nodes);
        Assert.Equal("loaded diagram", vm.DiagramPreview);

        var failingService = Substitute.For<IUseCaseService>();
        failingService.GetAsync(6, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns<Task<UseCaseDetail>>(_ => throw new InvalidOperationException("detail failed"));
        var failingVm = CreateViewModel(failingService);
        await failingVm.LoadDiagramAsync(6);

        await failingService.DidNotReceive().GetDiagramGraphAsync(Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.Null(failingVm.SelectedUseCase);
        Assert.Empty(failingVm.DiagramGraph.Nodes);
    }

    [Fact]
    public async Task SaveDiagramAsync_WhenWorkspaceChanges_RefusesStaleSave()
    {
        var service = Substitute.For<IUseCaseService>();
        var workspaceContext = new WorkspaceContextViewModel { ActiveWorkspacePath = WorkspacePath };
        service.GetAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 5, Title = "Login", ApprovalStatus = "Draft" }));
        service.GetDiagramGraphAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagramGraph()));
        service.GetDiagramAsync(5, "mermaid", WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagram { UseCaseId = 5, Format = "mermaid", Content = "diagram" }));
        var vm = new UseCaseDesignerViewModel(service, workspaceContext, NullLogger<UseCaseDesignerViewModel>.Instance);
        await vm.LoadDiagramAsync(5);
        vm.AddDiagramNode("actor", 1, 1);
        workspaceContext.ActiveWorkspacePath = @"F:\GitHub\Other";

        await vm.SaveDiagramAsync();

        await service.DidNotReceive().PutDiagramGraphAsync(Arg.Any<long>(), Arg.Any<UseCaseDiagramGraph>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.Null(vm.SelectedUseCase);
        Assert.Contains("Workspace changed", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveDiagramAsync_AfterLoadingDifferentDetail_RefusesStaleDiagramSave()
    {
        var service = Substitute.For<IUseCaseService>();
        service.GetAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 5, Title = "Login", ApprovalStatus = "Draft" }));
        service.GetAsync(7, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDetail { UseCaseId = 7, Title = "Checkout", ApprovalStatus = "Draft" }));
        service.GetDiagramGraphAsync(5, WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagramGraph { Nodes = [new UseCaseDiagramNode { Id = "actor-1", Type = "actor", Label = "User", X = 1, Y = 1 }] }));
        service.GetDiagramAsync(5, "mermaid", WorkspacePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UseCaseDiagram { UseCaseId = 5, Format = "mermaid", Content = "diagram" }));
        var vm = CreateViewModel(service);
        await vm.LoadDiagramAsync(5);

        await vm.LoadDetailAsync(7);
        vm.AddDiagramNode("actor", 10, 10);
        await vm.SaveDiagramAsync();

        await service.DidNotReceive().PutDiagramGraphAsync(Arg.Any<long>(), Arg.Any<UseCaseDiagramGraph>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        Assert.Empty(vm.DiagramGraph.Nodes);
    }
    private static UseCaseDesignerViewModel CreateViewModel(IUseCaseService service)
        => new(service, new WorkspaceContextViewModel { ActiveWorkspacePath = WorkspacePath }, NullLogger<UseCaseDesignerViewModel>.Instance);
}
