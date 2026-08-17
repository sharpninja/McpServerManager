using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using McpServer.Client.Models;
using McpServerManager.UI.Core.Services;
using Microsoft.Extensions.Logging;

namespace McpServerManager.UI.Core.ViewModels;

/// <summary>ViewModel for MCP Web use-case list, detail, and SVG diagram editing.</summary>
public sealed partial class UseCaseDesignerViewModel : ObservableObject
{
    private readonly IUseCaseService _service;
    private readonly WorkspaceContextViewModel _workspaceContext;
    private readonly ILogger<UseCaseDesignerViewModel> _logger;

    public UseCaseDesignerViewModel(
        IUseCaseService service,
        WorkspaceContextViewModel workspaceContext,
        ILogger<UseCaseDesignerViewModel> logger)
    {
        _service = service;
        _workspaceContext = workspaceContext;
        _logger = logger;
    }

    public ObservableCollection<UseCaseSummary> UseCases { get; } = [];

    public ObservableCollection<string> ValidationMessages { get; } = [];

    [ObservableProperty]
    private string? _lastLoadedWorkspacePath;

    private long? _loadedUseCaseId;
    private string? _loadedUseCaseWorkspacePath;
    private long? _loadedDiagramUseCaseId;
    private string? _loadedDiagramWorkspacePath;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _titleFilter;

    [ObservableProperty]
    private UseCaseDetail? _selectedUseCase;

    [ObservableProperty]
    private UseCaseDiagramGraph _diagramGraph = new();

    [ObservableProperty]
    private string? _diagramPreview;

    [ObservableProperty]
    private bool _detailDirty;

    [ObservableProperty]
    private bool _diagramDirty;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string? _editorBriefDescription;

    [ObservableProperty]
    private string? _editorPrecondition;

    [ObservableProperty]
    private string? _editorPostcondition;

    [ObservableProperty]
    private string? _editorScope;

    [ObservableProperty]
    private int _editorPriority;

    [ObservableProperty]
    private string _editorApprovalStatus = "Draft";

    [ObservableProperty]
    private string? _editorProductKey;

    public string? ActiveWorkspacePath => _workspaceContext.ActiveWorkspacePath;

    public bool HasSelectedUseCase => SelectedUseCase is not null;

    public bool IsDetailLoadedFor(long useCaseId, string? workspacePath)
        => SelectedUseCase?.UseCaseId == useCaseId
            && _loadedUseCaseId == useCaseId
            && string.Equals(_loadedUseCaseWorkspacePath, workspacePath, StringComparison.Ordinal);

    public bool HasDirtyDetailFor(long useCaseId)
        => DetailDirty && SelectedUseCase?.UseCaseId == useCaseId && _loadedUseCaseId == useCaseId;

    public bool IsDiagramLoadedFor(long useCaseId, string? workspacePath)
        => SelectedUseCase?.UseCaseId == useCaseId
            && _loadedDiagramUseCaseId == useCaseId
            && string.Equals(_loadedDiagramWorkspacePath, workspacePath, StringComparison.Ordinal);

    public bool HasDirtyDiagramFor(long useCaseId)
        => DiagramDirty && SelectedUseCase?.UseCaseId == useCaseId && _loadedDiagramUseCaseId == useCaseId;

    public bool CanSaveDetail => HasSelectedUseCase && DetailDirty && IsDetailValid();

    public bool CanSaveDiagram => HasSelectedUseCase && DiagramDirty && IsDiagramValid();

