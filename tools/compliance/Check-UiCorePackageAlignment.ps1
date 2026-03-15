param(
    [string]$RepoRoot = (Resolve-Path (Join-Path (Join-Path $PSScriptRoot "..") "..")).Path
)

$ErrorActionPreference = "Stop"

$expectedVersions = @{
    "CommunityToolkit.Mvvm" = "8.4.0"
    "YamlDotNet" = "16.3.0"
}

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

    $normalized = $RelativePath -replace '[\\/]', [IO.Path]::DirectorySeparatorChar
    $candidate = Join-Path $RepoRoot $normalized

    if (Test-Path -LiteralPath $candidate) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }

    # Compatibility fallback for legacy and alternate layouts.
    if ($RelativePath -like "lib/McpServer/*") {
        $altRelative = $RelativePath -replace '^lib[\\/]', ''
        $altCandidate = Join-Path $RepoRoot $altRelative
        if (Test-Path -LiteralPath $altCandidate) {
            return (Resolve-Path -LiteralPath $altCandidate).Path
        }
    }

    return $null
}

function Get-XmlPackageVersion {
    param(
        [string]$Path,
        [string]$NodeName,
        [string]$PackageId
    )

    [xml]$xml = Get-Content -LiteralPath $Path -Raw
    $nodes = Select-Xml -Xml $xml -XPath "//$NodeName[@Include='$PackageId']"
    if ($nodes.Count -eq 0) {
        return $null
    }

    $node = $nodes[0].Node
    $attrVersion = $node.Attributes["Version"]?.Value
    if (-not [string]::IsNullOrWhiteSpace($attrVersion)) {
        return $attrVersion.Trim()
    }

    $childVersion = $node.SelectSingleNode("Version")?.InnerText
    if (-not [string]::IsNullOrWhiteSpace($childVersion)) {
        return $childVersion.Trim()
    }

    return ""
}

function Assert-PackageVersion {
    param(
        [string]$Rule,
        [string]$ProjectRelativePath,
        [string]$NodeName,
        [string]$PackageId,
        [string]$ExpectedVersion
    )

    $fullPath = Resolve-RepoPath -RelativePath $ProjectRelativePath
    if (-not $fullPath -or -not (Test-Path -LiteralPath $fullPath)) {
        Add-Finding -Rule $Rule -File $ProjectRelativePath -Message "Required file not found."
        return
    }

    $version = Get-XmlPackageVersion -Path $fullPath -NodeName $NodeName -PackageId $PackageId
    if ($null -eq $version) {
        Add-Finding -Rule $Rule -File $ProjectRelativePath -Message "Missing $NodeName Include='$PackageId'."
        return
    }

    if ($version -ne $ExpectedVersion) {
        Add-Finding -Rule $Rule -File $ProjectRelativePath -Message "Expected $PackageId version $ExpectedVersion but found '$version'."
    }
}

Assert-PackageVersion `
    -Rule "PKG001" `
    -ProjectRelativePath "lib/McpServer/Directory.Packages.props" `
    -NodeName "PackageVersion" `
    -PackageId "CommunityToolkit.Mvvm" `
    -ExpectedVersion $expectedVersions["CommunityToolkit.Mvvm"]

Assert-PackageVersion `
    -Rule "PKG002" `
    -ProjectRelativePath "lib/McpServer/Directory.Packages.props" `
    -NodeName "PackageVersion" `
    -PackageId "YamlDotNet" `
    -ExpectedVersion $expectedVersions["YamlDotNet"]

$managerProjects = @(
    "src/McpServerManager/McpServerManager.csproj",
    "src/McpServerManager.Core/McpServerManager.Core.csproj"
)

foreach ($project in $managerProjects) {
    Assert-PackageVersion `
        -Rule "PKG003" `
        -ProjectRelativePath $project `
        -NodeName "PackageReference" `
        -PackageId "CommunityToolkit.Mvvm" `
        -ExpectedVersion $expectedVersions["CommunityToolkit.Mvvm"]

    Assert-PackageVersion `
        -Rule "PKG004" `
        -ProjectRelativePath $project `
        -NodeName "PackageReference" `
        -PackageId "YamlDotNet" `
        -ExpectedVersion $expectedVersions["YamlDotNet"]
}

if ($findings.Count -eq 0) {
    Write-Host "UI.Core package alignment check passed."
    exit 0
}

Write-Host ("UI.Core package alignment check failed with {0} finding(s)." -f $findings.Count)
$findings |
    Sort-Object File, Rule |
    Format-Table Rule, File, Message -AutoSize |
    Out-String -Width 240 |
    Write-Host

exit 1
