param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [string]$OutputPath,
    [switch]$FailOnFailures
)

$ErrorActionPreference = "Stop"

function Invoke-BaselineStep {
    param(
        [string]$Name,
        [string]$Command,
        [string]$Arguments
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "passed"
    $error = $null
    $exitCode = 0

    try {
        $argList = @($Arguments -split " ")
        & $Command @argList | Out-Host
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
        Command = "$Command $Arguments"
        Status = $status
        ExitCode = $exitCode
        DurationSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
        Error = $error
    }
}

Set-Location $RepoRoot

$steps = [System.Collections.Generic.List[object]]::new()

$steps.Add((Invoke-BaselineStep `
            -Name "Build McpServerManager.Core" `
            -Command "dotnet" `
            -Arguments "build src\\McpServerManager.Core\\McpServerManager.Core.csproj -v minimal"))

$steps.Add((Invoke-BaselineStep `
            -Name "Build McpServer.Director" `
            -Command "dotnet" `
            -Arguments "build lib\\McpServer\\src\\McpServer.Director\\McpServer.Director.csproj -v minimal"))

$steps.Add((Invoke-BaselineStep `
            -Name "Build McpServer.Web" `
            -Command "dotnet" `
            -Arguments "build lib\\McpServer\\src\\McpServer.Web\\McpServer.Web.csproj -v minimal"))

$steps.Add((Invoke-BaselineStep `
            -Name "Test host wrapper inheritance guards" `
            -Command "dotnet" `
            -Arguments "test src\\McpServerManager.Core.Tests\\McpServerManager.Core.Tests.csproj --filter FullyQualifiedName~UiCoreHostWrapperInheritanceTests --logger console;verbosity=minimal -v minimal"))

$baseline = [pscustomobject]@{
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $RepoRoot
    Steps = @($steps)
    FailedStepCount = @($steps | Where-Object { $_.Status -ne "passed" }).Count
}

$json = $baseline | ConvertTo-Json -Depth 8

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
    Write-Host "UI.Core migration baseline written: $fullPath"
}

$json

if ($FailOnFailures -and (@($steps | Where-Object { $_.Status -ne "passed" }).Count -gt 0)) {
    exit 1
}

exit 0
