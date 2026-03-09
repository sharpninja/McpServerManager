#Requires -Version 5.1
[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$DeviceSerial = "ZD222QH58Q"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
$nukeArgs.Add('--device-serial')
$nukeArgs.Add($DeviceSerial)

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'DeployAndroid' @nukeArgs
exit $LASTEXITCODE
