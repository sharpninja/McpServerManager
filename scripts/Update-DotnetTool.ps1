[CmdletBinding(PositionalBinding = $false, SupportsShouldProcess)]
param(
    [switch]$SkipVersionBump,
    [string]$ProjectPath = 'src\McpServer.Director\McpServer.Director.csproj',
    [string]$ToolId = 'SharpNinja.McpServer.Director',
    [string]$ToolCommand = 'director',
    [string]$NupkgDir = 'nupkg',
    [ValidateSet('Debug', 'Release', 'Staging')][string]$Configuration = 'Release',
    [string]$PackageVersion = '',
    [switch]$SkipProcessStop
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
$nukeArgs.Add('--project-path')
$nukeArgs.Add($ProjectPath)
$nukeArgs.Add('--tool-id')
$nukeArgs.Add($ToolId)
$nukeArgs.Add('--tool-command')
$nukeArgs.Add($ToolCommand)
$nukeArgs.Add('--nupkg-dir')
$nukeArgs.Add($NupkgDir)
if ($SkipVersionBump) { $nukeArgs.Add('--skip-version-bump') }
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    $nukeArgs.Add('--package-version')
    $nukeArgs.Add($PackageVersion)
}
if ($SkipProcessStop) { $nukeArgs.Add('--skip-process-stop') }
if ($WhatIfPreference) { $nukeArgs.Add('--what-if') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'UpdateDotnetTool' @nukeArgs
exit $LASTEXITCODE
