[CmdletBinding(PositionalBinding = $false, SupportsShouldProcess)]
param(
    [string]$InstallDir = '',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
if (-not [string]::IsNullOrWhiteSpace($InstallDir)) {
    $nukeArgs.Add('--install-dir')
    $nukeArgs.Add($InstallDir)
}
if ($Force) { $nukeArgs.Add('--force') }
if ($WhatIfPreference) { $nukeArgs.Add('--what-if') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'DeployMcpTodoExtension' @nukeArgs
exit $LASTEXITCODE
