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
    -Rule "SHM001" `
    -RelativePath "src/McpServer.UI.Core/Commands/CqrsRelayFactory.cs" `
    -Pattern "Create<T>\(Dispatcher\s+dispatcher,\s*Action<T\?>\s+action,\s*Func<T\?,\s*bool>\?\s+canExecute\)" `
    -MissingMessage "UI.Core relay factory must expose a parameter-aware Action<T?> canExecute overload for legacy host call patterns."

Assert-ContainsPattern `
    -Rule "SHM002" `
    -RelativePath "src/McpServer.UI.Core/Commands/CqrsRelayFactory.cs" `
    -Pattern "Create<T>\(Dispatcher\s+dispatcher,\s*Func<T\?,\s*Task>\s+action,\s*Func<T\?,\s*bool>\?\s+canExecute\)" `
    -MissingMessage "UI.Core relay factory must expose a parameter-aware Func<T?, Task> canExecute overload for compatibility."

Assert-ContainsPattern `
    -Rule "SHM003" `
    -RelativePath "src/McpServerManager.Core/Commands/CqrsRelayFactory.cs" `
    -Pattern "McpServer\.UI\.Core\.Commands\.CqrsRelayFactory\.Create\(" `
    -MissingMessage "Core relay factory must delegate to UI.Core relay factory to keep command execution behavior aligned."

Assert-ContainsPattern `
    -Rule "SHM004" `
    -RelativePath "src/McpServerManager.Core/ViewModels/MainWindowViewModel.Commands.cs" `
    -Pattern "ArchiveTreeItemCommand\s*=>[\s\S]*CqrsRelayFactory\.Create<FileNode\?>\(_dispatcher,\s*ArchiveTreeItem,\s*CanArchiveTreeItem\)" `
    -MissingMessage "ArchiveTreeItemCommand must use the compatibility factory overload instead of direct CqrsRelayCommand construction."

if ($findings.Count -eq 0) {
    Write-Host "UI.Core compatibility shims check passed."
    exit 0
}

Write-Host ("UI.Core compatibility shims check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
