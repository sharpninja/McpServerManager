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

## Update: User chose Hybrid (2026-04-11)

The user picked **Hybrid**. Detailed second-pass diff analysis below identifies what's actually unique to `d0617b0` versus what's in `3a90d9e` already, then proposes the concrete port plan.

### Trust-bootstrap interfaces — already present in both branches and in the common ancestor

The following Repl.Core interfaces exist in **all four** of `91ed4a6` (common ancestor), `54d3ab8` (Azure `develop`), `3a90d9e` (`claude/friendly-robinson`), and `d0617b0` (GitHub-mirror) — same blob hashes, unchanged:

- `src/McpServer.Repl.Core/IAuthRotationHandler.cs`
- `src/McpServer.Repl.Core/IMarkerFileReader.cs`
- `src/McpServer.Repl.Core/IReplProtocol.cs`
- `src/McpServer.Repl.Core/ITrustBootstrapService.cs`

These were authored before the branches split. Both `d0617b0` and `3a90d9e` inherit them as-is.

### Neither branch implements `ITrustBootstrapService`

A `git grep` for `: ITrustBootstrapService` and `class.*ITrustBootstrapService` returns **zero matches** in both `d0617b0` and `3a90d9e`. The interface is sitting unused on both sides.

### What `d0617b0` actually does for trust/nonces

Inside `AgentStdioHandler.cs` (the inline 646-line file), `d0617b0` has:

```csharp
private readonly Dictionary<string, string> _nonceByWorkspace = new(StringComparer.OrdinalIgnoreCase);
```

— an **out-of-band** nonce tracking dictionary that bypasses `ITrustBootstrapService` entirely. The trust bootstrap requirement (TR-MCP-REPL-006) is satisfied by inline ad-hoc code, not by an implementation of the existing interface.

This means d0617b0 is internally inconsistent: it references TR-MCP-REPL-006 in its file header but implements it in a way that doesn't compose with the rest of the Repl.Core trust scaffolding. A future refactor would need to extract this into the existing `ITrustBootstrapService` anyway.

### What `3a90d9e` does for trust/nonces

A `git grep` for `nonce` in `3a90d9e -- src/McpServer.Repl.Core/ src/McpServer.Repl.Host/` matches only the existing comments in `IMarkerFileReader.cs`, `IReplProtocol.cs`, `ITrustBootstrapService.cs`. **No production nonce code exists** — the trust bootstrap interface is still unimplemented in this branch too.

### What `d0617b0` does for the test child-process helper

`tests/McpServer.Repl.IntegrationTests/ReplChildProcessHelper.cs`:

- At `91ed4a6`: 303 lines (assumed — unchanged at the common ancestor)
- At `3a90d9e`: 303 lines (file was not touched)
- At `d0617b0`: **393 lines** (+90 lines added)

So `d0617b0` adds ~90 lines of test infrastructure to the integration test child-process helper. `3a90d9e` does not touch this file. The 90 added lines are d0617b0-unique.

### Concrete d0617b0-unique behaviors

| Behavior | In `d0617b0`? | In `3a90d9e`? | Port required? |
|---|---|---|---|
| Extracted `AgentStdioProtocol` in `Repl.Core` | No | **YES** | No (we adopt 3a90d9e's design) |
| Extracted `ReplCommandDispatcher` in `Repl.Core` | No | **YES** | No |
| Extracted `YamlSerializer` in `Repl.Core` (340 lines) | No | **YES** | No |
| Extracted `YamlEnvelopeTypes` in `Repl.Core` | No | **YES** | No |
| `YamlPipeExecutionTests.cs` (403 lines of test coverage) | No | **YES** | No |
| Per-workspace nonce tracking dictionary | YES (inline) | No | **YES — but as a clean `ITrustBootstrapService` impl, not inline** |
| `ITrustBootstrapService` implementation routed through Repl.Core | No (inline ad-hoc) | No | **YES — net-new work, neither branch has it** |
| ChildProcessHelper.cs +90-line test helper additions | YES | No | **YES — port verbatim** |

### Revised hybrid port plan

The hybrid is now three Azure-side commits in this exact order:

1. **Merge `claude/friendly-robinson` PR to Azure `develop`**
   - Brings in `AgentStdioProtocol`, `ReplCommandDispatcher`, `YamlSerializer`, `YamlEnvelopeTypes`, the refactored `AgentStdioHandler.cs`, `YamlPipeExecutionTests.cs`
   - Pure merge — no conflict expected
   - This is the user's responsibility to do via Azure DevOps PR review, OR I can do the merge locally in `F:/GitHub/McpServer` and push

2. **NEW: implement `ITrustBootstrapService` properly** with workspace-scoped nonce tracking
   - Class name: `TrustBootstrapService` in `src/McpServer.Repl.Core/`
   - Owns the per-workspace `Dictionary<string, string>` nonce store from d0617b0, but exposed as `RegisterNonceAsync(workspaceId, nonce)` / `ValidateNonceAsync(workspaceId, expectedNonce)` per the interface
   - Wired into DI in `ServiceCollectionExtensions`
   - Tested by new unit tests in `tests/McpServer.Repl.Core.Tests/TrustBootstrapServiceTests.cs`
   - This is **net-new work** that satisfies TR-MCP-REPL-006 and FR-MCP-REPL-004 cleanly

3. **Port the +90-line `ReplChildProcessHelper.cs` test helper additions**
   - Verbatim from `d0617b0`'s tree, applied on top of the post-merge state
   - May need minor adjustments if the merge changed unrelated parts of the file
   - Adds the integration test scaffolding d0617b0 needed

After all three commits are pushed to Azure `develop`, Phase 0.2 sync picks up the new HEAD and the worktree submodule advances accordingly. `d0617b0`'s commit hash is then permanently dropped, but every meaningful behavior it added is preserved either by 3a90d9e (the protocol extraction) or by commits 2 and 3 above (the trust bootstrap impl and test helper).

### Net new work this introduces beyond a vanilla cherry-pick

- 1 new Azure commit implementing `TrustBootstrapService` (~150 lines + ~100 lines of tests)
- 1 new Azure commit porting test helper additions (~90 lines, no new tests since the helper is itself test infrastructure)
- Total: ~340 net new lines of Azure-side work, all properly tested and properly architected

### Estimated effort

- Step 1 (merge): minutes (if no conflicts)
- Step 2 (implement TrustBootstrapService): ~2–3 hours (design, code, tests, doc comments referencing FR/TR IDs)
- Step 3 (port test helper): ~30 minutes (mostly mechanical)
- Total: ~3–4 hours of Azure-side work before Phase 0.2 can proceed

### Confirmation required from user

This hybrid port plan involves:
- Writing on Azure DevOps `develop` (3 new commits, all pushed to origin = Azure)
- Net-new design work (the `TrustBootstrapService` implementation isn't in either branch)
- Tests authored under the Byrd Process

I should NOT push any of this to Azure without the user's explicit confirmation. The next step is to ask the user to confirm:
1. Should I proceed with the merge of `claude/friendly-robinson` myself (running locally in `F:/GitHub/McpServer` and pushing), or do they want to handle the Azure PR review themselves?
2. Should the new `TrustBootstrapService` implementation be authored in this same worktree for the McpServerManager submodule update, or as a separate session in the standalone `F:/GitHub/McpServer` clone?
3. Are they OK with the ~3–4 hour effort estimate for the hybrid before Phase 0.2 can proceed, or would they prefer to fall back to plain Option (b) and accept the loss of the d0617b0 test-helper additions?
