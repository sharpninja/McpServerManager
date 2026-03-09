[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$VsixPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nukeArgs = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($VsixPath)) {
    $nukeArgs.Add('--vsix-path')
    $nukeArgs.Add($VsixPath)
}

& (Join-Path $PSScriptRoot 'Invoke-Nuke.ps1') -Target 'ListVsix' @nukeArgs
exit $LASTEXITCODE
