using System.Text.Json;
using McpServer.Repl.Core;
using Microsoft.Extensions.AI;

namespace McpServerManager.Director;

/// <summary>
/// Adapts REPL workflow interfaces (<see cref="ITodoWorkflow"/>, <see cref="IRequirementsWorkflow"/>,
/// <see cref="IGenericClientPassthrough"/>) to <see cref="AIFunction"/> instances that can be registered
/// as tools in a <see cref="Microsoft.Agents.AI.ChatClientAgent"/>.
/// </summary>
internal sealed class ReplWorkflowToolAdapter
{
    private readonly ITodoWorkflow _todo;
    private readonly IRequirementsWorkflow _requirements;
    private readonly IGenericClientPassthrough _passthrough;

    public ReplWorkflowToolAdapter(
        ITodoWorkflow todo,
        IRequirementsWorkflow requirements,
        IGenericClientPassthrough passthrough)
    {
        _todo = todo ?? throw new ArgumentNullException(nameof(todo));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _passthrough = passthrough ?? throw new ArgumentNullException(nameof(passthrough));
    }

    /// <summary>
    /// Creates all REPL workflow tools as <see cref="AIFunction"/> instances.
    /// </summary>
    public IReadOnlyList<AIFunction> CreateFunctions() =>
    [
        // --- TODO tools ---
        CreateTool(
            (Func<string?, string?, string?, string?, bool?, CancellationToken, Task<string>>)TodoQueryAsync,
            "repl_todo_query",
            "Query TODO items with optional filters. Returns matching TODOs as JSON."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoGetAsync,
            "repl_todo_get",
            "Get a specific TODO item by its canonical ID (e.g. PLAN-API-001, ISSUE-17)."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoSelectAsync,
            "repl_todo_select",
            "Select a TODO as the active context. Subsequent update/delete operations can omit the ID."),
        CreateTool(
            (Func<string, string, string, string, string?, CancellationToken, Task<string>>)TodoCreateAsync,
            "repl_todo_create",
            "Create a new TODO item. ID format: PHASE-AREA-### or ISSUE-NEW for GitHub issue."),
        CreateTool(
            (Func<string, string?, string?, string?, bool?, CancellationToken, Task<string>>)TodoUpdateAsync,
            "repl_todo_update",
            "Update an existing TODO item. Only provided fields are changed."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoDeleteAsync,
            "repl_todo_delete",
            "Delete a TODO item by its canonical ID."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoAnalyzeRequirementsAsync,
            "repl_todo_analyze_requirements",
            "Analyze requirements traceability for a TODO item."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoStreamStatusAsync,
            "repl_todo_stream_status",
            "Stream status analysis for a TODO item. Returns collected events as JSON."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoStreamPlanAsync,
            "repl_todo_stream_plan",
            "Stream implementation plan for a TODO item. Returns collected events as JSON."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoStreamImplementAsync,
            "repl_todo_stream_implement",
            "Stream implementation execution for a TODO item. Returns collected events as JSON."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoProjectionStatusAsync,
            "repl_todo_projection_status",
            "Get projection status for a TODO (whether it has valid status/plan/implementation state)."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)TodoRepairProjectionAsync,
            "repl_todo_repair_projection",
            "Repair stale or corrupted projections for a TODO item."),

