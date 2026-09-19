# Lab smoke checklist — PLAN-MANAGER-MEMORY-UI-001

Date: 2026-09-19  
VM: Cursor Cloud Agent (no live MCP, no memory-enabled workspace)

## Scope on this VM

**Unit-level only.** Live list/get/add/update/remove against a memory-enabled workspace was **not** possible (see `live-hv-blocker.md`).

## Unit-level smoke (this VM)

| Verb | Covered by | Live workspace |
| --- | --- | --- |
| list | `MemoryHandlerAuthContractTests.List_*`, `MemoryViewModelTests` list load, Web `MemoryListPage_RendersItems` | No |
| get | `Get_*` handler tests, `MemoryDetailViewModel` load tests, Web detail render | No |
| add | `Add_*` handler tests, VM Save draft → `AddMemoryAsync` | No |
| update | `Update_*` handler tests, VM Save existing without OCC | No |
| remove | `Remove_*` handler tests, VM `DeleteAsync` | No |

Focused Memory suites on **`main` tip `e1636b3`** (PR #7 merge; Failed 0, Skipped 0):

| Suite | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| UI.Core `FullyQualifiedName~Memory` | 22 | 0 | 0 |
| Director `FullyQualifiedName~Memory` | 2 | 0 | 0 |
| Web `FullyQualifiedName~Memory` | 4 | 0 | 0 |

Post-merge follow-up (workspace reload + failed-load reset) adds 2 UI.Core + 1 Web tests on `cursor/memory-completion-839a`.

## Live workspace checklist (Legion — not run here)

Use a workspace whose marker has a working API key and Memory enabled. Do **not** use add-profile / operator profile content.

1. `GET /health` — nonce/status OK
2. `GET /mcpserver/memory` (Effective / null scope) — list returns
3. `GET /mcpserver/memory/{id}` — get one row; note `version` display field
4. `POST /mcpserver/memory` — add `PLAN-MANAGER-MEMORY-UI-001-smoke` category `lab` text `smoke add`
5. `PUT /mcpserver/memory/{id}` — update text only; confirm request body has **no** `expectedVersion`
6. `DELETE /mcpserver/memory/{id}` — remove the smoke row
7. Director: Memory tab after Sessions; list/get/add/update/remove the same row
8. Web: `/memory` after Todos before Triage; `/memory/{id}` version labeled display only

Record HTTP status, ids, and version numbers in `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/lab-smoke-live.md` on Legion. Do not mark the plan done until that file and live HV jsonl exist.
