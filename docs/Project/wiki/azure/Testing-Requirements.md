# Testing Requirements (MCP Server)

## TEST-AUTH-CACHE

### TEST-AUTH-CACHE-001

Cover valid cached token reuse, expired token deletion, new token persistence after login, and no-op behavior when no workspace is active.

**Acceptance Criteria:**
- [ ] Tests prove valid cached tokens are reused.
- [ ] Tests prove expired cached tokens are deleted.
- [ ] Tests prove newly obtained tokens are cached when a workspace is active.


## TEST-TRIAGE

### TEST-TRIAGE-001

Cover dashboard loading, open triage TODO filtering, cross-workspace navigation, detail selection, empty states, and error handling.

**Acceptance Criteria:**
- [ ] UI.Core tests cover dashboard mapping, created TODO hydration, completed or missing TODO exclusion, selected detail loading, empty states, and errors.
- [ ] Director and Web tests cover tab registration, triage TODO click-through navigation, and workspace-aware TODO detail loading.

### TEST-TRIAGE-002

Cover report multi-select, move to new group, consolidate into selected group, group multi-select combine, disabled states, and refresh/error behavior.



## TEST-WEB

### TEST-WEB-001

Validate that the Blazor Hybrid host is part of the solution and builds, with focused coverage for shared startup registration where feasible.

**Acceptance Criteria:**
- [x] Targeted dotnet build for the hybrid host succeeds. (evidence: dotnet build src/McpServerManager.Web.Hybrid/McpServerManager.Web.Hybrid.csproj --no-restore -v minimal -maxcpucount:1 /p:UseSharedCompilation=false)
- [x] Existing mcp-web targeted tests or compile validation still pass after shared startup extraction. (evidence: dotnet test tests/McpServerManager.Web.Tests/McpServerManager.Web.Tests.csproj --no-restore --filter FullyQualifiedName~RoutesHybridHostTests)


## TEST-WEB-HYBRID-TOOL

### TEST-WEB-HYBRID-TOOL-001

Validate that the Nuke build compiles and the web tool target uses the hybrid project while preserving mcp-web command metadata.

**Acceptance Criteria:**
- [x] Targeted build/test validation passes for the changed Nuke build project or static verification confirms the project path and command. (evidence: build/Build.BuildAndDeployTargets.cs:759-761; dotnet build build/Build.csproj --no-restore; build.ps1 UpdateWebUiTool --what-if --skip-version-bump; git diff --check.)


## TEST-WEB-NAV

### TEST-WEB-NAV-001

Cover persistent back navigation, Ctrl+W workspace picker behavior, workspace-change reload/invalidation, and TODO sidebar Show Done default/filter behavior with bUnit and focused view-model tests.
