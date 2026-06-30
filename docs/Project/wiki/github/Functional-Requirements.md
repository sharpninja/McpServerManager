# Functional Requirements (MCP Server)

## FR1. FR1.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR2. FR2.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR3. FR3.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR4. FR4.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR5. FR5.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR6. FR6.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR7. FR7.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR8. FR8.

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-AUTH-CACHE-001 Shared workspace authentication token cache

Applications launched in a workspace must reuse a valid cached identity token, delete expired cached tokens, and cache newly obtained identity tokens in workspace-scoped shared auth storage.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] When launched in a workspace with a valid cached token, the app authenticates with that token without forcing a new identity-server login.
- [ ] When the cached token is expired, the app deletes it and falls back to the normal login flow.
- [ ] After identity-server login succeeds in a workspace, the token is saved in workspace-scoped shared auth storage for reuse by other apps.

## FR-GEN-001 Generate Document Test

FR for document generation test
Scope: layer-1+

## FR-RTUI-001 FR-RTUI-001

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-002 FR-RTUI-002

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-003 FR-RTUI-003

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-004 FR-RTUI-004

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-005 FR-RTUI-005

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-006 FR-RTUI-006

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-101 FR-RTUI-101

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-102 FR-RTUI-102

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-103 FR-RTUI-103

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-104 FR-RTUI-104

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-105 FR-RTUI-105

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-106 FR-RTUI-106

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-107 FR-RTUI-107

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-108 FR-RTUI-108

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-109 FR-RTUI-109

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-110 FR-RTUI-110

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-111 FR-RTUI-111

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-112 FR-RTUI-112

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-113 FR-RTUI-113

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-114 FR-RTUI-114

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-115 FR-RTUI-115

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-116 FR-RTUI-116

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-RTUI-117 FR-RTUI-117

Placeholder requirement backfilled by DB-FK-001.
Scope: layer-1+

## FR-TEST-001 FR-TEST-001

Placeholder requirement backfilled for TODO link FR-TEST-001.
Scope: layer-1+

## FR-TEST-002 FR-TEST-002

Placeholder requirement backfilled for TODO link FR-TEST-002.
Scope: layer-1+

## FR-TRIAGE-001 Triage dashboard tab

Show the triage queue, report group queue, run history with results, and open TODO items created by triage in Director and MCP Web.
Scope: layer-1+
**Acceptance Criteria:**
- [ ] Director and MCP Web expose a Triage tab/page with triage queue, report group queue, run history, and open triage-created TODO sections.
- [ ] Open triage-created TODO rows navigate to the TODO view in the TODO's workspace and select the target TODO.

## FR-TRIAGE-002 Web triage multi-row grouping

MCP Web triage dashboard must let users select multiple triage rows or groups and move/consolidate them into report groups without leaving the triage page.
Scope: layer-1+

## FR-WEB-001 MCP Web desktop hybrid host

McpServer must provide a Blazor Hybrid desktop host for the existing mcp-web experience so users can run the web UI as a local desktop application without duplicating page behavior.
Scope: layer-1+
**Acceptance Criteria:**
- [x] A new Blazor Hybrid host project starts and renders the existing mcp-web UI shell/components. (evidence: src/McpServerManager.Web.Hybrid/McpServerManager.Web.Hybrid.csproj)
- [x] The host shares services, routing, and configuration behavior with mcp-web wherever practical instead of forking feature pages. (evidence: src/McpServerManager.Web.Hybrid/Components/HybridRoot.razor; src/McpServerManager.Web/Routes.razor)
- [x] The project is included in the solution and can be built by targeted validation. (evidence: McpServerManager.sln; dotnet build src/McpServerManager.Web.Hybrid/McpServerManager.Web.Hybrid.csproj --no-restore)

## FR-WEB-HYBRID-TOOL-001 mcp-web global tool launches hybrid host

The Nuke publish/deploy path for the mcp-web global dotnet tool must package the Blazor Hybrid host while preserving the installed command name mcp-web.
Scope: layer-1+
**Acceptance Criteria:**
- [x] The web UI global tool package is produced from McpServerManager.Web.Hybrid rather than McpServerManager.Web. (evidence: build/Build.BuildAndDeployTargets.cs:759-761; dotnet build build/Build.csproj --no-restore; build.ps1 UpdateWebUiTool --what-if --skip-version-bump; git diff --check.)
- [x] The installed command remains mcp-web. (evidence: build/Build.BuildAndDeployTargets.cs:759-761; dotnet build build/Build.csproj --no-restore; build.ps1 UpdateWebUiTool --what-if --skip-version-bump; git diff --check.)

## FR-WEB-NAV-001 Persistent web navigation and workspace controls

MCP Web and the Hybrid host must provide a persistent back button, a Ctrl+W workspace picker matching Director behavior, workspace-change reload semantics for cached page state, and a TODO sidebar that defaults to open TODOs with Show Done off.
Scope: layer-1+

