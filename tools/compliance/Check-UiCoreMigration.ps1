param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
)

$ErrorActionPreference = "Stop"

$findings = New-Object System.Collections.Generic.List[object]

function Add-Finding {
    param(
        [string]$Rule,
        [string]$Description,
        [string]$File,
        [int]$Line,
        [string]$Text
    )

    $findings.Add([pscustomobject]@{
            Rule = $Rule
            Description = $Description
            File = $File
            Line = $Line
            Text = $Text
        })
}

function To-RelativePath {
    param([string]$Path)
    return [IO.Path]::GetRelativePath($RepoRoot, $Path).Replace("\", "/")
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

function Assert-ProjectReference {
    param(
        [string]$Rule,
        [string]$Description,
        [string]$ProjectRelativePath,
        [string]$ProjectReferencePattern
    )

    $projectPath = Resolve-RepoPath $ProjectRelativePath
    if (-not (Test-Path -LiteralPath $projectPath)) {
        Add-Finding -Rule $Rule -Description $Description -File $ProjectRelativePath -Line 1 -Text "Project file not found."
        return
    }

    $content = Get-Content -LiteralPath $projectPath -Raw
    if ($content -notmatch $ProjectReferencePattern) {
        Add-Finding -Rule $Rule -Description $Description -File $ProjectRelativePath -Line 1 -Text "Missing expected McpServer.UI.Core project reference."
    }
}

$projectReferenceChecks = @(
    @{
        Rule = "UIM001"
        Description = "McpServerManager.Core must reference McpServer.UI.Core."
        ProjectRelativePath = "src/McpServerManager.Core/McpServerManager.Core.csproj"
        ProjectReferencePattern = "<ProjectReference\s+Include=""(?:\.\.\\){1,2}(?:lib\\McpServer\\src\\)?McpServer\.UI\.Core\\McpServer\.UI\.Core\.csproj""\s*/?>"
    },
    @{
        Rule = "UIM002"
        Description = "McpServer.Web must reference McpServer.UI.Core."
        ProjectRelativePath = "src/McpServer.Web/McpServer.Web.csproj"
        ProjectReferencePattern = "<ProjectReference\s+Include=""(?:\.\.\\){1,2}(?:lib\\McpServer\\src\\)?McpServer\.UI\.Core\\McpServer\.UI\.Core\.csproj""\s*/?>"
    },
    @{
        Rule = "UIM003"
        Description = "McpServer.Director must reference McpServer.UI.Core."
        ProjectRelativePath = "src/McpServer.Director/McpServer.Director.csproj"
        ProjectReferencePattern = "<ProjectReference\s+Include=""(?:\.\.\\){1,2}(?:lib\\McpServer\\src\\)?McpServer\.UI\.Core\\McpServer\.UI\.Core\.csproj""\s*/?>"
    }
)

foreach ($check in $projectReferenceChecks) {
    Assert-ProjectReference `
        -Rule $check.Rule `
        -Description $check.Description `
        -ProjectRelativePath $check.ProjectRelativePath `
        -ProjectReferencePattern $check.ProjectReferencePattern
}

$managerViewModelPath = Join-Path $RepoRoot "src/McpServerManager.Core/ViewModels"
if (-not (Test-Path -LiteralPath $managerViewModelPath)) {
    Add-Finding -Rule "UIM100" -Description "McpServerManager.Core ViewModels path must exist." -File "src/McpServerManager.Core/ViewModels" -Line 1 -Text "Directory not found."
}
else {
    Get-ChildItem -LiteralPath $managerViewModelPath -Filter "*ViewModel.cs" -File |
        Sort-Object FullName |
        ForEach-Object {
            $classMatches = Select-String -LiteralPath $_.FullName -Pattern 'public\s+(?:sealed\s+)?partial\s+class\s+(?<name>[A-Za-z0-9_]+ViewModel)\s*:\s*(?<base>[^\r\n\{]+)' -AllMatches
            if ($classMatches.Count -eq 0) {
                Add-Finding `
                    -Rule "UIM101" `
                    -Description "Host ViewModel wrappers must inherit a McpServer.UI.Core.ViewModels base type." `
                    -File (To-RelativePath $_.FullName) `
                    -Line 1 `
                    -Text "No inheriting ViewModel declaration found."
                return
            }

            foreach ($match in $classMatches) {
                $baseList = $match.Matches[0].Groups["base"].Value
                $primaryBase = ($baseList -split ",")[0].Trim()
                if (-not $primaryBase.StartsWith("McpServer.UI.Core.ViewModels.", [StringComparison]::Ordinal)) {
                    Add-Finding `
                        -Rule "UIM101" `
                        -Description "Host ViewModel wrappers must inherit a McpServer.UI.Core.ViewModels base type." `
                        -File (To-RelativePath $_.FullName) `
                        -Line $match.LineNumber `
                        -Text $match.Line.Trim()
                }
            }
        }
}

$managerStructuralChecks = @(
    @{
        Rule = "UIM102"
        Description = "McpServerManager.Core ViewModelBase must inherit McpServer.UI.Core ViewModelBase."
        File = "src/McpServerManager.Core/ViewModels/ViewModelBase.cs"
        Pattern = 'public\s+class\s+ViewModelBase\s*:\s*McpServer\.UI\.Core\.ViewModels\.ViewModelBase'
        MissingText = "ViewModelBase does not inherit McpServer.UI.Core.ViewModels.ViewModelBase."
    },
    @{
        Rule = "UIM103"
        Description = "McpServerManager.Core EditorTab must inherit McpServer.UI.Core EditorTab."
        File = "src/McpServerManager.Core/ViewModels/EditorTab.cs"
        Pattern = 'public\s+class\s+EditorTab\s*:\s*McpServer\.UI\.Core\.ViewModels\.EditorTab'
        MissingText = "EditorTab does not inherit McpServer.UI.Core.ViewModels.EditorTab."
    }
)

foreach ($check in $managerStructuralChecks) {
    $filePath = Join-Path $RepoRoot $check.File
    if (-not (Test-Path -LiteralPath $filePath)) {
        Add-Finding -Rule $check.Rule -Description $check.Description -File $check.File -Line 1 -Text "File not found."
        continue
    }

    if (-not (Select-String -LiteralPath $filePath -Pattern $check.Pattern -Quiet)) {
        Add-Finding -Rule $check.Rule -Description $check.Description -File $check.File -Line 1 -Text $check.MissingText
    }
}

$noViewModelZones = @(
    @{
        Rule = "UIM201"
        Description = "McpServer.Director must not declare local *ViewModel classes."
        RelativePath = "src/McpServer.Director"
    },
    @{
        Rule = "UIM202"
        Description = "McpServer.Web must not declare local *ViewModel classes."
        RelativePath = "src/McpServer.Web"
    }
)

foreach ($zone in $noViewModelZones) {
    $zonePath = Resolve-RepoPath $zone.RelativePath
    if (-not (Test-Path -LiteralPath $zonePath)) {
        Add-Finding -Rule $zone.Rule -Description $zone.Description -File $zone.RelativePath -Line 1 -Text "Directory not found."
        continue
    }

    $zoneMatches = Get-ChildItem -LiteralPath $zonePath -Recurse -Filter "*.cs" -File |
        Select-String -Pattern '\bclass\s+[A-Za-z0-9_]+ViewModel\b'

    foreach ($zoneMatch in $zoneMatches) {
        Add-Finding `
            -Rule $zone.Rule `
            -Description $zone.Description `
            -File (To-RelativePath $zoneMatch.Path) `
            -Line $zoneMatch.LineNumber `
            -Text $zoneMatch.Line.Trim()
    }
}

if ($findings.Count -eq 0) {
    Write-Host "UI.Core migration guard check passed."
    exit 0
}

Write-Host ("UI.Core migration guard violations found: {0}" -f $findings.Count)
$findings |
    Sort-Object File, Line, Rule |
    Format-Table Rule, File, Line, Text -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