        // --- Requirements tools ---
        CreateTool(
            (Func<string?, string?, CancellationToken, Task<string>>)RequirementsListFrAsync,
            "repl_requirements_list_fr",
            "List functional requirements with optional area and status filters."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)RequirementsGetFrAsync,
            "repl_requirements_get_fr",
            "Get a functional requirement by ID (format: FR-AREA-###)."),
        CreateTool(
            (Func<string?, string?, string?, CancellationToken, Task<string>>)RequirementsListTrAsync,
            "repl_requirements_list_tr",
            "List technical requirements with optional area, subarea, and status filters."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)RequirementsGetTrAsync,
            "repl_requirements_get_tr",
            "Get a technical requirement by ID (format: TR-AREA-SUBAREA-###)."),
        CreateTool(
            (Func<string?, string?, CancellationToken, Task<string>>)RequirementsListTestAsync,
            "repl_requirements_list_test",
            "List test requirements with optional area and status filters."),
        CreateTool(
            (Func<string, CancellationToken, Task<string>>)RequirementsGetTestAsync,
            "repl_requirements_get_test",
            "Get a test requirement by ID (format: TEST-AREA-###)."),
        CreateTool(
            (Func<string?, string?, string?, CancellationToken, Task<string>>)RequirementsListMappingsAsync,
            "repl_requirements_list_mappings",
            "List requirement mappings with optional FR, TR, or TEST ID filters."),
        CreateTool(
            (Func<string, string, CancellationToken, Task<string>>)RequirementsGenerateDocumentAsync,
            "repl_requirements_generate_document",
            "Generate a requirements document. Format: 'markdown' or 'yaml'. DocType: 'fr', 'tr', 'test', or 'all'."),

        // --- Client passthrough tool ---
        CreateTool(
            (Func<string, string, string?, CancellationToken, Task<string>>)ClientInvokeAsync,
            "repl_client_invoke",
            "Dynamically invoke any McpServerClient sub-client method. clientName: e.g. 'context', 'github', 'workspace'. methodName: e.g. 'SearchAsync', 'ListIssuesAsync'. argsJson: optional JSON object of method arguments."),
    ];

    /// <summary>
    /// Creates <see cref="AITool"/> instances suitable for <see cref="Microsoft.Extensions.AI.ChatOptions.Tools"/>.
    /// </summary>
    public IReadOnlyList<AITool> CreateTools() => CreateFunctions().Cast<AITool>().ToList();

    // ──────────────────────────── TODO tool implementations ────────────────────────────

    private async Task<string> TodoQueryAsync(
        string? keyword, string? priority, string? section, string? id, bool? done,
        CancellationToken cancellationToken)
    {
        var result = await _todo.QueryAsync(keyword, priority, section, id, done, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            totalCount = result.TotalCount,
            items = result.Items.Select(i => new { i.Id, i.Title, i.Section, i.Priority, i.Done, i.Estimate })
        }, s_jsonOptions);
    }

    private async Task<string> TodoGetAsync(string id, CancellationToken cancellationToken)
    {
        var item = await _todo.GetAsync(id, cancellationToken);
        return SerializeTodoItem(item);
    }

    private async Task<string> TodoSelectAsync(string id, CancellationToken cancellationToken)
    {
        await _todo.SelectAsync(id, cancellationToken);
        var selection = _todo.CurrentSelection();
        return JsonSerializer.Serialize(new
        {
            message = $"Selected TODO {id}",
            selection = selection != null ? new { selection.Id, selection.Title, selection.Section, selection.Priority, selection.Done } : null
        }, s_jsonOptions);
    }

    private async Task<string> TodoCreateAsync(
        string id, string title, string section, string priority, string? estimate,
        CancellationToken cancellationToken)
    {
        var request = new SimpleTodoCreateRequest(id, title, section, priority, estimate);
        var result = await _todo.CreateAsync(request, cancellationToken);
        return JsonSerializer.Serialize(new { result.Success, item = new { result.Item.Id, result.Item.Title } }, s_jsonOptions);
    }

    private async Task<string> TodoUpdateAsync(
        string id, string? title, string? priority, string? section, bool? done,
        CancellationToken cancellationToken)
    {
        var request = new SimpleTodoUpdateRequest(title, priority, section, done);
        var result = await _todo.UpdateAsync(id, request, cancellationToken);
        return JsonSerializer.Serialize(new { result.Success, item = new { result.Item.Id, result.Item.Title, result.Item.Done } }, s_jsonOptions);
    }

    private async Task<string> TodoDeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _todo.DeleteAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, message = $"Deleted TODO {id}" }, s_jsonOptions);
    }

    private async Task<string> TodoAnalyzeRequirementsAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _todo.AnalyzeRequirementsAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            result.TodoId,
            result.AllRequirementsExist,
            functionalRequirements = result.FunctionalRequirements.Select(r => new { r.Id, r.Title, r.Exists }),
            technicalRequirements = result.TechnicalRequirements.Select(r => new { r.Id, r.Title, r.Exists }),
        }, s_jsonOptions);
    }

    private async Task<string> TodoStreamStatusAsync(string id, CancellationToken cancellationToken)
    {
        var events = new List<object>();
        await _todo.StreamStatusAsync(id, evt =>
        {
            events.Add(new { evt.EventType, evt.Data, evt.Sequence });
            return Task.CompletedTask;
        }, cancellationToken);
        return JsonSerializer.Serialize(new { todoId = id, events }, s_jsonOptions);
    }

    private async Task<string> TodoStreamPlanAsync(string id, CancellationToken cancellationToken)
    {
        var events = new List<object>();
        await _todo.StreamPlanAsync(id, evt =>
        {
            events.Add(new { evt.EventType, evt.Data, evt.Sequence });
            return Task.CompletedTask;
        }, cancellationToken);
        return JsonSerializer.Serialize(new { todoId = id, events }, s_jsonOptions);
    }

    private async Task<string> TodoStreamImplementAsync(string id, CancellationToken cancellationToken)
    {
        var events = new List<object>();
        await _todo.StreamImplementAsync(id, evt =>
        {
            events.Add(new { evt.EventType, evt.Data, evt.Sequence });
            return Task.CompletedTask;
        }, cancellationToken);
        return JsonSerializer.Serialize(new { todoId = id, events }, s_jsonOptions);
    }

    private async Task<string> TodoProjectionStatusAsync(string id, CancellationToken cancellationToken)
    {
        var status = await _todo.GetProjectionStatusAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            status.TodoId, status.HasStatus, status.HasPlan, status.HasImplementation,
            status.LastUpdated, status.IsStale
        }, s_jsonOptions);
    }

    private async Task<string> TodoRepairProjectionAsync(string id, CancellationToken cancellationToken)
    {
        await _todo.RepairProjectionAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, message = $"Repaired projections for {id}" }, s_jsonOptions);
    }

    // ──────────────────────────── Requirements tool implementations ────────────────────────────

    private async Task<string> RequirementsListFrAsync(string? area, string? status, CancellationToken cancellationToken)
    {
        var result = await _requirements.ListFrAsync(area, status, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            result.TotalCount,
            items = result.Items.Select(i => new { i.Id, i.Title, i.Area, i.Status })
        }, s_jsonOptions);
    }

    private async Task<string> RequirementsGetFrAsync(string id, CancellationToken cancellationToken)
    {
        var item = await _requirements.GetFrAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new { item.Id, item.Title, item.Area, item.Status, item.Description }, s_jsonOptions);
    }

    private async Task<string> RequirementsListTrAsync(string? area, string? subarea, string? status, CancellationToken cancellationToken)
    {
        var result = await _requirements.ListTrAsync(area, subarea, status, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            result.TotalCount,
            items = result.Items.Select(i => new { i.Id, i.Title, i.Area, i.Subarea, i.Status })
        }, s_jsonOptions);
    }

    private async Task<string> RequirementsGetTrAsync(string id, CancellationToken cancellationToken)
    {
        var item = await _requirements.GetTrAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new { item.Id, item.Title, item.Area, item.Subarea, item.Status, item.Description }, s_jsonOptions);
    }

    private async Task<string> RequirementsListTestAsync(string? area, string? status, CancellationToken cancellationToken)
    {
        var result = await _requirements.ListTestAsync(area, status, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            result.TotalCount,
            items = result.Items.Select(i => new { i.Id, i.Title, i.Area, i.Status })
        }, s_jsonOptions);
    }

    private async Task<string> RequirementsGetTestAsync(string id, CancellationToken cancellationToken)
    {
        var item = await _requirements.GetTestAsync(id, cancellationToken);
        return JsonSerializer.Serialize(new { item.Id, item.Title, item.Area, item.Status, item.Description }, s_jsonOptions);
    }

    private async Task<string> RequirementsListMappingsAsync(
        string? frId, string? trId, string? testId, CancellationToken cancellationToken)
    {
        var result = await _requirements.ListMappingsAsync(frId, trId, testId, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            result.TotalCount,
            items = result.Items.Select(i => new { i.FrId, i.TrId, i.TestId })
        }, s_jsonOptions);
    }

    private async Task<string> RequirementsGenerateDocumentAsync(
        string format, string docType, CancellationToken cancellationToken)
    {
        var result = await _requirements.GenerateDocumentAsync(format, docType, cancellationToken);
        return JsonSerializer.Serialize(new { result.Content, result.Format }, s_jsonOptions);
    }

    // ──────────────────────────── Client passthrough tool implementation ────────────────────────────

    private async Task<string> ClientInvokeAsync(
        string clientName, string methodName, string? argsJson, CancellationToken cancellationToken)
    {
        var arguments = string.IsNullOrWhiteSpace(argsJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson, s_jsonOptions) ?? new Dictionary<string, object?>();

        var result = await _passthrough.InvokeAsync(clientName, methodName, arguments, cancellationToken);
        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private static AIFunction CreateTool(Delegate implementation, string name, string description) =>
        AIFunctionFactory.Create(
            implementation,
            new AIFunctionFactoryOptions
            {
                Description = description,
                Name = name,
            });

    private static string SerializeTodoItem(ITodoItem item) =>
        JsonSerializer.Serialize(new
        {
            item.Id, item.Title, item.Section, item.Priority, item.Done,
            item.Estimate, item.Note, item.Description, item.TechnicalDetails,
            implementationTasks = item.ImplementationTasks.Select(t => new { t.Task, t.Done }),
            item.CompletedDate, item.DoneSummary, item.Remaining,
            item.DependsOn, item.FunctionalRequirements, item.TechnicalRequirements,
        }, s_jsonOptions);

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // ──────────────────────────── Simple request DTOs ────────────────────────────

    private sealed class SimpleTodoCreateRequest : ITodoCreateRequest
    {
        public SimpleTodoCreateRequest(string id, string title, string section, string priority, string? estimate)
        {
            Id = id;
            Title = title;
            Section = section;
            Priority = priority;
            Estimate = estimate;
        }

        public string Id { get; }
        public string Title { get; }
        public string Section { get; }
        public string Priority { get; }
        public string? Estimate { get; }
        public IReadOnlyList<string>? Description => null;
        public IReadOnlyList<string>? TechnicalDetails => null;
        public IReadOnlyList<ITodoSubtask>? ImplementationTasks => null;
        public string? Note => null;
        public string? Remaining => null;
        public IReadOnlyList<string>? DependsOn => null;
        public IReadOnlyList<string>? FunctionalRequirements => null;
        public IReadOnlyList<string>? TechnicalRequirements => null;
    }

    private sealed class SimpleTodoUpdateRequest : ITodoUpdateRequest
    {
        public SimpleTodoUpdateRequest(string? title, string? priority, string? section, bool? done)
        {
            Title = title;
            Priority = priority;
            Section = section;
            Done = done;
        }

        public string? Title { get; }
        public string? Priority { get; }
        public string? Section { get; }
        public bool? Done { get; }
        public string? Estimate => null;
        public IReadOnlyList<string>? Description => null;
        public IReadOnlyList<string>? TechnicalDetails => null;
        public IReadOnlyList<ITodoSubtask>? ImplementationTasks => null;
        public string? Note => null;
        public string? CompletedDate => null;
        public string? DoneSummary => null;
        public string? Remaining => null;
        public IReadOnlyList<string>? DependsOn => null;
        public IReadOnlyList<string>? FunctionalRequirements => null;
        public IReadOnlyList<string>? TechnicalRequirements => null;
    }
}
