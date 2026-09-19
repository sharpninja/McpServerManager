# PLAN-MANAGER-MEMORY-UI-001 Slice 0

Date: 2026-09-19  
Branch: `cursor/memory-ui-839a`  
Base: `main`

## Client package provenance

| Fact | Receipt |
| --- | --- |
| Package | `SharpNinja.McpServer.Client` **1.3.1** |
| Assembly | `McpServer.Client, Version=1.3.1.0` |
| Restored path | `.nuget/packages/sharpninja.mcpserver.client/1.3.1/lib/net10.0/McpServer.Client.dll` |
| Facade property | `McpServerClient.Memory` → `McpServer.Client.MemoryClient` |
| Bump | Not required. Operator D6 allows ≥1.3.1; Manager already on 1.3.1. No `ProjectReference` to McpServer. |

`MemoryClient` verbs (reflection of 1.3.1):

- `ListAsync(MemoryScope?, string? category, string? keyword, CancellationToken)` → `MemoryQueryResult`
- `GetAsync(string id, CancellationToken)` → `MemoryItem`
- `AddAsync(MemoryAddRequest, CancellationToken)` → `MemoryMutationResult`
- `UpdateAsync(string id, MemoryUpdateRequest, CancellationToken)` → `MemoryMutationResult`
- `RemoveAsync(string id, CancellationToken)` → `MemoryMutationResult`

Supporting models present: `MemoryScope`, `MemoryItem` (includes `Version`), `MemoryAddRequest`, `MemoryUpdateRequest` (no `expectedVersion`), `MemoryMutationResult`, `MemoryMutationFailureKind`.

D11 confirmed at the client: version is a display field on `MemoryItem`; update request has no OCC/expectedVersion.

## Survey — clone Todos, do not invent roles/paths

| Surface | Existing pattern cloned |
| --- | --- |
| CQRS messages | `Messages/TodoMessages.cs` + `Messages/TemplateMessages.cs` (CRUD closer to Memory) |
| API client | `ITodoApiClient` / `ITemplateApiClient` + host adapters |
| Auth keys | `McpActionKeys.TodoList/Get/Create/Update/Delete` → Viewer in `DirectorAuthorizationPolicyService` |
| Roles | `McpRoles.Viewer` / `AgentManager` / `Admin` only. No Operator. |
| Handlers | `ListTodosQueryHandler` etc.: validate → `CanExecuteAction` → client → `Result` |
| Host DI | `McpHostOptions.TodoClient` + `AddMcpHost` explicit/bootstrap paths |
| Three adapters | UI.Core bootstrap `UiCoreTodoApiClientAdapter`; Director `TodoApiClientAdapter`; Web `TodoApiClientAdapter` |
| List/Detail VMs | `TemplateListViewModel` / `TemplateDetailViewModel` (CRUD without prompt/OCC) |
| Director tabs | `MainScreen.ConfigureTabRegistry`: TODO then Sessions; D7 inserts Memory immediately after Sessions |
| Web nav | `NavMenu.razor`: `/todos` then `/triage`; D/S6 inserts `/memory` after Todos before Triage |
| Web pages | `Pages/Templates/TemplateList.razor` + `TemplateDetail.razor` |

## Locked decisions applied

- D4: Viewer for list/get/add/update/remove
- D6: NuGet 1.3.1 `MemoryClient` (no ProjectReference)
- D7: Director tab after Sessions
- D11: Version display-only
- AC16: five-verb Viewer auth-contract tests; no Operator strings
