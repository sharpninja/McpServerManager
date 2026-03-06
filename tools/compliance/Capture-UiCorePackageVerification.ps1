param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [string]$OutputPath,
    [string]$PackageVersion,
    [switch]$FailOnFailures
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-VerificationStep {
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

function Invoke-Dotnet {
    param([string[]]$Arguments)

    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Get-NuspecMetadata {
    param(
        [Parameter(Mandatory)][string]$PackagePath,
        [Parameter(Mandatory)][string]$WorkingDir
    )

    $extractDir = Join-Path $WorkingDir ([IO.Path]::GetFileNameWithoutExtension($PackagePath))
    if (Test-Path -LiteralPath $extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force
    }

    [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $extractDir)
    $nuspecFile = Get-ChildItem -LiteralPath $extractDir -Filter "*.nuspec" -File | Select-Object -First 1
    if ($null -eq $nuspecFile) {
        throw "Nuspec not found in package: $PackagePath"
    }

    [xml]$xml = Get-Content -LiteralPath $nuspecFile.FullName -Raw
    $metadata = $xml.package.metadata
    $dependencies = @()
    if ($metadata.dependencies) {
        foreach ($group in $metadata.dependencies.group) {
            foreach ($dep in $group.dependency) {
                $dependencies += [string]$dep.id
            }
        }
        foreach ($dep in $metadata.dependencies.dependency) {
            $dependencies += [string]$dep.id
        }
    }

    $packageTypes = @()
    if ($metadata.packageTypes) {
        foreach ($typeNode in $metadata.packageTypes.packageType) {
            $packageTypes += [string]$typeNode.name
        }
    }

    $packageFiles = Get-ChildItem -LiteralPath $extractDir -Recurse -File |
        ForEach-Object { [IO.Path]::GetRelativePath($extractDir, $_.FullName).Replace("\", "/") }

    return [pscustomobject]@{
        Id = [string]$metadata.id
        Version = [string]$metadata.version
        Description = [string]$metadata.description
        RepositoryUrl = [string]$metadata.repository.url
        Dependencies = @($dependencies | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
        PackageTypes = @($packageTypes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
        PackageFiles = @($packageFiles | Sort-Object)
    }
}

Set-Location $RepoRoot

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = "0.1.0-migrate.$timestamp"
}

$packageDir = Join-Path $RepoRoot "artifacts\\migration-packages\\$timestamp"
$tempRoot = Join-Path $env:TEMP "ui-core-package-verify-$timestamp"
$toolPath = Join-Path $tempRoot "tool"
$consumerRoot = Join-Path $tempRoot "consumer"
$extractRoot = Join-Path $tempRoot "extract"

foreach ($dir in @($packageDir, $tempRoot, $toolPath, $consumerRoot, $extractRoot)) {
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

$packageMetadata = [ordered]@{}
$steps = [System.Collections.Generic.List[object]]::new()

$steps.Add((Invoke-VerificationStep `
            -Name "Pack UI.Core and Director package set" `
            -Command "dotnet pack {McpServer.Cqrs,McpServer.Cqrs.Mvvm,McpServer.Client,McpServer.UI.Core,McpServer.Director}" `
            -Action {
                Invoke-Dotnet -Arguments @("pack", "lib\\McpServer\\src\\McpServer.Cqrs\\McpServer.Cqrs.csproj", "-c", "Release", "-o", $packageDir, "/p:PackageVersion=$PackageVersion")
                Invoke-Dotnet -Arguments @("pack", "lib\\McpServer\\src\\McpServer.Cqrs.Mvvm\\McpServer.Cqrs.Mvvm.csproj", "-c", "Release", "-o", $packageDir, "/p:PackageVersion=$PackageVersion")
                Invoke-Dotnet -Arguments @("pack", "lib\\McpServer\\src\\McpServer.Client\\McpServer.Client.csproj", "-c", "Release", "-o", $packageDir, "/p:PackageVersion=$PackageVersion")
                Invoke-Dotnet -Arguments @("pack", "lib\\McpServer\\src\\McpServer.UI.Core\\McpServer.UI.Core.csproj", "-c", "Release", "-o", $packageDir, "/p:PackageVersion=$PackageVersion")
                Invoke-Dotnet -Arguments @("pack", "lib\\McpServer\\src\\McpServer.Director\\McpServer.Director.csproj", "-c", "Release", "-o", $packageDir, "/p:PackageVersion=$PackageVersion")
            }))

$steps.Add((Invoke-VerificationStep `
            -Name "Validate package metadata and dependency graph" `
            -Command "Inspect packed nuspec metadata for UI.Core and Director" `
            -Action {
                $uiCorePackage = Get-ChildItem -LiteralPath $packageDir -Filter "SharpNinja.McpServer.UI.Core.$PackageVersion.nupkg" -File | Select-Object -First 1
                $directorPackage = Get-ChildItem -LiteralPath $packageDir -Filter "SharpNinja.McpServer.Director.$PackageVersion.nupkg" -File | Select-Object -First 1
                if ($null -eq $uiCorePackage) { throw "UI.Core package not found in $packageDir" }
                if ($null -eq $directorPackage) { throw "Director package not found in $packageDir" }

                $uiMeta = Get-NuspecMetadata -PackagePath $uiCorePackage.FullName -WorkingDir $extractRoot
                $directorMeta = Get-NuspecMetadata -PackagePath $directorPackage.FullName -WorkingDir $extractRoot

                if ($uiMeta.Id -ne "SharpNinja.McpServer.UI.Core") { throw "Unexpected UI.Core package ID: $($uiMeta.Id)" }
                if ($directorMeta.Id -ne "SharpNinja.McpServer.Director") { throw "Unexpected Director package ID: $($directorMeta.Id)" }
                if ($uiMeta.RepositoryUrl -notmatch "github.com") { throw "UI.Core repository URL missing/invalid." }
                if ($directorMeta.RepositoryUrl -notmatch "github.com") { throw "Director repository URL missing/invalid." }

                $requiredUiDependencies = @("SharpNinja.McpServer.Client", "SharpNinja.McpServer.Cqrs.Mvvm")
                foreach ($dep in $requiredUiDependencies) {
                    if ($uiMeta.Dependencies -notcontains $dep) {
                        throw "UI.Core dependency missing: $dep"
                    }
                }

                if ($directorMeta.PackageTypes -notcontains "DotnetTool") {
                    throw "Director package must declare DotnetTool package type."
                }

                $requiredBundledAssemblies = @("McpServer.Client.dll", "McpServer.UI.Core.dll")
                foreach ($assembly in $requiredBundledAssemblies) {
                    if (-not ($directorMeta.PackageFiles | Where-Object { $_ -match [regex]::Escape($assembly) })) {
                        throw "Director tool package must bundle $assembly."
                    }
                }

                $packageMetadata["UiCore"] = $uiMeta
                $packageMetadata["Director"] = $directorMeta
            }))

$steps.Add((Invoke-VerificationStep `
            -Name "Install director tool from local package and run smoke commands" `
            -Command "dotnet tool install --tool-path + director --help/list/session-log" `
            -Action {
                Invoke-Dotnet -Arguments @(
                    "tool", "install",
                    "--tool-path", $toolPath,
                    "SharpNinja.McpServer.Director",
                    "--add-source", $packageDir,
                    "--version", $PackageVersion
                )

                $directorExe = Join-Path $toolPath "director.exe"
                if (-not (Test-Path -LiteralPath $directorExe)) {
                    throw "director.exe not found after tool install."
                }

                & $directorExe --help | Out-Host
                if ($LASTEXITCODE -ne 0) { throw "director --help failed with exit code $LASTEXITCODE" }

                & $directorExe list --help | Out-Host
                if ($LASTEXITCODE -ne 0) { throw "director list --help failed with exit code $LASTEXITCODE" }

                & $directorExe "session-log" "list" "--help" | Out-Host
                if ($LASTEXITCODE -ne 0) { throw "director session-log list --help failed with exit code $LASTEXITCODE" }
            }))

$steps.Add((Invoke-VerificationStep `
            -Name "Validate UI.Core package consumption from clean sample project" `
            -Command "dotnet new/add package/build for temporary consumer project" `
            -Action {
                if (Test-Path -LiteralPath $consumerRoot) {
                    Remove-Item -LiteralPath $consumerRoot -Recurse -Force
                }

                Invoke-Dotnet -Arguments @("new", "classlib", "--framework", "net9.0", "--output", $consumerRoot, "--name", "UiCoreConsumer")
                $consumerProj = Join-Path $consumerRoot "UiCoreConsumer.csproj"
                Invoke-Dotnet -Arguments @(
                    "add", $consumerProj, "package",
                    "SharpNinja.McpServer.UI.Core",
                    "--version", $PackageVersion,
                    "--source", $packageDir
                )

                $sampleCode = @"
using McpServer.UI.Core.ViewModels;

namespace UiCoreConsumer;

public static class SmokeTypeReference
{
    public static System.Type GetTypeRef() => typeof(MainWindowViewModel);
}
"@
                Set-Content -LiteralPath (Join-Path $consumerRoot "SmokeTypeReference.cs") -Value $sampleCode -Encoding UTF8

                Invoke-Dotnet -Arguments @("build", $consumerProj, "-c", "Release")
            }))

$artifact = [pscustomobject]@{
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $RepoRoot
    PackageVersion = $PackageVersion
    PackageDirectory = $packageDir
    TempRoot = $tempRoot
    FailedStepCount = @($steps | Where-Object { $_.Status -ne "passed" }).Count
    Steps = @($steps)
    Metadata = [pscustomobject]$packageMetadata
}

$json = $artifact | ConvertTo-Json -Depth 12

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
    Write-Host "UI.Core package verification artifact written: $fullPath"
}

$json

if ($FailOnFailures -and (@($steps | Where-Object { $_.Status -ne "passed" }).Count -gt 0)) {
    exit 1
}

exit 0
