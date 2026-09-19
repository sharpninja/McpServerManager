# Codex / Legion prompt — live HV + lab smoke for PLAN-MANAGER-MEMORY-UI-001

Copy this prompt onto **PAYTON-LEGION2** (or any host with a valid `AGENTS-README-FIRST.yaml` and running MCP). Do not run this as a substitute on a VM that lacks `/health`.

---

You are completing PLAN-MANAGER-MEMORY-UI-001 on `sharpninja/McpServerManager` after PR #7 (Memory UI). **Do not invent AGREE scores. Do not publish add-profile / operator profile content. Do not set MCP TODO `done: true` unless live hostile OverallVerdict is AGREE with accuracy≥98 and completeness≥98.**

## Preconditions

1. Read workspace `AGENTS-README-FIRST.yaml` (gitignored marker) for `baseUrl` and `apiKey`.
2. `GET {baseUrl}/health` and keep the JSON body (nonce/status). If health fails, **stop**. Write a blocker receipt. Do not fake HV.
3. `POST {baseUrl}/mcpserver/sessionlog` for this turn (`Add-McpSessionTurn` / `McpSession.psm1`). Keep the session id.
4. `GET {baseUrl}/mcpserver/sessionlog?limit=5`
5. `GET {baseUrl}/mcpserver/todo` and find `PLAN-MANAGER-MEMORY-UI-001` (do not mark done yet).

## Confirm merge tip

```powershell
git fetch origin main
git checkout main
git pull origin main
git log -1 --oneline
```

Focused tests (Failed 0, Skipped 0 required):

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"
dotnet test tests/McpServerManager.UI.Core.Tests --filter "FullyQualifiedName~Memory" -p:NuGetAudit=false --nologo
dotnet test tests/McpServerManager.Director.Tests --filter "FullyQualifiedName~Memory" -p:NuGetAudit=false --nologo
dotnet test tests/McpServerManager.Web.Tests --filter "FullyQualifiedName~Memory" -p:NuGetAudit=false --nologo
```

## Lab smoke (memory-enabled workspace)

Using `McpServerClient.Memory` or HTTP (`X-Api-Key`, `X-Workspace-Path` from marker):

| Step | Call | Pass if |
| --- | --- | --- |
| list | `GET /mcpserver/memory` (omit scope = Effective) | 200 + items/totalCount |
| get | `GET /mcpserver/memory/{id}` | 200 + version field present |
| add | `POST /mcpserver/memory` `{ category, text, scope }` id `PLAN-MEMORY-SMOKE-001` | 200/201 success |
| update | `PUT /mcpserver/memory/{id}` `{ text }` only | 200; **no** `expectedVersion` in request |
| remove | `DELETE /mcpserver/memory/{id}` | 200; subsequent get 404 |

Also click/smoke Director Memory tab (after Sessions) and Web `/memory` (NavLink after Todos, before Triage). Version is display-only.

Write `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/lab-smoke-live.md` with statuses, ids, versions.

## Live hostile H-done (required)

Run the **standard hostile HV / AGREE** pipeline used in this workspace (same tool that emits OverallVerdict). Target claim:

> Director + Mcp-Web expose Viewer Memory list/get/add/update/remove per PLAN-MANAGER-MEMORY-UI-001 DoD; D4 Viewer; D11 version display-only; D6 client ≥1.3.1 MemoryClient; AC16 five-verb Viewer tests; no Operator; no OCC; PR merged to main.

Requirements:

- Hostile stance (find faults; do not rubber-stamp)
- Persist **request jsonl** and **response jsonl** under `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/hv/`
- Persist the **full session-log verdict body** (the sessionlog entry that contains OverallVerdict)
- OverallVerdict must be **AGREE**
- accuracy ≥ 98
- completeness ≥ 98

If OverallVerdict is not AGREE, or scores are below 98, **do not** mark the TODO done. File findings and fix on a `cursor/…-839a` branch.

## Only after live AGREE

```powershell
Import-Module ./McpTodo.psm1
Initialize-McpSession
Complete-McpTodo -Id "PLAN-MANAGER-MEMORY-UI-001" -DoneSummary "Merged Memory UI to main; live HV AGREE accuracy/completeness ≥98; lab smoke list/get/add/update/remove recorded."
```

Attach HV jsonl paths and lab-smoke-live.md path in the session-log Response.

If MCP TODO write fails after a valid live AGREE, leave `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/awaiting-mcp-done-write.md` updated with HV paths and the exact TODO PUT error.
