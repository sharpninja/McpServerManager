#Requires -Version 5.1
[CmdletBinding(PositionalBinding = $false, SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $SkipInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
if ($SkipInstall) { $nukeArgs.Add('--skip-install') }
if ($WhatIfPreference) { $nukeArgs.Add('--what-if') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'BuildAndInstallVsix' @nukeArgs
exit $LASTEXITCODE
