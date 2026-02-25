param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
)

$ErrorActionPreference = "Stop"

$commandsPath = Join-Path $RepoRoot "src/McpServerManager.Core/Commands"
if (-not (Test-Path -LiteralPath $commandsPath)) {
    throw "Commands path not found: $commandsPath"
}

$rules = @(
    @{
        Id = "CQRS001"
        Description = "Commands/queries must not declare ViewModel-typed properties"
        Pattern = "\b[A-Za-z0-9_]+ViewModel\s+[A-Za-z0-9_]+\s*\{\s*get\s*;\s*\}"
    },
    @{
        Id = "CQRS002"
        Description = "Handlers must not call through command.ViewModel"
        Pattern = "\.ViewModel\."
    },
    @{
        Id = "CQRS003"
        Description = "Handlers must not invoke ViewModel internal methods"
        Pattern = "Internal\s*\("
    }
)

$findings = New-Object System.Collections.Generic.List[object]

Get-ChildItem -LiteralPath $commandsPath -Filter "*.cs" -File |
    Sort-Object FullName |
    ForEach-Object {
        $file = $_
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
    Write-Host "CQRS boundary check passed."
    exit 0
}

Write-Error ("CQRS boundary violations found: {0}" -f $findings.Count)
$findings |
    Sort-Object File, Line, Rule |
    Format-Table Rule, File, Line, Text -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1

