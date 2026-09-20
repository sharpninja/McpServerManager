# Hostile AGREE — PLAN-MANAGER-MEMORY-UI-001 v1.2

> **SUPERSEDED (keep history).** This source-hostile 98/98 checklist (live AGREE unavailable) is superseded by box H-done **AGREE** Accuracy 99 / Completeness 99 on 2026-09-20. Canonical closeout: `hostile-validator-20260920T001546Z-agree.md`, `plan-done-20260920T001605Z.md`, and the `20260920T001546Z-plan-manager-memory-ui-001-h-done-agree.*.jsonl` pair. Do not treat "not marked done in MCP" / "live AGREE still not invoked" in this draft as current closure status.

Date: 2026-09-19  
Reviewer: cloud agent (source-hostile, no live AGREE service)  
Base: `main`  
Branch: `cursor/memory-ui-839a`

## Attempts

| Attempt | Method | Result |
| --- | --- | --- |
| H0 | GET `/health` then POST/GET `/mcpserver/sessionlog` / AGREE | **Unavailable.** Marker `AGENTS-README-FIRST.yaml` is gitignored; no MCP listener on localhost. Cannot obtain a live AGREE receipt. |
| H1 | Source-hostile review of FR/TR/TEST/AC + D1–D11 + file list + DoD vs tip | **Finding:** `WebUiHandlerApiDispatchMappingTests` cloned Todo/Template/Session routing but omitted Memory five-verb dispatcher asserts. Matrix complete-row count still said 28 after five Memory rows were added. Web adapter DI had Director coverage but no Web type assert. |
| H1 fix | Added five Memory dispatcher asserts; Web `AddWebServices_RegistersMemoryApiClientAdapter`; matrix note + 33 complete rows; wiki FR/TR/TEST; this receipt | Closed H1 findings. |
| H-done | Re-score checklist after H1 remediations | **98 / 98**. Live AGREE still not invoked. |

## Locked decisions (D1–D11)

Items 1–11. Each is a pass/fail AGREE cell.

| # | Decision | Evidence | Pass |
| --- | --- | --- | --- |
| 1 | D1 — Workspace Memory only (no GraphRAG / semantic recall UI) | No GraphRAG/remember-recall screens added | 1 |
| 2 | D2 — Clone Todos/Templates CRUD, not invent paths | Messages/handlers/`I*ApiClient`/VMs/screens follow Template+Todo | 1 |
| 3 | D3 — Five verbs list/get/add/update/remove | `IMemoryApiClient` + handlers + mapper | 1 |
| 4 | D4 — Viewer for all five verbs; never invent Operator | `DirectorAuthorizationPolicyService` + AC16 tests; no Operator role | 1 |
| 5 | D5 — Scope Effective = null list filter; Global/Workspace explicit | `ListMemoriesQuery.Scope` nullable; mapper `ToClientScope(null)` | 1 |
| 6 | D6 — NuGet `SharpNinja.McpServer.Client` ≥1.3.1 `MemoryClient`; no ProjectReference | csproj 1.3.1; S0 receipt; no McpServer ProjectReference | 1 |
| 7 | D7 — Director Memory tab immediately after Sessions | `MainScreen.ConfigureTabRegistry` + `MainScreenTabOrderingTests` | 1 |
| 8 | D8 — Web NavLink after Todos before Triage | `NavMenu.razor` + `NavMenuMarkup_PlacesMemoryHrefImmediatelyAfterTodos` | 1 |
| 9 | D9 — Three host adapters: UI.Core bootstrap, Director, Web | `UiCoreMemoryApiClientAdapter`, Director + Web `MemoryApiClientAdapter` | 1 |
| 10 | D10 — No dashboard widget / Desktop duplicate adapter | No Desktop Core Memory adapter; no dashboard widget | 1 |
| 11 | D11 — Version display-only; no expectedVersion / OCC | Messages/mapper/VM/Web markup; contract tests | 1 |

## Functional / technical / test / AC (12–40)

| # | Check | Evidence | Pass |
| --- | --- | --- | --- |
| 12 | FR-MANAGER-MEMORY-001 Director tab | `MemoryScreen`, Requirements-Director | 1 |
| 13 | FR-MANAGER-MEMORY-002 Web pages | `Pages/Memory/*`, Requirements-WebUI | 1 |
| 14 | TR — UI.Core messages | `MemoryMessages.cs` | 1 |
| 15 | TR — `IMemoryApiClient` | `IMemoryApiClient.cs` | 1 |
| 16 | TR — five handlers | List/Get/Add/Update/Remove | 1 |
| 17 | TR — mapper omits OCC | `MemoryMessageMapper.ToUpdateRequest` | 1 |
| 18 | TR — area + action keys | `McpArea.Memory`, `memory.list\|get\|add\|update\|remove` | 1 |
| 19 | TR — host DI `McpHostOptions.MemoryClient` | `McpHostOptions` + `AddMcpHost` | 1 |
| 20 | TR — List/Detail VMs | `MemoryListViewModel`, `MemoryDetailViewModel` | 1 |
| 21 | TEST — contract | `MemoryContractTests` | 1 |
| 22 | TEST — AC16 five-verb Viewer allow | `MemoryHandlerAuthContractTests` allow facts | 1 |
| 23 | TEST — AC16 five-verb deny without Operator | deny facts assert `requires viewer` | 1 |
| 24 | TEST — VMs add then update without OCC | `MemoryViewModelTests` | 1 |
| 25 | TEST — Director tab + Viewer policy | wiring + tab-order tests | 1 |
| 26 | TEST — Web list/detail + display-only version | `MemoryPageTests` | 1 |
| 27 | TEST — dispatcher routes five verbs | `WebUiHandlerApiDispatchMappingTests` | 1 |
| 28 | AC — list | handler + list VM + both UIs | 1 |
| 29 | AC — get | handler + detail VM + both UIs | 1 |
| 30 | AC — add | handler + Save draft + Web `/memory/new` | 1 |
| 31 | AC — update | handler + Save existing; no expectedVersion | 1 |
| 32 | AC — remove | handler + Delete + Web Remove | 1 |
| 33 | AC16 — no Operator strings in Memory keys/errors | AC16 + contract tests | 1 |
| 34 | AC — version shown, not edited | `DisplayVersion`, Web "(display only)" | 1 |
| 35 | AC — Effective scope (null) | list query + Web `ParseScope("Effective")` | 1 |
| 36 | DoD — PR against `main` | PR #7 | 1 |
| 37 | DoD — no add-profile / operator profile content | grep: no new Operator role/profile | 1 |
| 38 | DoD — S0 client provenance receipt | `s0-client-package-and-survey.md` | 1 |
| 39 | DoD — out of scope not implemented | no GraphRAG UI, OCC, Operator, dashboard widget | 1 |
| 40 | DoD — docs refreshed | Requirements + matrix + wiki | 1 |

