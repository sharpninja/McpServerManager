param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [string]$OutputPath,
    [switch]$FailOnFailures
)

$ErrorActionPreference = "Stop"

function Invoke-TranscriptStep {
    param(
        [string]$JourneyId,
        [string]$Description,
        [string]$Command,
        [string]$Arguments
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "passed"
    $exitCode = 0
    $error = $null
    $transcript = ""

    try {
        if ([string]::IsNullOrWhiteSpace($Arguments)) {
            $transcript = (& $Command 2>&1 | Out-String)
        }
        else {
            $argList = @($Arguments -split " ")
            $transcript = (& $Command @argList 2>&1 | Out-String)
        }

        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $status = "failed"
            $error = "Exit code: $exitCode"
        }
    }
    catch {
        $status = "failed"
        $error = $_.Exception.Message
        $transcript = ($transcript + [Environment]::NewLine + ($_ | Out-String)).Trim()
    }
    finally {
        $sw.Stop()
    }

    return [pscustomobject]@{
        JourneyId = $JourneyId
        Description = $Description
        Command = if ([string]::IsNullOrWhiteSpace($Arguments)) { $Command } else { "$Command $Arguments" }
        Status = $status
        ExitCode = $exitCode
        DurationSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
        Error = $error
        Transcript = $transcript
    }
}

Set-Location $RepoRoot

$steps = [System.Collections.Generic.List[object]]::new()
$invokeArchitectureChecksPath = Join-Path $RepoRoot "tools/compliance/Invoke-ArchitectureChecks.ps1"

$steps.Add((Invoke-TranscriptStep `
            -JourneyId "COMPLIANCE-GUARDRAILS" `
            -Description "Architecture and migration guardrail execution transcript." `
            -Command $invokeArchitectureChecksPath `
            -Arguments ""))

$steps.Add((Invoke-TranscriptStep `
            -JourneyId "TODO-AND-CQRS-DISPATCH" `
            -Description "CQRS command dispatch integration transcript (CommandRoundTripTests)." `
            -Command "dotnet" `
            -Arguments "test src\\McpServerManager.Core.Tests\\McpServerManager.Core.Tests.csproj --filter FullyQualifiedName~CommandRoundTripTests -v minimal"))

$steps.Add((Invoke-TranscriptStep `
            -JourneyId "HOST-WRAPPER-INHERITANCE" `
            -Description "Host wrapper inheritance guard transcript (UiCoreHostWrapperInheritanceTests)." `
            -Command "dotnet" `
            -Arguments "test src\\McpServerManager.Core.Tests\\McpServerManager.Core.Tests.csproj --filter FullyQualifiedName~UiCoreHostWrapperInheritanceTests -v minimal"))

$steps.Add((Invoke-TranscriptStep `
            -JourneyId "DIRECTOR-HOST-BUILD" `
            -Description "Director host build transcript for migration parity baseline." `
            -Command "dotnet" `
            -Arguments "build lib\\McpServer\\src\\McpServer.Director\\McpServer.Director.csproj -v minimal"))

$steps.Add((Invoke-TranscriptStep `
            -JourneyId "WEB-HOST-BUILD" `
            -Description "Web host build transcript for migration parity baseline." `
            -Command "dotnet" `
            -Arguments "build lib\\McpServer\\src\\McpServer.Web\\McpServer.Web.csproj -v minimal"))

$artifact = [pscustomobject]@{
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $RepoRoot
    StepCount = $steps.Count
    FailedStepCount = @($steps | Where-Object { $_.Status -ne "passed" }).Count
    Steps = @($steps)
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
    Write-Host "UI.Core behavior transcripts written: $fullPath"
}

$json

if ($FailOnFailures -and (@($steps | Where-Object { $_.Status -ne "passed" }).Count -gt 0)) {
    exit 1
}

exit 0
