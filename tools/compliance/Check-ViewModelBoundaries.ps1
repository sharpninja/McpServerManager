param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [switch]$IncludeLegacy
)

$ErrorActionPreference = "Stop"

$scopePaths = @(
    (Join-Path $RepoRoot "src/McpServerManager.Core/ViewModels")
)

if ($IncludeLegacy) {
    $scopePaths += (Join-Path $RepoRoot "src/McpServerManager/ViewModels")
}

$scopeFiles = @()
foreach ($path in $scopePaths) {
    if (Test-Path -LiteralPath $path) {
        $scopeFiles += Get-ChildItem -LiteralPath $path -Filter "*.cs" -File
    }
}

$rules = @(
    @{
        Id = "VM001"
        Description = "ViewModels must not construct application services"
        Pattern = "new\s+(?:Mcp[A-Za-z0-9_]*Service|OllamaLogAgentService)\b"
    },
    @{
        Id = "VM002"
        Description = "ViewModels must not register CQRS handlers (composition root)"
        Pattern = "_mediator\.Register(?:Query)?\s*\("
    },
    @{
        Id = "VM003"
        Description = "ViewModels must not use filesystem APIs directly"
        Pattern = "\b(?:File|Directory)\."
    },
    @{
        Id = "VM004"
        Description = "ViewModels must not launch processes directly"
        Pattern = "(?:\bProcess\.Start\b|System\.Diagnostics\.Process\.Start)"
    },
    @{
        Id = "VM005"
        Description = "ViewModels must not own watcher/timer app infrastructure"
        Pattern = "(?:FileSystemWatcher|new\s+Timer\s*\()"
    },
    @{
        Id = "VM006"
        Description = "ViewModels must not parse JSON for app/domain workflows"
        Pattern = "(?:JsonDocument\.Parse|JsonNode\.Parse)"
    },
    @{
        Id = "VM007"
        Description = "ViewModels must not use HttpClient directly"
        Pattern = "\bHttpClient\b"
    }
)

$findings = New-Object System.Collections.Generic.List[object]

foreach ($file in ($scopeFiles | Sort-Object FullName)) {
    foreach ($rule in $rules) {
        $matches = Select-String -LiteralPath $file.FullName -Pattern $rule.Pattern -AllMatches
        foreach ($match in $matches) {
            $relative = [IO.Path]::GetRelativePath($RepoRoot, $file.FullName).Replace("\", "/")
            $findings.Add([pscustomobject]@{
                    Rule = $rule.Id
                    Description = $rule.Description
                    File = $relative
                    Line = $match.LineNumber
                    Text = $match.Line.Trim()
                })
        }
    }
}

if ($findings.Count -eq 0) {
    Write-Host "ViewModel boundary check passed."
    exit 0
}

# Phase 0 baseline: existing violations in MainWindowViewModel are tracked but not blocking.
# Fail only if new violations appear (count exceeds baseline).
$baseline = 125
$scopeLabel = if ($IncludeLegacy) { "Core + legacy" } else { "Core only (legacy excluded by Phase 0 scope decision)" }

if ($findings.Count -le $baseline) {
    Write-Warning ("ViewModel boundary violations ({0}): {1} (at or below Phase 0 baseline of {2})" -f $scopeLabel, $findings.Count, $baseline)
    $findings |
        Sort-Object File, Line, Rule |
        Format-Table Rule, File, Line, Text -AutoSize |
        Out-String -Width 240 |
        Write-Host
    exit 0
}

Write-Error ("ViewModel boundary violations ({0}): {1} — exceeds Phase 0 baseline of {2}" -f $scopeLabel, $findings.Count, $baseline)
$findings |
    Sort-Object File, Line, Rule |
    Format-Table Rule, File, Line, Text -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1

