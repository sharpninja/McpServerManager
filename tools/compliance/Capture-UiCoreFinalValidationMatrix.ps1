param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [string]$OutputPath,
    [switch]$FailOnFailures
)

$ErrorActionPreference = "Stop"

function Invoke-MatrixStep {
    param(
        [string]$Name,
        [string]$Command,
        [scriptblock]$Action
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "passed"
    $error = $null

    try {
        & $Action
    }
    catch {
        $status = "failed"
        $error = $_.Exception.Message
    }
    finally {
        $sw.Stop()
    }

    return [pscustomobject]@{
        Name = $Name
        Command = $Command
        Status = $status
        DurationSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
        Error = $error
    }
}

Set-Location $RepoRoot

$sessionFiles = "C:\\Users\\kingd\\.copilot\\session-state\\bff566af-f937-4df9-b011-f387bbcf2aca\\files"
$baselinePath = Join-Path $sessionFiles "ui-core-migration-baseline.json"
$behaviorPath = Join-Path $sessionFiles "ui-core-migration-behavior-transcripts.json"
$hostSmokePath = Join-Path $sessionFiles "ui-core-host-smoke.json"
$packagePath = Join-Path $sessionFiles "ui-core-package-verification.json"

$steps = [System.Collections.Generic.List[object]]::new()

$steps.Add((Invoke-MatrixStep `
            -Name "Architecture compliance guardrails" `
            -Command "tools/compliance/Invoke-ArchitectureChecks.ps1" `
            -Action {
                & ".\\tools\\compliance\\Invoke-ArchitectureChecks.ps1"
                if ($LASTEXITCODE -ne 0) { throw "Invoke-ArchitectureChecks failed with exit code $LASTEXITCODE" }
            }))

$steps.Add((Invoke-MatrixStep `
            -Name "Migration baseline matrix capture" `
            -Command "tools/compliance/Capture-UiCoreMigrationBaseline.ps1 -FailOnFailures" `
            -Action {
                & ".\\tools\\compliance\\Capture-UiCoreMigrationBaseline.ps1" -OutputPath $baselinePath -FailOnFailures
                if ($LASTEXITCODE -ne 0) { throw "Capture-UiCoreMigrationBaseline failed with exit code $LASTEXITCODE" }
            }))

$steps.Add((Invoke-MatrixStep `
            -Name "Behavior transcript capture" `
            -Command "tools/compliance/Capture-UiCoreBehaviorTranscripts.ps1 -FailOnFailures" `
            -Action {
                & ".\\tools\\compliance\\Capture-UiCoreBehaviorTranscripts.ps1" -OutputPath $behaviorPath -FailOnFailures
                if ($LASTEXITCODE -ne 0) { throw "Capture-UiCoreBehaviorTranscripts failed with exit code $LASTEXITCODE" }
            }))

$steps.Add((Invoke-MatrixStep `
            -Name "Web/Director smoke validation" `
            -Command "tools/compliance/Run-UiCoreHostSmoke.ps1 -FailOnFailures" `
            -Action {
                & ".\\tools\\compliance\\Run-UiCoreHostSmoke.ps1" -OutputPath $hostSmokePath -FailOnFailures
                if ($LASTEXITCODE -ne 0) { throw "Run-UiCoreHostSmoke failed with exit code $LASTEXITCODE" }
            }))

$steps.Add((Invoke-MatrixStep `
            -Name "Package and distribution verification" `
            -Command "tools/compliance/Capture-UiCorePackageVerification.ps1 -FailOnFailures" `
            -Action {
                & ".\\tools\\compliance\\Capture-UiCorePackageVerification.ps1" -OutputPath $packagePath -FailOnFailures
                if ($LASTEXITCODE -ne 0) { throw "Capture-UiCorePackageVerification failed with exit code $LASTEXITCODE" }
            }))

$artifact = [pscustomobject]@{
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $RepoRoot
    FailedStepCount = @($steps | Where-Object { $_.Status -ne "passed" }).Count
    Steps = @($steps)
    EvidenceArtifacts = [pscustomobject]@{
        Baseline = $baselinePath
        BehaviorTranscripts = $behaviorPath
        HostSmoke = $hostSmokePath
        PackageVerification = $packagePath
    }
}

$json = $artifact | ConvertTo-Json -Depth 10

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $fullPath = $OutputPath
    if (-not [IO.Path]::IsPathRooted($fullPath)) {
        $fullPath = Join-Path $RepoRoot $OutputPath
    }

    $dir = Split-Path -Parent $fullPath
    if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Set-Content -LiteralPath $fullPath -Value $json -Encoding UTF8
    Write-Host "UI.Core final validation matrix written: $fullPath"
}

$json

if ($FailOnFailures -and (@($steps | Where-Object { $_.Status -ne "passed" }).Count -gt 0)) {
    exit 1
}

exit 0
