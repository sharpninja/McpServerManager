#requires -Version 7
<#
.SYNOPSIS
    Append a turn to the LocalDB migration session log via McpSession.psm1.

.DESCRIPTION
    Thin wrapper around McpSession.psm1's Initialize-McpSession +
    New-McpSessionLog + Add-McpSessionEntry, hard-coded with the migration
    plan's fixed session id and model so each phase only needs to specify
    title/query/response/tags.

.PARAMETER Title
    queryTitle for the turn (e.g. "Phase 0.1 d0617b0 investigation").

.PARAMETER QueryText
    The user-prompt or work request that initiated the turn.

.PARAMETER Response
    The result text describing what was accomplished.

.PARAMETER Status
    in_progress, completed, or failed. Defaults to completed.

.PARAMETER Tags
    Comma-separated tags (e.g. "phase-0.1,workstream-investigation").

.PARAMETER ContextList
    Comma-separated list of file paths or context references.

.PARAMETER RequestId
    Optional explicit request id. If omitted, generated as
    req-<yyyyMMddTHHmmssZ>-<title-slug>.

.NOTES
    Reads AGENTS-README-FIRST.yaml from the parent McpServerManager workspace
    (3 levels up from this script in the worktree).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Title,
    [Parameter(Mandatory)] [string] $QueryText,
    [string] $Response = '',
    [ValidateSet('in_progress','completed','failed')] [string] $Status = 'completed',
    [string] $Tags = '',
    [string] $ContextList = '',
    [string] $RequestId = ''
)

$ErrorActionPreference = 'Stop'

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$worktreeRoot = Resolve-Path (Join-Path $scriptDir '..')
$repoRoot    = Resolve-Path (Join-Path $worktreeRoot '../../..')
$markerPath  = Join-Path $repoRoot 'AGENTS-README-FIRST.yaml'
$modulePath  = Join-Path $repoRoot 'McpSession.psm1'

if (-not (Test-Path $markerPath)) { throw "Marker not found at $markerPath" }
if (-not (Test-Path $modulePath)) { throw "McpSession.psm1 not found at $modulePath" }

Import-Module $modulePath -Force | Out-Null
Initialize-McpSession -MarkerPath $markerPath | Out-Null

$sessionId = 'ClaudeCode-20260411T163146Z-localdb-migration-plan'

# New-McpSessionLog with an existing sessionId is upsert-style — safe to call repeatedly
$session = New-McpSessionLog `
    -SourceType 'ClaudeCode' `
    -SessionId  $sessionId `
    -Title      'MCP Server local service: SQLite -> SQL Server LocalDB migration (plan execution)' `
    -Model      'claude-opus-4-6'

if (-not $RequestId) {
    $slug = ($Title -replace '[^a-zA-Z0-9]+','-').Trim('-').ToLower()
    if ($slug.Length -gt 40) { $slug = $slug.Substring(0, 40) }
    $RequestId = 'req-{0}-{1}' -f (Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ'), $slug
}

$tagArray     = if ($Tags)        { $Tags        -split ',' | ForEach-Object Trim } else { @() }
$contextArray = if ($ContextList) { $ContextList -split ',' | ForEach-Object Trim } else { @() }

$entry = Add-McpSessionEntry `
    -Session         $session `
    -RequestId       $RequestId `
    -QueryTitle      $Title `
    -QueryText       $QueryText `
    -Interpretation  'Phase execution for the LocalDB migration plan; see ~/OneDrive/Documents/mcp-server-db-sync.md' `
    -Response        $Response `
    -Status          $Status `
    -Model           'claude-opus-4-6' `
    -Tags            $tagArray `
    -ContextList     $contextArray

Write-Host "Turn appended: $($entry.requestId)" -ForegroundColor Green
Write-Host "Status       : $Status"
