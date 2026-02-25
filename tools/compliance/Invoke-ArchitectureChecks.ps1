param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [switch]$IncludeLegacy
)

$ErrorActionPreference = "Stop"

function Invoke-Guardrail {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "=== $Name ==="
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

$cqrsScript = Join-Path $PSScriptRoot "Check-CqrsBoundaries.ps1"
$vmScript = Join-Path $PSScriptRoot "Check-ViewModelBoundaries.ps1"

Invoke-Guardrail -Name "CQRS boundary check" -Action {
    & $cqrsScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "ViewModel boundary check" -Action {
    if ($IncludeLegacy) {
        & $vmScript -RepoRoot $RepoRoot -IncludeLegacy
    }
    else {
        & $vmScript -RepoRoot $RepoRoot
    }
}

Write-Host "All architecture compliance guardrails passed."
