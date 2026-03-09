#Requires -Version 7.0
[CmdletBinding(PositionalBinding = $false, SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $NoBuild,
    [switch] $Clean,
    [switch] $Force,
    [switch] $Install,
    [switch] $NoCert,
    [string] $Version = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
if ($NoBuild) { $nukeArgs.Add('--no-build') }
if ($Clean) { $nukeArgs.Add('--clean') }
if ($Force) { $nukeArgs.Add('--force') }
if ($Install) { $nukeArgs.Add('--install') }
if ($NoCert) { $nukeArgs.Add('--no-cert') }
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $nukeArgs.Add('--package-version')
    $nukeArgs.Add($Version)
}
if ($WhatIfPreference) { $nukeArgs.Add('--what-if') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'BuildDesktopMsix' @nukeArgs
exit $LASTEXITCODE