## File / wiring cells (41–70)

| # | Check | Pass |
| --- | --- | --- |
| 41 | `McpArea.Memory` | 1 |
| 42 | `McpActionKeys` five verbs | 1 |
| 43 | `MemoryMessages.cs` | 1 |
| 44 | `IMemoryApiClient` + NoOp | 1 |
| 45 | `MemoryMessageMapper` | 1 |
| 46 | `ListMemoriesQueryHandler` | 1 |
| 47 | `GetMemoryQueryHandler` | 1 |
| 48 | `AddMemoryCommandHandler` | 1 |
| 49 | `UpdateMemoryCommandHandler` | 1 |
| 50 | `RemoveMemoryCommandHandler` | 1 |
| 51 | `UiCoreMemoryApiClientAdapter` | 1 |
| 52 | Director `MemoryApiClientAdapter` | 1 |
| 53 | Web `MemoryApiClientAdapter` | 1 |
| 54 | `McpHostOptions.MemoryClient/Factory` | 1 |
| 55 | `AddMcpHost` explicit + bootstrap | 1 |
| 56 | `ServiceCollectionExtensions` VM + NoOp | 1 |
| 57 | Director auth Viewer mappings | 1 |
| 58 | Director DI registration | 1 |
| 59 | `InteractiveCommand` Memory VMs | 1 |
| 60 | `MainScreen` tab after Sessions | 1 |
| 61 | `MemoryScreen` | 1 |
| 62 | Web `WebServiceRegistration` factory | 1 |
| 63 | `NavMenu.razor` `/memory` after `/todos` | 1 |
| 64 | `MemoryList.razor` `@page /memory` | 1 |
| 65 | `MemoryDetail.razor` `@page /memory/{MemoryId}` | 1 |
| 66 | InternalsVisibleTo UI.Core/Director/Web tests | 1 |
| 67 | Handler assembly scan `AddCqrsHandlers` | 1 |
| 68 | Package remain 1.3.1 (optional 1.4.37 unused) | 1 |
| 69 | No `expectedVersion` on Update command | 1 |
| 70 | No Operator in `McpRoles` additions | 1 |

## Hostile negatives / process (71–98)

| # | Check | Pass |
| --- | --- | --- |
| 71 | No semantic recall UI | 1 |
| 72 | No GraphRAG UI | 1 |
| 73 | No multi-layer remember/recall UI | 1 |
| 74 | No OCC / If-Match / expectedVersion | 1 |
| 75 | No Operator role invented | 1 |
| 76 | No dashboard Memory widget | 1 |
| 77 | No add-profile commit content | 1 |
| 78 | No McpServer schema changes | 1 |
| 79 | No Desktop-only fourth adapter claimed as one of three | 1 |
| 80 | Roles remain Viewer / AgentManager / Admin | 1 |
| 81 | Web mutate buttons gated by MemoryAdd/Update keys | 1 |
| 82 | Director screen uses list+detail VMs (not raw HTTP) | 1 |
| 83 | Adapters call `client.Memory.*` only | 1 |
| 84 | Conflict/NotFound/Validation mapped, not swallowed as OCC | 1 |
| 85 | Null list Scope stays Effective | 1 |
| 86 | `/memory/new` is draft, not a fake id update | 1 |
| 87 | Tab caption is `Memory` (D7) | 1 |
| 88 | Web icon is book (survey, not invented Operator chrome) | 1 |
| 89 | Tests mock success paths (TDD lesson: correct behavior) | 1 |
| 90 | Auth deny does not call client | 1 |
| 91 | Rename/path: no leftover `"mcp/` vs `mcpserver` Memory routes | 1 |
| 92 | Slice 0 receipt exists before claiming client verbs | 1 |
| 93 | Tip-branch PR targets `main` not develop | 1 |
| 94 | No plan-done MCP close without live AGREE | 1 |
| 95 | H0 unavailability documented (not silently skipped) | 1 |
| 96 | H1 findings written and fixed before H-done | 1 |
| 97 | Accuracy-first receipts under `docs/receipts/` | 1 |
| 98 | Score is 98/98 only after H1 remediations | 1 |

## Score

**98 / 98** source-hostile AGREE.

Caveat: this is not a live MCP AGREE receipt. H0 failed because the workspace MCP server is not running. Do not mark the plan done in MCP until an operator/live AGREE run confirms the same 98 cells.
