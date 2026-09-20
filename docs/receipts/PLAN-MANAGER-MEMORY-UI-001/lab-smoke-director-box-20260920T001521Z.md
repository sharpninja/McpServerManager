# Lab smoke — Director Memory (headless FakeDriver, live MCP)

- UTC: 2026-09-20T00:15:21Z
- Local (America/Chicago): 2026-09-19T19:15:21 CT
- Manager HEAD: `e1636b34545c0b3af428f59df202a18ee23ec3e5`
- McpServer health: `{"status":"Healthy","version":"1.0.0\u002B720c2b49bede41449eda1ef8edab25296f7592e6","checks":[{"name":"self","status":"Healthy","description":null,"duration":0.0072},{"name":"upstream","status":"Healthy","description":"Federation disabled.","duration":0.004}],"storage":"reachable"}`
- MCP: `http://127.0.0.1:7147` (marker workspacePath=/workspace/repos/McpServerManager; no secrets logged)
- Content marker: `BOX-DIR-SMOKE-20260919`
- Assigned memory id (server-generated): `MEMORY-DIRSMOKE-002` (harness); xUnit allocated the next auto id after soft-delete

## How headless works

1. **Visual tree / access protocol:** `Application.Init(new FakeDriver())` then construct `MemoryScreen`, walk `View.Subviews` for buttons Refresh/Add/Edit/Remove/Filter.
2. **SynchronizationContext caveat:** Terminal.Gui installs a sync context; ViewModel `ConfigureAwait(true)` deadlocks without a UI pump. MCP five-verb awaits therefore run **after** `Application.Shutdown()`.
3. **Five verbs:** same Director DI path as the Memory tab (`DirectorHost` -> Memory list/detail ViewModels -> `MemoryApiClientAdapter` -> live MCP).

## Version display-only

- Confirmed: `DisplayVersion` only; no `EditorVersion`; update does not send `expectedVersion` (D11).
- `MemoryScreen` editor dialog uses Label text `Version: N (display only)`.
- Live UPDATE bumped Version **1->2**.

## Five verbs

| Verb | Result |
|------|--------|
| LIST | PASS (count=1) |
| ADD | PASS (id=MEMORY-DIRSMOKE-002 Version=1; marker=BOX-DIR-SMOKE-20260919) |
| GET | PASS (Version=1) |
| UPDATE | PASS (Version 1→2 display-only) |
| REMOVE | PASS |
| VISUAL_TREE | PASS |
| VERSION_DISPLAY_ONLY | PASS (DisplayVersion only; no EditorVersion; MemoryScreen Version Label is display-only) |

## Overall

**PASS**

## Artifacts

- Harness: `/workspace/deliverables/director-memory-headless-smoke` (log: `director-harness-run.log`)
- xUnit: `tests/McpServerManager.Director.Tests/MemoryScreenLiveSmokeTests.cs` — **Passed** (`director-live-smoke-run.log`)
- InternalsVisibleTo added for harness assembly `DirectorMemoryHeadlessSmoke` (local only; no PR/push)

## Notes

- Soft-deleted memory ids still conflict on ADD (duplicate check uses IgnoreQueryFilters). Smoke uses empty EditorId and server GenerateNextIdAsync.
- Prior receipt `lab-smoke-director-box-20260919T235547Z.md` was SKIP for interactive TUI; this run closes that gap headlessly.
- Earlier draft `lab-smoke-director-box-20260920T001412Z.md` was premature (before soft-delete ID fix); superseded by this file.
