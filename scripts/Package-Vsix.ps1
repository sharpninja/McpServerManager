#Requires -Version 5.1
[CmdletBinding(PositionalBinding = $false, SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
if ($WhatIfPreference) { $nukeArgs.Add('--what-if') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'PackageVsix' @nukeArgs
exit $LASTEXITCODE
