[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$VsixPath = '',
    [Parameter(Mandatory)]
    [string]$VsixEntry
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($VsixPath)) {
    $nukeArgs.Add('--vsix-path')
    $nukeArgs.Add($VsixPath)
}
$nukeArgs.Add('--vsix-entry')
$nukeArgs.Add($VsixEntry)

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'ReadVsix' @nukeArgs
exit $LASTEXITCODE
