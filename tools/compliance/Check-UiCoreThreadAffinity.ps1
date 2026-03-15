param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
)

$ErrorActionPreference = "Stop"

$findings = [System.Collections.Generic.List[object]]::new()

function Add-Finding {
    param(
        [string]$Rule,
        [string]$File,
        [string]$Message
    )

    $findings.Add([pscustomobject]@{
            Rule = $Rule
            File = $File
            Message = $Message
    })
}

function Resolve-RepoPath {
    param([string]$RelativePath)

    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add($RelativePath)

    if ($RelativePath -match '^src[\\/](.+)$') {
        $candidates.Add("lib/McpServer/src/$($matches[1])")
    }

    if ($RelativePath -match '^lib[\\/]McpServer[\\/]src[\\/](.+)$') {
        $candidates.Add("src/$($matches[1])")
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        $path = Join-Path $RepoRoot $candidate
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    return Join-Path $RepoRoot $RelativePath
}

function Assert-ContainsPattern {
    param(
        [string]$Rule,
        [string]$RelativePath,
        [string]$Pattern,
        [string]$MissingMessage
    )

    $fullPath = Resolve-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        Add-Finding -Rule $Rule -File $RelativePath -Message "Required file not found."
        return
    }

    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($content -notmatch $Pattern) {
        Add-Finding -Rule $Rule -File $RelativePath -Message $MissingMessage
    }
}

Assert-ContainsPattern `
    -Rule "THR001" `
    -RelativePath "src/McpServerManager.Core/ServiceCollectionExtensions.cs" `
    -Pattern "AddSingleton<(?:(?:McpServer\.UI\.Core\.Services\.)?IUiDispatcherService|IUiDispatcherService)>\s*\([^)]*\bnew\s+AvaloniaUiDispatcherService\s*\(\s*\)\s*\)" `
    -MissingMessage "Host UI.Core service provider must register AvaloniaUiDispatcherService to preserve UI-thread execution."

Assert-ContainsPattern `
    -Rule "THR002" `
    -RelativePath "src/McpServerManager.Core/Services/AvaloniaUiDispatcherService.cs" `
    -Pattern "Dispatcher\.UIThread\.CheckAccess\(\)" `
    -MissingMessage "AvaloniaUiDispatcherService must guard direct execution with Dispatcher.UIThread.CheckAccess()."

Assert-ContainsPattern `
    -Rule "THR003" `
    -RelativePath "src/McpServerManager.Core/Services/AvaloniaUiDispatcherService.cs" `
    -Pattern "Dispatcher\.UIThread\.Post\(" `
    -MissingMessage "AvaloniaUiDispatcherService must marshal off-thread actions using Dispatcher.UIThread.Post()."

Assert-ContainsPattern `
    -Rule "THR004" `
    -RelativePath "src/McpServer.UI.Core/Commands/InvokeUiActionCommand.cs" `
    -Pattern "Dispatcher\.UIThread\.InvokeAsync\(" `
    -MissingMessage "UI.Core InvokeUiAction handler must marshal command delegates via Dispatcher.UIThread.InvokeAsync()."

if ($findings.Count -eq 0) {
    Write-Host "UI.Core thread-affinity check passed."
    exit 0
}

Write-Host ("UI.Core thread-affinity check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
