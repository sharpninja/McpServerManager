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
$uiCoreMigrationScript = Join-Path $PSScriptRoot "Check-UiCoreMigration.ps1"
$uiCorePhaseGateScript = Join-Path $PSScriptRoot "Check-UiCoreMigrationPhaseGate.ps1"
$uiCorePackageAlignmentScript = Join-Path $PSScriptRoot "Check-UiCorePackageAlignment.ps1"
$uiCoreAdapterContractsScript = Join-Path $PSScriptRoot "Check-UiCoreAdapterContracts.ps1"
$uiCoreSharedOrchestrationScript = Join-Path $PSScriptRoot "Check-UiCoreSharedOrchestration.ps1"
$uiCoreCompatibilityShimsScript = Join-Path $PSScriptRoot "Check-UiCoreCompatibilityShims.ps1"
$uiCoreThreadAffinityScript = Join-Path $PSScriptRoot "Check-UiCoreThreadAffinity.ps1"
$uiCoreHostIntegrationScript = Join-Path $PSScriptRoot "Check-UiCoreHostIntegration.ps1"
$uiCoreResidualsScript = Join-Path $PSScriptRoot "Check-UiCoreMigrationResiduals.ps1"

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

Invoke-Guardrail -Name "UI.Core migration guard check" -Action {
    & $uiCoreMigrationScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core migration phase gate" -Action {
    & $uiCorePhaseGateScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core package alignment check" -Action {
    & $uiCorePackageAlignmentScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core adapter contract check" -Action {
    & $uiCoreAdapterContractsScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core shared orchestration check" -Action {
    & $uiCoreSharedOrchestrationScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core compatibility shims check" -Action {
    & $uiCoreCompatibilityShimsScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core thread-affinity check" -Action {
    & $uiCoreThreadAffinityScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core host integration check" -Action {
    & $uiCoreHostIntegrationScript -RepoRoot $RepoRoot
}

Invoke-Guardrail -Name "UI.Core migration residual cleanup check" -Action {
    & $uiCoreResidualsScript -RepoRoot $RepoRoot
}

Write-Host "All architecture compliance guardrails passed."
