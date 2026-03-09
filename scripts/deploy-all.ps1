#Requires -Version 7.0
[CmdletBinding(PositionalBinding = $false, SupportsShouldProcess)]
param(
    [ValidateSet('All', 'Director', 'WebUi', 'AndroidPhone', 'AndroidEmulator', 'DesktopMsix', 'DesktopDeb')]
    [string[]]$Targets = @('All'),

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$PackageVersion = '',
    [string]$AndroidPhoneSerial = '',
    [string]$AndroidEmulatorSerial = '',
    [string]$WslDistro = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
$nukeArgs.Add('--deploy-selection')
$nukeArgs.Add(($Targets -join ','))
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    $nukeArgs.Add('--package-version')
    $nukeArgs.Add($PackageVersion)
}
if (-not [string]::IsNullOrWhiteSpace($AndroidPhoneSerial)) {
    $nukeArgs.Add('--android-phone-serial')
    $nukeArgs.Add($AndroidPhoneSerial)
}
if (-not [string]::IsNullOrWhiteSpace($AndroidEmulatorSerial)) {
    $nukeArgs.Add('--android-emulator-serial')
    $nukeArgs.Add($AndroidEmulatorSerial)
}
if (-not [string]::IsNullOrWhiteSpace($WslDistro)) {
    $nukeArgs.Add('--wsl-distro')
    $nukeArgs.Add($WslDistro)
}
if ($WhatIfPreference) { $nukeArgs.Add('--what-if') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'DeployAll' @nukeArgs
exit $LASTEXITCODE
