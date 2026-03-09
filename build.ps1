[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function ConvertTo-NukeArguments {
    param(
        [string[]]$InputArguments = @()
    )

    $effectiveArguments = @($InputArguments | Where-Object { $null -ne $_ })
    if ($effectiveArguments.Count -eq 0) {
        $helpArguments = [System.Collections.Generic.List[string]]::new()
        $helpArguments.Add('--help')
        return $helpArguments
    }

    $forwardedArguments = [System.Collections.Generic.List[string]]::new()
    $hasExplicitTarget = $false

    foreach ($argument in $effectiveArguments) {
        if ($argument -match '^(--|-|/)(target|t)$') {
            $hasExplicitTarget = $true
        }
    }

    $firstArgument = $effectiveArguments[0]
    if (-not $hasExplicitTarget -and
        -not [string]::IsNullOrWhiteSpace($firstArgument) -and
        -not $firstArgument.StartsWith('-') -and
        -not $firstArgument.StartsWith('/')) {
        $forwardedArguments.Add('--target')
    }

    foreach ($argument in $effectiveArguments) {
        $forwardedArguments.Add([string]$argument)
    }

    return $forwardedArguments
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot 'build\Build.csproj'
$forwardedArguments = ConvertTo-NukeArguments -InputArguments $Arguments

$commandArguments = [System.Collections.Generic.List[string]]::new()
$commandArguments.Add('run')
$commandArguments.Add('--project')
$commandArguments.Add($projectPath)
$commandArguments.Add('--')
$commandArguments.Add('--root')
$commandArguments.Add($repoRoot)

foreach ($argument in @($forwardedArguments)) {
    if ($null -ne $argument) {
        $commandArguments.Add([string]$argument)
    }
}

& dotnet @commandArguments
exit $LASTEXITCODE
