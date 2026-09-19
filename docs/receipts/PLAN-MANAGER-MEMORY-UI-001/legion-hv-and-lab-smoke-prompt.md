# Codex / Legion prompt — live HV H-done + lab smoke

**Host:** PAYTON-LEGION2 (or any machine with a valid `AGENTS-README-FIRST.yaml` and a live MCP).  
**Do not run this on the Cloud VM.** That VM has no marker and `/health` on `:7147` is connection-refused.

**Hard rules**

- Do **not** invent AGREE scores.
- Do **not** publish add-profile / operator profile content.
- Do **not** set MCP TODO `PLAN-MANAGER-MEMORY-UI-001` `done: true` unless live hostile **OverallVerdict = AGREE** with **accuracy ≥ 98** and **completeness ≥ 98**, plus request jsonl + response jsonl + the full session-log verdict body on disk.

---

Paste everything below the line into Codex on Legion.

---

You are closing PLAN-MANAGER-MEMORY-UI-001 on `sharpninja/McpServerManager`.

Cloud VM already confirmed:

- `origin/main` tip **`e1636b34545c0b3af428f59df202a18ee23ec3e5`** = merge of PR #7 (`Merge pull request #7 from sharpninja/cursor/memory-ui-839a`).
- Focused Memory tests on that SHA: UI.Core **22** passed, Director **2** passed, Web **4** passed; Failed 0, Skipped 0.
- Live HV was **not** run on the Cloud VM (no `AGENTS-README-FIRST.yaml`; `curl` to `http://127.0.0.1:7147/health` failed connect).
- Follow-up PR #8 (`cursor/memory-completion-839a`) has workspace-reload / failed-load fixes + receipts. Merge it only if you want those on main before HV; HV claim may target `e1636b3` or a later main tip if you merge #8 first.

## 1. Session bootstrap

1. Read `AGENTS-README-FIRST.yaml` for `baseUrl` and `apiKey`.
2. `GET {baseUrl}/health` — keep the JSON (status/nonce). If this fails, **stop** and write a blocker. Do not fake HV.
3. `POST {baseUrl}/mcpserver/sessionlog` (`Add-McpSessionTurn` / `McpSession.psm1`). Keep session id.
4. `GET {baseUrl}/mcpserver/sessionlog?limit=5`
5. `GET {baseUrl}/mcpserver/todo` — locate `PLAN-MANAGER-MEMORY-UI-001`. Do not mark done yet.

## 2. Confirm main tip

```powershell
git fetch origin main
git checkout main
git pull origin main
git rev-parse HEAD
git log -1 --oneline
```

Re-run if you want a Legion receipt (Failed 0 / Skipped 0 required):

```powershell
dotnet test tests/McpServerManager.UI.Core.Tests --filter "FullyQualifiedName~Memory" -p:NuGetAudit=false --nologo
dotnet test tests/McpServerManager.Director.Tests --filter "FullyQualifiedName~Memory" -p:NuGetAudit=false --nologo
dotnet test tests/McpServerManager.Web.Tests --filter "FullyQualifiedName~Memory" -p:NuGetAudit=false --nologo
```

## 3. Lab smoke (live workspace — this VM cannot do Director/Web GUI)

Use marker `X-Api-Key` + `X-Workspace-Path` (or `McpServerClient.Memory`). Do not use operator/add-profile content.

| Verb | Call | Pass |
| --- | --- | --- |
| list | `GET /mcpserver/memory` (omit scope = Effective) | 200 + items/totalCount |
| get | `GET /mcpserver/memory/{id}` | 200; `version` present (display-only) |
| add | `POST /mcpserver/memory` `{ id, category, text, scope }` id `PLAN-MEMORY-SMOKE-001` | 200/201 success |
| update | `PUT /mcpserver/memory/{id}` `{ text }` only | 200; request has **no** `expectedVersion` |
| remove | `DELETE /mcpserver/memory/{id}` | 200; later get 404 |

Then GUI on Legion: Director Memory tab **after Sessions**; Web `/memory` NavLink **after Todos, before Triage**; detail version labeled display only.

Write `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/lab-smoke-live.md` with HTTP statuses, ids, versions.

## 4. Live hostile H-done (required for done:true)

Run the workspace **hostile HV / AGREE** tool (the one that emits `OverallVerdict`). Claim:

> Director + Mcp-Web expose Viewer Memory list/get/add/update/remove per PLAN-MANAGER-MEMORY-UI-001 DoD on main (`e1636b3` or current main tip). D4 Viewer; D11 version display-only; D6 SharpNinja.McpServer.Client ≥1.3.1 MemoryClient; AC16 five-verb Viewer tests; no Operator; no OCC.

Persist under `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/hv/`:

- `request.jsonl`
- `response.jsonl`
- `sessionlog-verdict.json` (full session-log entry containing OverallVerdict)

Gate: OverallVerdict **AGREE**, accuracy ≥ 98, completeness ≥ 98. If not, fix on `cursor/<name>-839a` and do **not** mark the TODO done.

## 5. Only after that gate

```powershell
Import-Module ./McpTodo.psm1
Initialize-McpSession
Complete-McpTodo -Id "PLAN-MANAGER-MEMORY-UI-001" -DoneSummary "main e1636b3 (PR #7); live HV AGREE accuracy/completeness ≥98; lab smoke list/get/add/update/remove in lab-smoke-live.md"
```

Attach the three HV files + `lab-smoke-live.md` in the session-log Response.
