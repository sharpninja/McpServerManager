[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory)]
    [string]$Target,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $repoRoot 'build.ps1'
if (-not (Test-Path -LiteralPath $buildScript)) {
    throw "build.ps1 not found at '$buildScript'."
}

$forwarded = [System.Collections.Generic.List[string]]::new()
$forwarded.Add('--target')
$forwarded.Add($Target)
foreach ($argument in @($Arguments)) {
    if ($null -ne $argument) {
        $forwarded.Add([string]$argument)
    }
}

& $buildScript @forwarded
exit $LASTEXITCODE
