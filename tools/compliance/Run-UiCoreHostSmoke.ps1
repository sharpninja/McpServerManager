param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [string]$OutputPath,
    [switch]$FailOnFailures
)

$ErrorActionPreference = "Stop"

function Invoke-SmokeStep {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$Arguments
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "passed"
    $error = $null
    $exitCode = 0

    try {
        & $Command @Arguments | Out-Host
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $status = "failed"
            $error = "Exit code: $exitCode"
        }
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
        Command = "$Command $($Arguments -join ' ')"
        Status = $status
        ExitCode = $exitCode
        DurationSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
        Error = $error
    }
}

Set-Location $RepoRoot

$steps = [System.Collections.Generic.List[object]]::new()

$steps.Add((Invoke-SmokeStep `
            -Name "Web controller integration smoke (todo/session/workspace)" `
            -Command "dotnet" `
            -Arguments @(
                "test",
                "lib\\McpServer\\tests\\McpServer.Support.Mcp.IntegrationTests\\McpServer.Support.Mcp.IntegrationTests.csproj",
                "--filter", "FullyQualifiedName~TodoControllerTests|FullyQualifiedName~SessionLogControllerTests|FullyQualifiedName~WorkspaceControllerTests",
                "--nologo",
                "--verbosity", "minimal"
            )))

$steps.Add((Invoke-SmokeStep `
            -Name "Workspace auth/resolution middleware smoke" `
            -Command "dotnet" `
            -Arguments @(
                "test",
                "lib\\McpServer\\tests\\McpServer.Support.Mcp.Tests\\McpServer.Support.Mcp.Tests.csproj",
                "--filter", "FullyQualifiedName~WorkspaceResolutionMiddlewareTests|FullyQualifiedName~WorkspaceAuthMiddlewareTests",
                "--nologo",
                "--verbosity", "minimal"
            )))

$steps.Add((Invoke-SmokeStep `
            -Name "Director CLI smoke (health/list/agents/todo/session-log/sync)" `
            -Command "dotnet" `
            -Arguments @(
                "test",
                "lib\\McpServer\\tests\\McpServer.Director.Tests\\McpServer.Director.Tests.csproj",
                "--filter", "FullyQualifiedName~HealthCommandTests|FullyQualifiedName~ListCommandTests|FullyQualifiedName~AgentsCommandTests|FullyQualifiedName~TodoCommandTests|FullyQualifiedName~SessionLogCommandTests|FullyQualifiedName~SyncCommandTests",
                "--nologo",
                "--verbosity", "minimal"
            )))

$steps.Add((Invoke-SmokeStep `
            -Name "Director auth/workspace lifecycle smoke" `
            -Command "dotnet" `
            -Arguments @(
                "test",
                "lib\\McpServer\\tests\\McpServer.Director.Tests\\McpServer.Director.Tests.csproj",
                "--filter", "FullyQualifiedName~AuthCommandTests|FullyQualifiedName~ValidateAndInitCommandTests|FullyQualifiedName~InteractiveAndExecCommandTests",
                "--nologo",
                "--verbosity", "minimal"
            )))

$steps.Add((Invoke-SmokeStep `
            -Name "Director exec area coverage smoke" `
            -Command "dotnet" `
            -Arguments @(
                "test",
                "lib\\McpServer\\tests\\McpServer.Director.Tests\\McpServer.Director.Tests.csproj",
                "--filter", "FullyQualifiedName~ExecAreaCoverageTests",
                "--nologo",
                "--verbosity", "minimal"
            )))

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
    Write-Host "UI.Core host smoke artifact written: $fullPath"
}

$json

if ($FailOnFailures -and (@($steps | Where-Object { $_.Status -ne "passed" }).Count -gt 0)) {
    exit 1
}

exit 0
