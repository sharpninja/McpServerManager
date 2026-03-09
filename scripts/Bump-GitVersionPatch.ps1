function Get-NextVersionValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $gitVersionPath = Join-Path $RepoRoot 'GitVersion.yml'
    if (-not (Test-Path -LiteralPath $gitVersionPath)) {
        throw "GitVersion.yml not found at '$gitVersionPath'."
    }

    $content = Get-Content -Path $gitVersionPath -Raw
    $match = [regex]::Match($content, '(?m)^next-version:\s*(\d+)\.(\d+)\.(\d+)')
    if (-not $match.Success) {
        throw 'Could not parse next-version from GitVersion.yml.'
    }

    return '{0}.{1}.{2}' -f $match.Groups[1].Value, $match.Groups[2].Value, $match.Groups[3].Value
}

function Bump-GitVersionPatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    $buildScript = Join-Path $RepoRoot 'build.ps1'
    if (-not (Test-Path -LiteralPath $buildScript)) {
        throw "build.ps1 not found at '$buildScript'."
    }

    $oldVersion = Get-NextVersionValue -RepoRoot $RepoRoot
    & $buildScript --target BumpGitVersionPatch
    if ($LASTEXITCODE -ne 0) {
        throw "NUKE target BumpGitVersionPatch failed with exit code $LASTEXITCODE."
    }

    $newVersion = Get-NextVersionValue -RepoRoot $RepoRoot
    return [pscustomobject]@{
        OldVersion = $oldVersion
        NewVersion = $newVersion
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    Bump-GitVersionPatch -RepoRoot $repoRoot
}
