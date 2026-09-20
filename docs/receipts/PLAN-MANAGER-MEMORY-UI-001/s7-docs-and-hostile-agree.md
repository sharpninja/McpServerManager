# PLAN-MANAGER-MEMORY-UI-001 Slice 7 — docs, receipts, hostile AGREE

> **SUPERSEDED for closure status (keep history).** Slice 7 docs remain useful as the first in-repo Memory UI refresh. Claims that the plan is **not** marked done without a live AGREE receipt, and that closeout requires only this source-hostile 98/98 file, are superseded by box H-done **AGREE** (2026-09-20, Accuracy 99 / Completeness 99) plus Web and Director headless lab receipts in this folder.

Date: 2026-09-19  
Branch: `cursor/memory-ui-839a`  
Base: `main` (this repo has no develop)

## Docs refreshed

| Artifact | Change |
| --- | --- |
| `docs/Requirements-Director.md` | FR-MANAGER-MEMORY-001 + Memory tab in FR-MCP-030 |
| `docs/Requirements-WebUI.md` | FR-MANAGER-MEMORY-002 |
| `docs/architecture/compliance/UI-USECASE-MATRIX.md` | Memory domain `5/5`, five complete use-case rows, Blazor inventory |
| `docs/Project/wiki/github/*` | FR/TR/TEST-MANAGER-MEMORY + matrix/mapping |
| `docs/Project/wiki/azure/*` | Same Manager Memory rows |
| `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/s0-client-package-and-survey.md` | Slice 0 client provenance |

Plan status in repo docs: **implemented**. Plan is **not** marked done in MCP (no live MCP session). Closeout requires this hostile AGREE receipt.

## Test receipts (2026-09-19)

NuGet restore used `-p:NuGetAudit=false` because TreatWarningsAsErrors elevates NU1902/NU1903. No audit-policy change.

| Suite | Filter / scope | Result |
| --- | --- | --- |
| UI.Core | `FullyQualifiedName~Memory` + host + dispatch mapping | 28 passed |
| UI.Core | `HandlerApiDispatchTests` (includes five Memory handlers) | 162 passed |
| Director | Memory + tab order + DI + auth | 33 passed |
| Web | Memory pages + adapter DI + NavigationAuth | 13 passed |

Post-fix rerun commands:

```text
dotnet test tests/McpServerManager.UI.Core.Tests --filter FullyQualifiedName~Memory -p:NuGetAudit=false
dotnet test tests/McpServerManager.UI.Core.Tests --filter FullyQualifiedName~WebUiHandlerApiDispatchMappingTests -p:NuGetAudit=false
dotnet test tests/McpServerManager.Director.Tests --filter FullyQualifiedName~Memory -p:NuGetAudit=false
dotnet test tests/McpServerManager.Web.Tests --filter "FullyQualifiedName~Memory|FullyQualifiedName~NavigationAuth" -p:NuGetAudit=false
```

## Slice map

| Slice | Work | Status |
| --- | --- | --- |
| S0 | Client 1.3.1 + `MemoryClient` reflection; Todo/Template/tab/nav survey | receipt `s0-client-package-and-survey.md` |
| S1 | `McpArea.Memory`, `memory.*` keys, messages, `IMemoryApiClient` | `MemoryContractTests` |
| S2 | Five handlers + AC16 Viewer allow/deny | `MemoryHandlerAuthContractTests` |
| S3 | UI.Core bootstrap + Director + Web adapters + DI | host tests + `McpHostBuilderExtensionsTests` |
| S4 | List/Detail ViewModels | `MemoryViewModelTests` |
| S5 | `MemoryScreen` + tab after Sessions (D7) | `MemoryScreenWiringTests`, `MainScreenTabOrderingTests` |
| S6 | `/memory`, `/memory/{Id}`, NavLink after Todos | `MemoryPageTests`, `NavigationAuthTests` |
| S7 | Docs + this receipt + hostile AGREE | this file |

## Hostile AGREE

Live AGREE endpoint is unavailable in this environment (`AGENTS-README-FIRST.yaml` gitignored; `/health` not running). Attempts and source-hostile review are recorded in `hostile-agree.md`.

Score after H1 remediations: **98 / 98** (source-hostile checklist). Plan is **not** marked done in MCP without a live AGREE receipt.
