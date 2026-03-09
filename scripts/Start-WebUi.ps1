[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet("Debug", "Release", "Staging")]
    [string]$Configuration = "Debug",
    [int]$Port = 5200,
    [int]$TimeoutSeconds = 60,
    [switch]$NoBuild,
    [switch]$KillExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
$nukeArgs.Add('--configuration')
$nukeArgs.Add($Configuration)
$nukeArgs.Add('--port')
$nukeArgs.Add($Port.ToString([System.Globalization.CultureInfo]::InvariantCulture))
$nukeArgs.Add('--timeout-seconds')
$nukeArgs.Add($TimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture))
if ($NoBuild) { $nukeArgs.Add('--no-build') }
if ($KillExisting) { $nukeArgs.Add('--kill-existing') }

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'StartWebUi' @nukeArgs
exit $LASTEXITCODE
