param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

function To-RelativePath {
    param([string]$Path)
    return [IO.Path]::GetRelativePath($RepoRoot, $Path).Replace("\", "/")
}

function New-List {
    New-Object 'System.Collections.Generic.List[object]'
}

function Get-CqrsSymbols {
    param([System.IO.FileInfo[]]$Files)

    $commands = New-List
    $queries = New-List
    $handlers = New-List

    foreach ($file in $Files) {
        $relative = To-RelativePath $file.FullName

        foreach ($m in (Select-String -LiteralPath $file.FullName -Pattern 'public\s+sealed\s+record\s+(?<name>[A-Za-z0-9_]+)\b[^;\r\n\{]*:\s*ICommand<' -AllMatches)) {
            $commands.Add([pscustomobject]@{
                    Name = $m.Matches[0].Groups["name"].Value
                    File = $relative
                    Line = $m.LineNumber
                })
        }

        foreach ($m in (Select-String -LiteralPath $file.FullName -Pattern 'public\s+sealed\s+record\s+(?<name>[A-Za-z0-9_]+)\b[^;\r\n\{]*:\s*IQuery<' -AllMatches)) {
            $queries.Add([pscustomobject]@{
                    Name = $m.Matches[0].Groups["name"].Value
                    File = $relative
                    Line = $m.LineNumber
                })
        }

        foreach ($m in (Select-String -LiteralPath $file.FullName -Pattern 'public\s+(?:sealed\s+)?class\s+(?<name>[A-Za-z0-9_]+)\b[^;\r\n\{]*:\s*ICommandHandler<' -AllMatches)) {
            $handlers.Add([pscustomobject]@{
                    Name = $m.Matches[0].Groups["name"].Value
                    File = $relative
                    Line = $m.LineNumber
                })
        }
    }

    return [pscustomobject]@{
        Commands = $commands.ToArray()
        Queries = $queries.ToArray()
        Handlers = $handlers.ToArray()
    }
}

function Get-HostWrapperInventory {
    param([string]$RelativePath)

    $path = Join-Path $RepoRoot $RelativePath
    $wrappers = New-List
    if (-not (Test-Path -LiteralPath $path)) {
        return $wrappers.ToArray()
    }

    foreach ($file in (Get-ChildItem -LiteralPath $path -Filter "*ViewModel.cs" -File | Sort-Object FullName)) {
        $relative = To-RelativePath $file.FullName
        foreach ($m in (Select-String -LiteralPath $file.FullName -Pattern 'public\s+(?:sealed\s+)?partial\s+class\s+(?<name>[A-Za-z0-9_]+ViewModel)\s*:\s*(?<base>[^\r\n\{]+)' -AllMatches)) {
            $wrappers.Add([pscustomobject]@{
                    Name = $m.Matches[0].Groups["name"].Value
                    BaseType = ($m.Matches[0].Groups["base"].Value -split ",")[0].Trim()
                    File = $relative
                    Line = $m.LineNumber
                })
        }
    }

    $structuralFiles = @(
        "ViewModelBase.cs",
        "EditorTab.cs"
    )

    foreach ($name in $structuralFiles) {
        $filePath = Join-Path $path $name
        if (-not (Test-Path -LiteralPath $filePath)) { continue }
        $relative = To-RelativePath $filePath
        foreach ($m in (Select-String -LiteralPath $filePath -Pattern 'public\s+class\s+(?<name>[A-Za-z0-9_]+)\s*:\s*(?<base>[^\r\n\{]+)' -AllMatches)) {
            $wrappers.Add([pscustomobject]@{
                    Name = $m.Matches[0].Groups["name"].Value
                    BaseType = ($m.Matches[0].Groups["base"].Value -split ",")[0].Trim()
                    File = $relative
                    Line = $m.LineNumber
                })
        }
    }

    return $wrappers.ToArray()
}

function Get-LocalViewModelDeclarations {
    param([string]$RelativePath)

    $path = Join-Path $RepoRoot $RelativePath
    $declarations = New-List
    if (-not (Test-Path -LiteralPath $path)) {
        return $declarations.ToArray()
    }

    foreach ($m in (Get-ChildItem -LiteralPath $path -Recurse -Filter "*.cs" -File | Select-String -Pattern '\bclass\s+(?<name>[A-Za-z0-9_]+ViewModel)\b')) {
        $declarations.Add([pscustomobject]@{
                Name = $m.Matches[0].Groups["name"].Value
                File = To-RelativePath $m.Path
                Line = $m.LineNumber
            })
    }

    return $declarations.ToArray()
}

$managerCommandFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $RepoRoot "src/McpServerManager.Core/Commands") -Filter "*.cs" -File
)
$uiCoreMessageFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $RepoRoot "lib/McpServer/src/McpServer.UI.Core/Messages") -Filter "*.cs" -File
)

$managerSymbols = Get-CqrsSymbols -Files $managerCommandFiles
$uiCoreSymbols = Get-CqrsSymbols -Files $uiCoreMessageFiles
$hostWrappers = Get-HostWrapperInventory -RelativePath "src/McpServerManager.Core/ViewModels"
$directorLocal = Get-LocalViewModelDeclarations -RelativePath "lib/McpServer/src/McpServer.Director"
$webLocal = Get-LocalViewModelDeclarations -RelativePath "lib/McpServer/src/McpServer.Web"

$inventory = [pscustomobject]@{
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    RepoRoot = $RepoRoot
    ManagerCore = [pscustomobject]@{
        CommandFileCount = $managerCommandFiles.Count
        CommandCount = @($managerSymbols.Commands).Count
        HandlerCount = @($managerSymbols.Handlers).Count
        Commands = @($managerSymbols.Commands | Sort-Object Name, File)
        Handlers = @($managerSymbols.Handlers | Sort-Object Name, File)
    }
    UiCoreMessages = [pscustomobject]@{
        MessageFileCount = $uiCoreMessageFiles.Count
        CommandCount = @($uiCoreSymbols.Commands).Count
        QueryCount = @($uiCoreSymbols.Queries).Count
        Commands = @($uiCoreSymbols.Commands | Sort-Object Name, File)
        Queries = @($uiCoreSymbols.Queries | Sort-Object Name, File)
    }
    HostWrappers = [pscustomobject]@{
        Count = @($hostWrappers).Count
        Items = @($hostWrappers | Sort-Object Name, File)
    }
    HostIsolation = [pscustomobject]@{
        DirectorLocalViewModelCount = @($directorLocal).Count
        WebLocalViewModelCount = @($webLocal).Count
        DirectorLocalViewModels = @($directorLocal | Sort-Object Name, File)
        WebLocalViewModels = @($webLocal | Sort-Object Name, File)
    }
}

$json = $inventory | ConvertTo-Json -Depth 12

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
    Write-Host "UI.Core migration inventory written: $fullPath"
}

$json
