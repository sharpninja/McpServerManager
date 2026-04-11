# Phase 0.1 — Investigation of `d0617b0 Fixed REPL host`

**Date:** 2026-04-11
**Worktree:** `mcp-localdb-migration`
**Plan reference:** `mcp-server-db-sync.md` (Phase 0.1)

## Question

The `lib/McpServer` GitHub-mirror submodule contains commit `d0617b0 Fixed REPL host` which is **not present on Azure DevOps** (the canonical source-of-truth remote for the McpServer library). Per Phase 0.1 of the plan, decide whether to:

- (a) cherry-pick `d0617b0` to Azure `develop` and push,
- (b) discard it because the same fix exists on Azure under a different commit, or
- (c) discard it because the work has been superseded.

## Findings

### Commit footprint

`d0617b0` is **substantial** — not a small fix:

```
src/McpServer.Repl.Host/AgentStdioHandler.cs       | 616 +++++++++++++++++++--
src/McpServer.Repl.Host/McpServer.Repl.Host.csproj |   1 +
.../ServiceCollectionExtensions.cs                 |  27 +-
.../ReplChildProcessHelper.cs                      | 102 +++-
4 files changed, 694 insertions(+), 52 deletions(-)
```

**What it does:** converts the previous `AgentStdioHandler` skeleton into a fully-functional handler with:

- YAML envelope serialization/deserialization (using `YamlDotNet` directly inside the handler)
- Constructor-injected `ISessionLogWorkflow`, `ITodoWorkflow`, `IGenericClientPassthrough`
- Per-workspace nonce tracking via `Dictionary<string, string>`
- Workspace path resolution via `MarkerFileClientOptionsResolver`

It is **net-new functionality**, not a bug fix. The `Fixed REPL host` commit message is misleading — this is a feature implementation.

### Azure-side state of the same files

Searching `91ed4a6..54d3ab8` (Azure `develop`) for any commit touching the four files in `d0617b0`'s footprint:

| File | Azure commits |
|---|---|
| `src/McpServer.Repl.Host/AgentStdioHandler.cs` | **0** |
| `src/McpServer.Repl.Host/ReplChildProcessHelper.cs` | **0** |
| `src/McpServer.Repl.Host/ServiceCollectionExtensions.cs` | **0** |
| `src/McpServer.Repl.Host/McpServer.Repl.Host.csproj` | **0** |

Same query against `91ed4a6..main` (Azure `main`): **0 commits**.

Same query against the three open Azure feature branches (`claude/busy-dubinsky`, `claude/dreamy-brahmagupta`, `claude/friendly-robinson`): **0 commits** for `busy-dubinsky` and `dreamy-brahmagupta`.

### A competing implementation on `claude/friendly-robinson`

Branch `claude/friendly-robinson` contains commit `3a90d9e Fix REPL agent-stdio YAML pipe being echoed instead of executed`. This is the **only** REPL-host-related commit on Azure across all branches.

`3a90d9e` is a **competing implementation** of the same functionality, but better architected:

```
src/McpServer.Repl.Core/AgentStdioProtocol.cs      | 181 +++++++++   (NEW)
src/McpServer.Repl.Core/ReplCommandDispatcher.cs   | 180 +++++++++   (NEW)
src/McpServer.Repl.Core/YamlEnvelopeTypes.cs       |  92 +++++       (NEW)
src/McpServer.Repl.Core/YamlSerializer.cs          | 340 +++++++++++ (NEW)
src/McpServer.Repl.Host/AgentStdioHandler.cs       |  76 +---        (REFACTORED)
.../ServiceCollectionExtensions.cs                 |  13 +
.../YamlPipeExecutionTests.cs                      | 403 +++++++++++ (NEW TESTS)
7 files changed, 1221 insertions(+), 64 deletions(-)
```

Notable architectural differences from `d0617b0`:

- **Extracts** the YAML protocol, serializer, and command dispatcher into `McpServer.Repl.Core` rather than inlining them in the handler
- **Reduces** `AgentStdioHandler.cs` by 64 lines instead of inflating it by 616
- **Adds** `YamlPipeExecutionTests.cs` (403 lines of test coverage) — Byrd-process-compliant
- Co-Authored-By: Claude Sonnet 4.6

`3a90d9e` is on a feature branch and **has not been merged to Azure `develop` or `main`**.

## Recommendation

**Option (b) — Discard `d0617b0`** AND **merge `claude/friendly-robinson` (which contains `3a90d9e`) to Azure `develop` first**, so the submodule sync (Phase 0.2) brings the architecturally-superior REPL implementation into the lib/McpServer pin.

### Rationale

1. `3a90d9e` is the strictly better implementation: it extracts protocol/serializer/dispatcher into a reusable Core layer, has full test coverage (`YamlPipeExecutionTests`, 403 lines), and is smaller in `AgentStdioHandler.cs`.
2. `d0617b0` violates Byrd Process: 694 insertions with **zero accompanying tests**. Cherry-picking it forward would entrench an untested feature implementation in `develop` that we'd then need to refactor anyway.
3. Discarding `d0617b0` is safe because its functionality is superseded by `3a90d9e`'s extracted-and-tested version. Nothing valuable is lost — only the inferior packaging.
4. Merging the `claude/friendly-robinson` PR is a single Azure DevOps PR action; once merged, `54d3ab8` advances and `Phase 0.2` picks up the new HEAD automatically.

### Risks

- **Risk 1:** `claude/friendly-robinson` PR may have unrelated changes I haven't surveyed. The user must review the full PR before merging, not just `3a90d9e`. Run `git -C F:/GitHub/McpServer log --oneline 91ed4a6..3a90d9e` to see the full branch history.
- **Risk 2:** If the user wants to preserve `d0617b0`'s exact code (e.g., for forensics or because there's hidden logic in the 694 lines that `3a90d9e` lacks), Option (b) loses it. Mitigation: this investigation note **is** the forensic record; the commit hash `d0617b0` is preserved in the GitHub-mirror remote and can be inspected at any time.

## Decision required from user

The user must confirm one of:

- **Confirmed (b)**: Merge `claude/friendly-robinson` PR on Azure → Phase 0.2 sync picks up the new develop HEAD → `d0617b0` is dropped from history. *Recommended.*
- **Override to (a)**: Cherry-pick `d0617b0` to Azure develop instead. We accept the inferior architecture and the missing tests. Not recommended.
- **Hybrid**: Do both — port the unique parts of `d0617b0` that aren't in `3a90d9e` as additional fixes on top after the merge. Highest effort.