    partial void OnSelectedUseCaseChanged(UseCaseDetail? value)
    {
        ApplyDetail(value);
        OnPropertyChanged(nameof(HasSelectedUseCase));
        OnPropertyChanged(nameof(CanSaveDetail));
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    partial void OnEditorTitleChanged(string value) => MarkDetailDirty();

    partial void OnEditorBriefDescriptionChanged(string? value) => MarkDetailDirty();

    partial void OnEditorPreconditionChanged(string? value) => MarkDetailDirty();

    partial void OnEditorPostconditionChanged(string? value) => MarkDetailDirty();

    partial void OnEditorScopeChanged(string? value) => MarkDetailDirty();

    partial void OnEditorPriorityChanged(int value) => MarkDetailDirty();

    partial void OnEditorApprovalStatusChanged(string value) => MarkDetailDirty();

    partial void OnEditorProductKeyChanged(string? value) => MarkDetailDirty();

    public async Task LoadListAsync(CancellationToken cancellationToken = default)
    {
        await RunLoadAsync(async ct =>
        {
            var workspacePath = ActiveWorkspacePath;
            ResetIfWorkspaceChanged(workspacePath);
            var items = await _service.ListAsync(Normalize(TitleFilter), workspacePath, ct).ConfigureAwait(true);
            Replace(UseCases, items);
            StatusMessage = $"Loaded {UseCases.Count} use cases.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task LoadDetailAsync(long useCaseId, CancellationToken cancellationToken = default)
    {
        await RunLoadAsync(async ct =>
        {
            var workspacePath = ActiveWorkspacePath;
            ResetIfWorkspaceChanged(workspacePath);
            SelectedUseCase = await _service.GetAsync(useCaseId, workspacePath, ct).ConfigureAwait(true);
            CaptureUseCaseIdentity(useCaseId, workspacePath);
            ClearLoadedDiagramState();
            DetailDirty = false;
            StatusMessage = $"Loaded use case {useCaseId}.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public void StartNewUseCase()
    {
        ResetIfWorkspaceChanged(ActiveWorkspacePath);
        ClearLoadedUseCaseState();
        ApplyDetail(null);
        ValidationMessages.Clear();
        ErrorMessage = null;
        StatusMessage = "Ready to create a use case.";
    }

    public async Task<long?> CreateAsync(CancellationToken cancellationToken = default)
    {
        ValidationMessages.Clear();
        if (string.IsNullOrWhiteSpace(EditorTitle))
        {
            ValidationMessages.Add("Title is required.");
            return null;
        }

        var requestedApprovalStatus = EditorApprovalStatus;
        var requestedProductKey = Normalize(EditorProductKey);
        var workspacePath = ActiveWorkspacePath;

        await RunSaveAsync(async ct =>
        {
            var created = await _service.CreateAsync(
                new CreateUseCaseRequest
                {
                    Title = EditorTitle.Trim(),
                    BriefDescription = Normalize(EditorBriefDescription),
                    Precondition = Normalize(EditorPrecondition),
                    Postcondition = Normalize(EditorPostcondition),
                    Scope = Normalize(EditorScope),
                    Priority = EditorPriority
                },
                workspacePath,
                ct).ConfigureAwait(true);

            SelectedUseCase = created;
            CaptureUseCaseIdentity(created.UseCaseId, workspacePath);
            SelectedUseCase = await ApplyApprovalAndProductAsync(created, requestedApprovalStatus, requestedProductKey, workspacePath, ct).ConfigureAwait(true);
            DetailDirty = false;
            StatusMessage = $"Created use case {SelectedUseCase.UseCaseId}.";
        }, cancellationToken).ConfigureAwait(true);

        return SelectedUseCase?.UseCaseId;
    }

    public async Task SaveDetailAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null || !ValidateDetail())
            return;

        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        var requestedApprovalStatus = EditorApprovalStatus;
        var requestedProductKey = Normalize(EditorProductKey);

        await RunSaveAsync(async ct =>
        {
            var updated = await _service.UpdateAsync(
                useCaseId,
                new UpdateUseCaseRequest
                {
                    Title = EditorTitle.Trim(),
                    BriefDescription = Normalize(EditorBriefDescription),
                    Precondition = Normalize(EditorPrecondition),
                    Postcondition = Normalize(EditorPostcondition),
                    Scope = Normalize(EditorScope),
                    Priority = EditorPriority
                },
                workspacePath,
                ct).ConfigureAwait(true);

            SelectedUseCase = await ApplyApprovalAndProductAsync(updated, requestedApprovalStatus, requestedProductKey, workspacePath, ct).ConfigureAwait(true);
            CaptureUseCaseIdentity(useCaseId, workspacePath);
            ClearLoadedDiagramState();
            DetailDirty = false;
            StatusMessage = $"Saved use case {useCaseId}.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task AddActorAsync(string name, string type, bool isPrimary, CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null || string.IsNullOrWhiteSpace(name))
            return;

        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        await RunSaveAsync(async ct =>
        {
            await _service.AttachActorAsync(
                useCaseId,
                new AttachUseCaseActorRequest { Name = name.Trim(), Type = Normalize(type) ?? "Primary", IsPrimary = isPrimary },
                workspacePath,
                ct).ConfigureAwait(true);
            await ReloadSelectedPreservingDirtyHeaderAsync(ct).ConfigureAwait(true);
            StatusMessage = "Actor added.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task AddFlowAsync(string flowType, string? name, CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null)
            return;

        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        await RunSaveAsync(async ct =>
        {
            await _service.AddFlowAsync(
                useCaseId,
                new AddUseCaseFlowRequest { FlowType = Normalize(flowType) ?? "Basic", Name = Normalize(name) },
                workspacePath,
                ct).ConfigureAwait(true);
            await ReloadSelectedPreservingDirtyHeaderAsync(ct).ConfigureAwait(true);
            StatusMessage = "Flow added.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task AddStepAsync(long flowId, string action, string? systemResponse, CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null || flowId <= 0 || string.IsNullOrWhiteSpace(action))
            return;

        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        await RunSaveAsync(async ct =>
        {
            await _service.AddStepAsync(
                useCaseId,
                flowId,
                new AddUseCaseStepRequest { Action = action.Trim(), SystemResponse = Normalize(systemResponse) },
                workspacePath,
                ct).ConfigureAwait(true);
            await ReloadSelectedPreservingDirtyHeaderAsync(ct).ConfigureAwait(true);
            StatusMessage = "Step added.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task LinkFrAsync(string frId, string linkType, CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null || string.IsNullOrWhiteSpace(frId))
            return;

        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        await RunSaveAsync(async ct =>
        {
            await _service.LinkFrAsync(
                useCaseId,
                new LinkUseCaseToFrRequest { FrId = frId.Trim(), LinkType = Normalize(linkType) ?? "Realizes" },
                workspacePath,
                ct).ConfigureAwait(true);
            await ReloadSelectedPreservingDirtyHeaderAsync(ct).ConfigureAwait(true);
            StatusMessage = "Functional requirement linked.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task UnlinkFrAsync(string frId, CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null || string.IsNullOrWhiteSpace(frId))
            return;

        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        await RunSaveAsync(async ct =>
        {
            await _service.UnlinkFrAsync(useCaseId, frId, workspacePath, ct).ConfigureAwait(true);
            await ReloadSelectedPreservingDirtyHeaderAsync(ct).ConfigureAwait(true);
            StatusMessage = "Functional requirement unlinked.";
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task LoadDiagramAsync(long useCaseId, CancellationToken cancellationToken = default)
    {
        if (HasDirtyDetailFor(useCaseId))
        {
            ErrorMessage = "Save or reload the use case before opening the diagram.";
            return;
        }

        await RunLoadAsync(async ct =>
        {
            var workspacePath = ActiveWorkspacePath;
            ResetIfWorkspaceChanged(workspacePath);
            var detail = await _service.GetAsync(useCaseId, workspacePath, ct).ConfigureAwait(true);
            var graph = await _service.GetDiagramGraphAsync(useCaseId, workspacePath, ct).ConfigureAwait(true);
            SelectedUseCase = detail;
            CaptureUseCaseIdentity(useCaseId, workspacePath);
            DiagramGraph = graph;
            CaptureDiagramIdentity(useCaseId, workspacePath);
            DiagramDirty = false;
            await RefreshDiagramPreviewAsync(useCaseId, workspacePath, ct).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    public void EnsureSystemBoundary()
    {
        DiagramGraph.SystemBoundary ??= new UseCaseDiagramBoundary { Id = "system", Label = "System", X = 220, Y = 80, Width = 520, Height = 360 };
        DiagramDirty = true;
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    public void RenameSystemBoundary(string label)
    {
        if (DiagramGraph.SystemBoundary is null || string.IsNullOrWhiteSpace(label))
            return;

        DiagramGraph.SystemBoundary.Label = label.Trim();
        DiagramDirty = true;
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    public void AddDiagramNode(string type, double x, double y)
    {
        var normalizedType = type is "actor" or "usecase" ? type : "usecase";
        DiagramGraph.Nodes.Add(new UseCaseDiagramNode
        {
            Id = $"{normalizedType}-{Guid.NewGuid():N}",
            Type = normalizedType,
            Label = normalizedType == "actor" ? "Actor" : "Use Case",
            X = x,
            Y = y
        });
        DiagramDirty = true;
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    public void MoveDiagramNode(string nodeId, double x, double y)
    {
        var node = DiagramGraph.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
        if (node is null)
            return;

        node.X = x;
        node.Y = y;
        DiagramDirty = true;
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    public void RenameDiagramNode(string nodeId, string label)
    {
        var node = DiagramGraph.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
        if (node is null || string.IsNullOrWhiteSpace(label))
            return;

        node.Label = label.Trim();
        DiagramDirty = true;
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    public void AddDiagramEdge(string type, string sourceNodeId, string targetNodeId)
    {
        if (sourceNodeId == targetNodeId || !DiagramGraph.Nodes.Any(n => n.Id == sourceNodeId) || !DiagramGraph.Nodes.Any(n => n.Id == targetNodeId))
            return;

        DiagramGraph.Edges.Add(new UseCaseDiagramEdge
        {
            Id = $"edge-{Guid.NewGuid():N}",
            Type = NormalizeEdgeType(type),
            Source = sourceNodeId,
            Target = targetNodeId
        });
        DiagramDirty = true;
        OnPropertyChanged(nameof(CanSaveDiagram));
    }

    public void DeleteDiagramItem(string id)
    {
        var removedNodes = DiagramGraph.Nodes.RemoveAll(n => string.Equals(n.Id, id, StringComparison.Ordinal));
        var removedEdges = DiagramGraph.Edges.RemoveAll(e => string.Equals(e.Id, id, StringComparison.Ordinal));
        if (removedNodes > 0)
            DiagramGraph.Edges.RemoveAll(e => e.Source == id || e.Target == id);
        if (removedNodes > 0 || removedEdges > 0)
        {
            DiagramDirty = true;
            OnPropertyChanged(nameof(CanSaveDiagram));
        }
    }

    public async Task SaveDiagramAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedUseCase is null || !ValidateDiagram())
            return;

        if (!TryGetLoadedDiagramIdentity(out var useCaseId, out var workspacePath))
            return;

        await RunSaveAsync(async ct =>
        {
            DiagramGraph = await _service.PutDiagramGraphAsync(useCaseId, DiagramGraph, workspacePath, ct).ConfigureAwait(true);
            CaptureDiagramIdentity(useCaseId, workspacePath);
            DiagramDirty = false;
            await RefreshDiagramPreviewAsync(useCaseId, workspacePath, ct).ConfigureAwait(true);
            StatusMessage = "Diagram saved.";
        }, cancellationToken).ConfigureAwait(true);
    }

    private async Task<UseCaseDetail> ApplyApprovalAndProductAsync(
        UseCaseDetail detail,
        string requestedApprovalStatus,
        string? requestedProductKey,
        string? workspacePath,
        CancellationToken cancellationToken)
    {
        var current = detail;
        if (!string.Equals(current.ApprovalStatus, requestedApprovalStatus, StringComparison.Ordinal))
        {
            current = await _service.SetApprovalAsync(
                current.UseCaseId,
                new SetUseCaseApprovalRequest { Status = requestedApprovalStatus },
                workspacePath,
                cancellationToken).ConfigureAwait(true);
        }

        if (!string.Equals(current.ProductKey ?? string.Empty, requestedProductKey ?? string.Empty, StringComparison.Ordinal))
        {
            current = await _service.SetProductAsync(
                current.UseCaseId,
                new SetUseCaseProductRequest { ProductKey = requestedProductKey },
                workspacePath,
                cancellationToken).ConfigureAwait(true);
        }

        return current;
    }

    private sealed record DetailEditorSnapshot(
        string Title,
        string? BriefDescription,
        string? Precondition,
        string? Postcondition,
        string? Scope,
        int Priority,
        string ApprovalStatus,
        string? ProductKey);

    private async Task ReloadSelectedAsync(CancellationToken cancellationToken)
    {
        if (!TryGetLoadedUseCaseIdentity(out var useCaseId, out var workspacePath))
            return;

        SelectedUseCase = await _service.GetAsync(useCaseId, workspacePath, cancellationToken).ConfigureAwait(true);
        CaptureUseCaseIdentity(useCaseId, workspacePath);
        DetailDirty = false;
    }

    private async Task ReloadSelectedPreservingDirtyHeaderAsync(CancellationToken cancellationToken)
    {
        var snapshot = DetailDirty ? CaptureDetailEditorSnapshot() : null;

        await ReloadSelectedAsync(cancellationToken).ConfigureAwait(true);

        if (snapshot is not null)
        {
            RestoreDetailEditorSnapshot(snapshot);
            DetailDirty = true;
            OnPropertyChanged(nameof(CanSaveDetail));
        }
    }

    private DetailEditorSnapshot CaptureDetailEditorSnapshot()
        => new(
            EditorTitle,
            EditorBriefDescription,
            EditorPrecondition,
            EditorPostcondition,
            EditorScope,
            EditorPriority,
            EditorApprovalStatus,
            EditorProductKey);

    private void RestoreDetailEditorSnapshot(DetailEditorSnapshot snapshot)
    {
        EditorTitle = snapshot.Title;
        EditorBriefDescription = snapshot.BriefDescription;
        EditorPrecondition = snapshot.Precondition;
        EditorPostcondition = snapshot.Postcondition;
        EditorScope = snapshot.Scope;
        EditorPriority = snapshot.Priority;
        EditorApprovalStatus = snapshot.ApprovalStatus;
        EditorProductKey = snapshot.ProductKey;
    }

    private async Task RefreshDiagramPreviewAsync(long useCaseId, string? workspacePath, CancellationToken cancellationToken)
    {
        var diagram = await _service.GetDiagramAsync(useCaseId, "mermaid", workspacePath, cancellationToken).ConfigureAwait(true);
        DiagramPreview = diagram.Content;
    }

    private void ApplyDetail(UseCaseDetail? detail)
    {
        if (detail is null)
        {
            EditorTitle = string.Empty;
            EditorBriefDescription = null;
            EditorPrecondition = null;
            EditorPostcondition = null;
            EditorScope = null;
            EditorPriority = 0;
            EditorApprovalStatus = "Draft";
            EditorProductKey = null;
            DetailDirty = false;
            DiagramGraph = new UseCaseDiagramGraph();
            DiagramPreview = null;
            DiagramDirty = false;
            OnPropertyChanged(nameof(CanSaveDetail));
            OnPropertyChanged(nameof(CanSaveDiagram));
            return;
        }

        EditorTitle = detail.Title;
        EditorBriefDescription = detail.BriefDescription;
        EditorPrecondition = detail.Precondition;
        EditorPostcondition = detail.Postcondition;
        EditorScope = detail.Scope;
        EditorPriority = detail.Priority;
        EditorApprovalStatus = detail.ApprovalStatus;
        EditorProductKey = detail.ProductKey;
        DetailDirty = false;
    }

    private void CaptureUseCaseIdentity(long useCaseId, string? workspacePath)
    {
        _loadedUseCaseId = useCaseId;
        _loadedUseCaseWorkspacePath = workspacePath;
    }

    private void CaptureDiagramIdentity(long useCaseId, string? workspacePath)
    {
        _loadedDiagramUseCaseId = useCaseId;
        _loadedDiagramWorkspacePath = workspacePath;
    }

    private bool TryGetLoadedUseCaseIdentity(out long useCaseId, out string? workspacePath)
    {
        useCaseId = _loadedUseCaseId.GetValueOrDefault();
        workspacePath = _loadedUseCaseWorkspacePath;
        if (SelectedUseCase is not null
            && _loadedUseCaseId == SelectedUseCase.UseCaseId
            && string.Equals(_loadedUseCaseWorkspacePath, ActiveWorkspacePath, StringComparison.Ordinal))
        {
            return true;
        }

        ErrorMessage = "Workspace changed. Reload the use case before saving.";
        return false;
    }

    private bool TryGetLoadedDiagramIdentity(out long useCaseId, out string? workspacePath)
    {
        useCaseId = _loadedDiagramUseCaseId.GetValueOrDefault();
        workspacePath = _loadedDiagramWorkspacePath;
        if (SelectedUseCase is not null
            && _loadedDiagramUseCaseId == SelectedUseCase.UseCaseId
            && _loadedUseCaseId == SelectedUseCase.UseCaseId
            && string.Equals(_loadedDiagramWorkspacePath, ActiveWorkspacePath, StringComparison.Ordinal))
        {
            return true;
        }

        ErrorMessage = "Workspace changed. Reload the diagram before saving.";
        return false;
    }

    private void ClearLoadedUseCaseState()
    {
        _loadedUseCaseId = null;
        _loadedUseCaseWorkspacePath = null;
        SelectedUseCase = null;
        ClearLoadedDiagramState();
        DetailDirty = false;
    }

    private void ClearLoadedDiagramState()
    {
        _loadedDiagramUseCaseId = null;
        _loadedDiagramWorkspacePath = null;
        DiagramGraph = new UseCaseDiagramGraph();
        DiagramPreview = null;
        DiagramDirty = false;
    }

    private void ResetIfWorkspaceChanged(string? workspacePath)
    {
        if (string.Equals(LastLoadedWorkspacePath, workspacePath, StringComparison.Ordinal))
            return;

        LastLoadedWorkspacePath = workspacePath;
        UseCases.Clear();
        ClearLoadedUseCaseState();
    }

    private async Task RunLoadAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await operation(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunSaveAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsSaving = true;
        try
        {
            await operation(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
            _logger.LogWarning("{ExceptionDetail}", ex.ToString());
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool ValidateDetail()
    {
        ValidationMessages.Clear();
        AddDetailValidationMessages();
        return ValidationMessages.Count == 0;
    }

    private bool IsDetailValid()
        => !string.IsNullOrWhiteSpace(EditorTitle) && EditorPriority >= 0;

    private void AddDetailValidationMessages()
    {
        if (string.IsNullOrWhiteSpace(EditorTitle))
            ValidationMessages.Add("Title is required.");
        if (EditorPriority < 0)
            ValidationMessages.Add("Priority cannot be negative.");
    }

    private bool ValidateDiagram()
    {
        ValidationMessages.Clear();
        AddDiagramValidationMessages();
        return ValidationMessages.Count == 0;
    }

    private bool IsDiagramValid()
    {
        var nodeIds = DiagramGraph.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        return DiagramGraph.Edges.All(edge => nodeIds.Contains(edge.Source) && nodeIds.Contains(edge.Target));
    }

    private void AddDiagramValidationMessages()
    {
        var nodeIds = DiagramGraph.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in DiagramGraph.Edges)
        {
            if (!nodeIds.Contains(edge.Source) || !nodeIds.Contains(edge.Target))
                ValidationMessages.Add($"Edge {edge.Id} references a missing node.");
        }
    }

    private void MarkDetailDirty()
    {
        if (SelectedUseCase is not null)
        {
            DetailDirty = true;
            OnPropertyChanged(nameof(CanSaveDetail));
        }
    }

    private static string NormalizeEdgeType(string? type)
        => type is "association" or "include" or "extend" or "generalization" ? type : "association";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
