# Artifact Refresh Operator Prompt

Use this prompt when an independent effort is landing changes and the planning/compliance artifacts must be synchronized to the current as-built state.

```text
You are refreshing planning/compliance artifacts in the RequestTracker workspace while independent gap-closure work is landing in parallel.

Goal
Reconcile artifacts to reflect the current as-built state, without guessing, and preserve the major-phase execution model.

Artifacts to refresh
1) docs/todo.yaml
   - TODO id: uicore-vm-relay-plan
   - TODO id: uicore-vm-relay-gaps
2) docs/architecture/compliance/UI-USECASE-MATRIX.md

Required planning structure (must remain explicit)
- M1: Backfill Missing Handlers with Unit Tests
- M2: Backfill missing ViewModels and Relay Commands with Unit and Integration Tests
- M3: Normalize ViewModel and RelayCommand usage across all UI surfaces and UI tests
- M4: As-Built Requirements Audit and Remediation
- M5: Migrate Director and Web UI to McpServerManager workspace

Execution instructions
1. Inspect independent changes first:
   - Review changed files, relevant commits, and test additions.
   - Identify which gaps/tasks are now actually closed vs still open.
2. Recompute current coverage from code:
   - Handler coverage by endpoint/message domain.
   - RelayCommand -> Handler -> ViewModel mutation chains.
   - UI presence/absence per use case across Phone/Tablet/Desktop/TUI.
3. Update uicore-vm-relay-plan:
   - Keep M1..M5 as top-level sequencing.
   - Mark tasks done ONLY with concrete evidence in code/tests.
   - Add missing tasks discovered from independent work.
   - Update `remaining` to the next highest-risk execution slice.
4. Update uicore-vm-relay-gaps:
   - Close gaps with evidence; add new gaps if newly discovered.
   - Keep sequencing aligned to M1..M5.
   - Record unresolved blockers explicitly.
5. Update UI-USECASE-MATRIX.md:
   - Ensure every active use case has current UI tags.
   - Ensure complete paths explicitly show RelayCommand -> Handler -> ViewModel mutation.
   - Keep omission rows for any missing RelayCommand chain.
   - Refresh divergence annotations per use case.
   - Refresh Mermaid endpoint-group diagrams to match latest state.
6. Validate consistency:
   - Counts in plan/gaps/matrix must agree (complete-path rows, omissions, coverage totals).
   - No “done” task without evidence path/symbol/test.
   - If a rename/replacement occurred, verify old pattern is fully removed across src/.
7. Run/record verification:
   - Run relevant unit/integration/UI tests for changed surfaces.
   - Capture pass/fail and unresolved risks.

Output format required
- Summary of artifact updates (what changed and why)
- Evidence list (file paths + symbols/tests) for each newly completed task/gap
- Updated coverage numbers (handlers, complete chains, omissions)
- Open risks/blockers
- Recommended next execution slice (M1..M5 task IDs)

Constraints
- Do not invent results.
- Do not silently drop existing tasks; supersede or close with rationale.
- Keep edits scoped to the listed artifacts unless a supporting fix is required.
```
