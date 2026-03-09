#Requires -Version 5.1
[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet("Prepare", "Collect")]
    [string]$Phase = "Collect",

    [string]$DeviceSerial = "ZD222QH58Q",

    [string]$PackageName = "ninja.thesharp.mcpservermanager",

    [string]$OutputRoot = '',

    [switch]$IncludeBugreport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--phase')
$nukeArgs.Add($Phase)
$nukeArgs.Add('--device-serial')
$nukeArgs.Add($DeviceSerial)
$nukeArgs.Add('--package-name')
$nukeArgs.Add($PackageName)
if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) {
    $nukeArgs.Add('--output-root')
    $nukeArgs.Add($OutputRoot)
}
if ($IncludeBugreport) { $nukeArgs.Add('--include-bugreport') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'CollectAndroidCrashArtifacts' @nukeArgs
exit $LASTEXITCODE
