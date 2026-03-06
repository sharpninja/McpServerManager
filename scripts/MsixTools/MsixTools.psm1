#Requires -Version 7.0
# MsixTools.psm1 — build and install MSIX packages from .NET projects.
# Import-Module ./scripts/MsixTools  (or via MsixTools.psd1)
# Reads project / build configuration from msix.yml in the workspace root.

Set-StrictMode -Version 3.0

# ── YAML support ──────────────────────────────────────────────────────────────
function Import-YamlModule {
    if (Get-Module -Name powershell-yaml -ErrorAction SilentlyContinue) { return }
    if (-not (Get-Module -ListAvailable -Name powershell-yaml -ErrorAction SilentlyContinue)) {
        Write-Host "[MsixTools] Installing powershell-yaml module..."
        if (-not (Get-PackageProvider -Name NuGet -ListAvailable -ErrorAction SilentlyContinue)) {
            Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null
        }
        Install-Module -Name powershell-yaml -Scope CurrentUser -Force -AllowClobber
    }
    Import-Module powershell-yaml -Force -ErrorAction Stop
}

# ── Private helpers ────────────────────────────────────────────────────────────

# Safe IDictionary access — works with both Hashtable and OrderedDictionary (powershell-yaml).
function Get-CfgValue {
    param($Table, [string]$Key, $Default = $null)
    if ($null -ne $Table -and $Table -is [System.Collections.IDictionary] -and $Table.Contains($Key)) {
        return $Table[$Key]
    }
    return $Default
}

function Find-WinSdkTool {
    param([string]$Name)
    $sdkRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (Test-Path $sdkRoot) {
        $found = Get-ChildItem $sdkRoot -Directory | Sort-Object Name -Descending | ForEach-Object {
            $p = Join-Path $_.FullName "x64\$Name"
            if (Test-Path $p) { $p }
        } | Select-Object -First 1
        if ($found) { return $found }
    }
    $inPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }
    return $null
}

function ConvertTo-MsixVersion {
    param([string]$SemVer)
    $base  = ($SemVer -split '-')[0]
    $parts = $base -split '\.'
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

function New-PlaceholderIconPng {
    param([string]$Path, [int]$W, [int]$H, [string]$Initials = 'RA')
    Add-Type -AssemblyName System.Drawing
    $bmp = [System.Drawing.Bitmap]::new($W, $H)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0x1E, 0x88, 0xE5))
    try {
        $fontSize = [int]([Math]::Max(8, $H * 0.35))
        $font = [System.Drawing.Font]::new('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold)
        $sf   = [System.Drawing.StringFormat]::new()
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = [System.Drawing.RectangleF]::new(0, 0, $W, $H)
        $g.DrawString($Initials, $font, [System.Drawing.Brushes]::White, $rect, $sf)
        $font.Dispose(); $sf.Dispose()
    } catch { }
    $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Write-MsixIcons {
    param(
        [string]$AssetsDir,
        [string]$IconSourceSvg = '',
        [string]$IconSourceDir = '',
        [string]$Initials = 'RA'
    )
    $iconSpecs = @(
        @{ Name = 'Square44x44Logo.png';   W = 44;  H = 44  },
        @{ Name = 'Square150x150Logo.png'; W = 150; H = 150 },
        @{ Name = 'Wide310x150Logo.png';   W = 310; H = 150 },
        @{ Name = 'Square310x310Logo.png'; W = 310; H = 310 },
        @{ Name = 'StoreLogo.png';         W = 50;  H = 50  }
    )
    foreach ($spec in $iconSpecs) {
        $iconPath = Join-Path $AssetsDir $spec.Name
        if ($IconSourceDir -and (Test-Path (Join-Path $IconSourceDir $spec.Name))) {
            Copy-Item (Join-Path $IconSourceDir $spec.Name) $iconPath -Force
            continue
        }
        $rendered = $false
        if ($IconSourceSvg -and (Test-Path $IconSourceSvg)) {
            $inkscape = Get-Command inkscape -ErrorAction SilentlyContinue
            $magick   = Get-Command magick   -ErrorAction SilentlyContinue
            if ($inkscape) {
                & inkscape --export-filename="$iconPath" --export-width=$spec.W --export-height=$spec.H "$IconSourceSvg" 2>$null
                $rendered = $LASTEXITCODE -eq 0
            } elseif ($magick) {
                & magick -background none -resize "$($spec.W)x$($spec.H)" "$IconSourceSvg" "$iconPath" 2>$null
                $rendered = $LASTEXITCODE -eq 0
            }
        }
        if (-not $rendered) { New-PlaceholderIconPng -Path $iconPath -W $spec.W -H $spec.H -Initials $Initials }
    }
}

function Resolve-PackageVersion {
    param([string]$WorkspaceRoot, [string]$Tag = '[MsixTools]')
    try {
        $gvJson = dotnet tool run dotnet-gitversion -- /output json 2>$null | ConvertFrom-Json
        if ($gvJson -and $gvJson.SemVer) {
            Write-Host "$Tag version source: dotnet-gitversion -> $($gvJson.SemVer)"
            return $gvJson.SemVer
        }
    } catch { }

    $gvYml = Join-Path $WorkspaceRoot 'GitVersion.yml'
    if (Test-Path $gvYml) {
        $m = (Get-Content $gvYml | Select-String '^\s*next-version:\s*(.+)').Matches
        if ($m.Count -gt 0) {
            $v = $m[0].Groups[1].Value.Trim()
            Write-Host "$Tag version source: GitVersion.yml next-version -> $v"
            return $v
        }
    }

    $gitTag = git -C $WorkspaceRoot describe --tags --abbrev=0 2>$null
    if ($gitTag -match '^v?(\d+\.\d+\.\d+)') {
        $v = $Matches[1]
        Write-Host "$Tag version source: git tag ($gitTag) -> $v"
        return $v
    }

    Write-Host "$Tag version source: hardcoded fallback -> 0.1.0"
    return '0.1.0'
}

# ── Public functions ───────────────────────────────────────────────────────────

function Read-MsixConfig {
    <#
    .SYNOPSIS
        Parse an msix.yml configuration file into a hashtable.
    .PARAMETER Path
        Path to msix.yml. Defaults to msix.yml in the current directory.
    #>
    [CmdletBinding()]
    param([string]$Path = '')
    if (-not $Path) { $Path = Join-Path (Get-Location).Path 'msix.yml' }
    if (-not (Test-Path $Path)) { Write-Error "msix.yml not found: $Path" }
    Import-YamlModule
    return Get-Content $Path -Raw | ConvertFrom-Yaml
}

function New-MsixPackage {
    <#
    .SYNOPSIS
        Build an MSIX package from .NET projects defined in msix.yml or parameters.

    .DESCRIPTION
        Reads defaults from msix.yml (when present); explicit parameters always take precedence.
        Publishes the configured projects, assembles an MSIX layout with AppxManifest.xml,
        optionally signs the package, and optionally installs it.

        Minimum requirement: -ServiceProject or -DesktopProject (or matching msix.yml sections).

    .PARAMETER WorkspaceRoot
        Root directory of the workspace. Defaults to the current directory.

    .PARAMETER ConfigPath
        Path to msix.yml. Auto-detected in WorkspaceRoot if omitted.

    .PARAMETER PackageName
        MSIX Identity Name (no spaces). Read from msix.yml package.name if omitted.

    .PARAMETER PackageDisplayName
        Human-readable name. Read from msix.yml package.displayName if omitted.

    .PARAMETER Publisher
        MSIX Identity Publisher (must match signing certificate Subject exactly).
        Default: "CN=<PackageName> Dev".

    .PARAMETER Version
        SemVer version string. Auto-detected via dotnet-gitversion / GitVersion.yml / git tag.

    .PARAMETER ServiceProject
        Hashtable: Path, Framework (net10.0), ServiceName, SubDir (service),
        StartAccount (localSystem), StartupType (auto). Merged with msix.yml service section.

    .PARAMETER DesktopProject
        Hashtable: Path, Framework (net9.0), SubDir (desktop), Executable,
        AppId, DisplayName, Description. Merged with msix.yml desktop section.

    .PARAMETER PluginProjects
        Array of hashtables: Path, Framework (net10.0), DestSubDir (service\plugins).

    .PARAMETER ExcludeService
        Skip the service project even when msix.yml defines one.

    .PARAMETER ExcludeDesktop
        Skip the desktop project even when msix.yml defines one.

    .PARAMETER Configuration
        Build configuration: Release (default) or Debug.

    .PARAMETER RuntimeId
        Target RID. Default: win-x64.

    .PARAMETER SelfContained
        Publish self-contained single-file. Default: $true (or msix.yml build.selfContained).

    .PARAMETER NuGetConfig
        Path to NuGet.Config. Auto-detected in WorkspaceRoot if omitted.

    .PARAMETER IconSourceSvg
        SVG file to rasterize for MSIX icons. Auto-detected from msix.yml icons.svg.

    .PARAMETER IconSourceDir
        Directory of pre-sized PNG icon files matching MSIX naming conventions.

    .PARAMETER CertThumbprint
        SHA1 thumbprint of a certificate in Cert:\CurrentUser\My to sign with.

    .PARAMETER DevCert
        Create or reuse a self-signed dev certificate and sign the package.

    .PARAMETER BumpMajor
        Increment the major component of next-version in GitVersion.yml before building
        (resets minor and patch to 0). Mutually exclusive with -BumpMinor and -BumpPatch.

    .PARAMETER BumpMinor
        Increment the minor component of next-version in GitVersion.yml before building
        (resets patch to 0). Mutually exclusive with -BumpMajor and -BumpPatch.

    .PARAMETER BumpPatch
        Increment the patch component of next-version in GitVersion.yml before building.
        Mutually exclusive with -BumpMajor and -BumpMinor.

    .PARAMETER Clean
        Delete bin/ and obj/ directories before publishing.

    .PARAMETER NoBuild
        Skip dotnet publish; use existing publish output. Mutually exclusive with -Clean.

    .PARAMETER Force
        Skip the AppxManifest review pause.

    .PARAMETER Install
        Call Install-MsixPackage after packaging.

    .PARAMETER OutDir
        Output directory. Default: msix.yml output.dir or <WorkspaceRoot>\artifacts.
    #>
    [CmdletBinding()]
    param(
        [string]      $WorkspaceRoot = (Get-Location).Path,
        [string]      $ConfigPath = '',
        [string]      $PackageName = '',
        [string]      $PackageDisplayName = '',
        [string]      $Publisher = '',
        [string]      $Version = '',
        [hashtable]   $ServiceProject = $null,
        [hashtable]   $DesktopProject = $null,
        [hashtable[]] $PluginProjects = @(),
        [switch]      $ExcludeService,
        [switch]      $ExcludeDesktop,
        [ValidateSet('Debug', 'Release')]
        [string]      $Configuration = '',
        [string]      $RuntimeId = '',
        [object]      $SelfContained = $null,   # $null means "read from YAML / default $true"
        [string]      $NuGetConfig = '',
        [string]      $IconSourceSvg = '',
        [string]      $IconSourceDir = '',
        [string]      $CertThumbprint = '',
        [switch]      $DevCert,
        [switch]      $Clean,
        [switch]      $NoBuild,
        [switch]      $BumpMajor,
        [switch]      $BumpMinor,
        [switch]      $BumpPatch,
        [switch]      $Force,
        [switch]      $Install,
        [string]      $OutDir = ''
    )

    $ErrorActionPreference = 'Stop'
    $tag = '[New-MsixPackage]'

    # Establish an absolute WorkspaceRoot so all derived paths are also absolute.
    $WorkspaceRoot = Convert-Path $WorkspaceRoot

    # ── Load msix.yml ──────────────────────────────────────────────────────────
    if (-not $ConfigPath) { $ConfigPath = Join-Path $WorkspaceRoot 'msix.yml' }
    $cfg = $null
    if (Test-Path $ConfigPath) {
        Write-Host "$tag reading config: $ConfigPath"
        $cfg = Read-MsixConfig -Path $ConfigPath
    }

    $cfgPkg     = Get-CfgValue $cfg 'package'
    $cfgSvc     = Get-CfgValue $cfg 'service'
    $cfgDsk     = Get-CfgValue $cfg 'desktop'
    $cfgPlugins = Get-CfgValue $cfg 'plugins' @()
    if ($null -eq $cfgPlugins) { $cfgPlugins = @() }
    if ($cfgPlugins -is [System.Collections.IDictionary]) { $cfgPlugins = @($cfgPlugins) }
    $cfgBuild   = Get-CfgValue $cfg 'build'
    $cfgOutput  = Get-CfgValue $cfg 'output'
    $cfgIcons   = Get-CfgValue $cfg 'icons'

    # ── Apply YAML defaults (params win) ──────────────────────────────────────
    if (-not $PackageName)        { $PackageName        = Get-CfgValue $cfgPkg 'name'          'RemoteAgent' }
    if (-not $PackageDisplayName) { $PackageDisplayName = Get-CfgValue $cfgPkg 'displayName'   $PackageName  }
    if (-not $Publisher)          { $Publisher          = Get-CfgValue $cfgPkg 'publisher'      "CN=$PackageName Dev" }
    if (-not $Configuration)      { $Configuration      = Get-CfgValue $cfgBuild 'configuration' 'Release' }
    if (-not $RuntimeId)          { $RuntimeId          = Get-CfgValue $cfgBuild 'rid'           'win-x64' }
    if ($null -eq $SelfContained) { $SelfContained      = [bool](Get-CfgValue $cfgBuild 'selfContained' $true) }
    if (-not $OutDir) {
        $rel = Get-CfgValue $cfgOutput 'dir' 'artifacts'
        $OutDir = if ([System.IO.Path]::IsPathRooted($rel)) { $rel } else { Join-Path $WorkspaceRoot $rel }
    }
    if (-not $IconSourceSvg) {
        $svg = Get-CfgValue $cfgIcons 'svg' ''
        if ($svg) { $IconSourceSvg = if ([System.IO.Path]::IsPathRooted($svg)) { $svg } else { Join-Path $WorkspaceRoot $svg } }
    }
    if (-not $NuGetConfig) {
        $nc = Join-Path $WorkspaceRoot 'NuGet.Config'
        if (Test-Path $nc) { $NuGetConfig = $nc }
    }

    # ── Build project objects from YAML if not supplied via params ─────────────
    if (-not $ServiceProject -and $cfgSvc) {
        $p = Get-CfgValue $cfgSvc 'path' ''
        if ($p) {
            $ServiceProject = @{
                Path         = $p
                Framework    = Get-CfgValue $cfgSvc 'framework'    'net10.0'
                ServiceName  = Get-CfgValue $cfgSvc 'serviceName'  $PackageName
                DisplayName  = Get-CfgValue $cfgSvc 'displayName'  "$PackageDisplayName Service"
                SubDir       = Get-CfgValue $cfgSvc 'subDir'       'service'
                StartAccount = Get-CfgValue $cfgSvc 'startAccount' 'localSystem'
                StartupType  = Get-CfgValue $cfgSvc 'startupType'  'auto'
            }
        }
    }
    if (-not $DesktopProject -and $cfgDsk) {
        $p = Get-CfgValue $cfgDsk 'path' ''
        if ($p) {
            $DesktopProject = @{
                Path        = $p
                Framework   = Get-CfgValue $cfgDsk 'framework'   'net9.0'
                SubDir      = Get-CfgValue $cfgDsk 'subDir'      'desktop'
                AppId       = Get-CfgValue $cfgDsk 'appId'       "${PackageName}App"
                DisplayName = Get-CfgValue $cfgDsk 'displayName' $PackageDisplayName
                Description = Get-CfgValue $cfgDsk 'description' "$PackageDisplayName desktop application"
            }
        }
    }
    if ($PluginProjects.Count -eq 0 -and $cfgPlugins.Count -gt 0) {
        $PluginProjects = @($cfgPlugins | ForEach-Object {
            $pp = $_
            $p  = Get-CfgValue $pp 'path' ''
            if ($p) {
                @{
                    Path       = $p
                    Framework  = Get-CfgValue $pp 'framework'  'net10.0'
                    DestSubDir = (Get-CfgValue $pp 'destSubDir' 'service/plugins') -replace '/', '\'
                }
            }
        } | Where-Object { $_ })
    }

    # ── Apply exclusion flags ──────────────────────────────────────────────────
    if ($ExcludeService) { $ServiceProject = $null }
    if ($ExcludeDesktop) { $DesktopProject = $null }

    # ── Validate ──────────────────────────────────────────────────────────────
    if (-not $ServiceProject -and -not $DesktopProject) {
        Write-Error "$tag No projects configured. Provide -ServiceProject/-DesktopProject or an msix.yml."
    }
    if ($DevCert -and $CertThumbprint) {
        Write-Error "$tag -DevCert and -CertThumbprint are mutually exclusive."
    }
    if ($Clean -and $NoBuild) {
        Write-Error "$tag -Clean and -NoBuild are mutually exclusive."
    }
    if (($BumpMajor.IsPresent + $BumpMinor.IsPresent + $BumpPatch.IsPresent) -gt 1) {
        Write-Error "$tag -BumpMajor, -BumpMinor, and -BumpPatch are mutually exclusive."
    }

    # ── Version bump ──────────────────────────────────────────────────────────
    if ($BumpMajor -or $BumpMinor -or $BumpPatch) {
        $gvYml = Join-Path $WorkspaceRoot 'GitVersion.yml'
        if (-not (Test-Path $gvYml)) { Write-Error "$tag GitVersion.yml not found at $gvYml." }
        $current = (Get-Content $gvYml | Select-String '^\s*next-version:\s*(.+)').Matches[0].Groups[1].Value.Trim()
        $parts   = $current -split '\.'
        if ($parts.Count -lt 3 -or ($parts | Where-Object { $_ -notmatch '^\d+$' })) {
            Write-Error "$tag Cannot parse next-version '$current' in GitVersion.yml."
        }
        [int]$maj = $parts[0]; [int]$min = $parts[1]; [int]$pat = $parts[2]
        if      ($BumpMajor) { $maj++; $min = 0; $pat = 0 }
        elseif  ($BumpMinor) { $min++; $pat = 0 }
        else                 { $pat++ }
        $newVer = "$maj.$min.$pat"
        (Get-Content $gvYml -Raw) -replace '(?m)^(next-version:\s*).*', "`${1}$newVer" |
            Set-Content $gvYml -Encoding UTF8
        Write-Host "$tag bumped next-version: $current -> $newVer"
    }

    # ── Version detection ─────────────────────────────────────────────────────
    if (-not $Version) {
        $Version = Resolve-PackageVersion -WorkspaceRoot $WorkspaceRoot -Tag $tag
    } else {
        Write-Host "$tag version source: -Version parameter -> $Version"
    }
    $Version4 = ConvertTo-MsixVersion $Version

    # ── Architecture ──────────────────────────────────────────────────────────
    $MsixArch = switch ($RuntimeId) {
        'win-x86'   { 'x86'   }
        'win-arm64' { 'arm64' }
        default     { 'x64'   }
    }
    $pkgSlug  = $PackageName.ToLower() -replace '[^a-z0-9]', '-'
    $MsixFile = Join-Path $OutDir "${pkgSlug}_${Version}_${MsixArch}.msix"

    Write-Host "$tag version=$Version ($Version4)  rid=$RuntimeId  config=$Configuration  self-contained=$SelfContained"
    Write-Host "$tag service=$($null -ne $ServiceProject)  desktop=$($null -ne $DesktopProject)  out=$OutDir"

    # ── Locate Windows SDK tools ──────────────────────────────────────────────
    $MakeAppx = Find-WinSdkTool 'makeappx.exe'
    $SignTool  = Find-WinSdkTool 'signtool.exe'
    if (-not $MakeAppx) {
        Write-Error "$tag makeappx.exe not found.`n  Install with: winget install Microsoft.WindowsSDK.10.0.22621"
    }
    Write-Host "$tag makeappx : $MakeAppx"
    if ($SignTool) { Write-Host "$tag signtool : $SignTool" }

    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

    # ── Clean ─────────────────────────────────────────────────────────────────
    if ($Clean) {
        $projPaths = @()
        if ($ServiceProject) { $projPaths += $ServiceProject['Path'] }
        if ($DesktopProject) { $projPaths += $DesktopProject['Path'] }
        foreach ($pp in $PluginProjects) { $projPaths += $pp['Path'] }
        foreach ($p in $projPaths) {
            $dir = if ([System.IO.Path]::IsPathRooted($p)) { Split-Path $p } else { Join-Path $WorkspaceRoot (Split-Path $p) }
            foreach ($sub in @('bin', 'obj')) {
                $full = Join-Path $dir $sub
                if (Test-Path $full) {
                    Write-Host "$tag removing $full ..."
                    [System.IO.Directory]::Delete($full, $true)
                }
            }
        }
    }

    # ── Publish ───────────────────────────────────────────────────────────────
    $scFlags  = if ($SelfContained) { '--self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true' } else { '' }
    $nugetArg = if ($NuGetConfig)   { "--configfile `"$NuGetConfig`"" } else { '' }

    # Append version properties to all builds
    $verProps = "-p:Version=$Version -p:AssemblyVersion=$Version4 -p:FileVersion=$Version4 -p:InformationalVersion=$Version"
    $scFlags  = "$scFlags $verProps"

    function Invoke-Publish {
        param([string]$ProjPath, [string]$Framework, [string]$OutPath, [string]$Extra = '')
        $full = if ([System.IO.Path]::IsPathRooted($ProjPath)) { $ProjPath } else { Join-Path $WorkspaceRoot $ProjPath }
        $cmd  = "dotnet publish `"$full`" -c $Configuration -r $RuntimeId -f $Framework $nugetArg -o `"$OutPath`" $Extra"
        Invoke-Expression $cmd
        if ($LASTEXITCODE -ne 0) { throw "$tag dotnet publish failed (exit $LASTEXITCODE)." }
    }

    $ServicePub = $null
    $DesktopPub = $null

    if ($ServiceProject) {
        $ServicePub = Join-Path $OutDir 'publish-service'
        if ($NoBuild) {
            Write-Host "$tag skipping service build (-NoBuild), using: $ServicePub"
            if (-not (Test-Path $ServicePub)) { throw "$tag -NoBuild specified but publish output not found: $ServicePub" }
        } else {
            Write-Host "$tag publishing service -> $ServicePub"
            Invoke-Publish -ProjPath $ServiceProject['Path'] -Framework ($ServiceProject['Framework'] ?? 'net10.0') -OutPath $ServicePub -Extra $scFlags
        }

        foreach ($pp in $PluginProjects) {
            $pOut = Join-Path $OutDir "publish-plugin-$([System.IO.Path]::GetFileNameWithoutExtension($pp['Path']))"
            $pp['_Out']     = $pOut
            $pp['_DestSub'] = ($pp['DestSubDir'] ?? 'service\plugins') -replace '/', '\'
            if ($NoBuild) {
                Write-Host "$tag skipping plugin build (-NoBuild), using: $pOut"
                if (-not (Test-Path $pOut)) { throw "$tag -NoBuild specified but plugin output not found: $pOut" }
            } else {
                Write-Host "$tag publishing plugin -> $pOut"
                Invoke-Publish -ProjPath $pp['Path'] -Framework ($pp['Framework'] ?? 'net10.0') -OutPath $pOut -Extra "--self-contained false $verProps"
            }
        }
    }

    if ($DesktopProject) {
        $DesktopPub = Join-Path $OutDir 'publish-desktop'
        if ($NoBuild) {
            Write-Host "$tag skipping desktop build (-NoBuild), using: $DesktopPub"
            if (-not (Test-Path $DesktopPub)) { throw "$tag -NoBuild specified but publish output not found: $DesktopPub" }
        } else {
            Write-Host "$tag publishing desktop -> $DesktopPub"
            Invoke-Publish -ProjPath $DesktopProject['Path'] -Framework ($DesktopProject['Framework'] ?? 'net9.0') -OutPath $DesktopPub -Extra $scFlags
        }
    }

    # ── Assemble layout ───────────────────────────────────────────────────────
    $PkgRoot   = Join-Path $OutDir 'msix-layout'
    $AssetsDir = Join-Path $PkgRoot 'Assets'
    if (Test-Path $PkgRoot) { Remove-Item $PkgRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null

    # Auto-detect SVG if not configured
    if (-not $IconSourceSvg) {
        $auto = Get-ChildItem (Join-Path $WorkspaceRoot 'src') -Recurse -Filter 'appicon.svg' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($auto) { $IconSourceSvg = $auto.FullName }
    }
    $initials = (($PackageName -replace '[^A-Za-z]', '').ToUpper())[0..1] -join ''
    Write-MsixIcons -AssetsDir $AssetsDir -IconSourceSvg $IconSourceSvg -IconSourceDir $IconSourceDir -Initials $initials
    Write-Host "$tag icon assets written to $AssetsDir"

    if ($ServiceProject -and $ServicePub) {
        $svcSub  = ($ServiceProject['SubDir'] ?? 'service') -replace '/', '\'
        $svcDest = Join-Path $PkgRoot $svcSub
        New-Item -ItemType Directory -Path $svcDest -Force | Out-Null
        Copy-Item (Join-Path $ServicePub '*') $svcDest -Recurse -Force
        # Include appsettings.json if present alongside the .csproj
        $svcProjDir = if ([System.IO.Path]::IsPathRooted($ServiceProject['Path'])) {
            Split-Path $ServiceProject['Path']
        } else {
            Join-Path $WorkspaceRoot (Split-Path $ServiceProject['Path'])
        }
        $ap = Join-Path $svcProjDir 'appsettings.json'
        if (Test-Path $ap) { Copy-Item $ap $svcDest -Force }
        # Plugins
        foreach ($pp in $PluginProjects) {
            if ($pp['_Out']) {
                $pDest = Join-Path $PkgRoot $pp['_DestSub']
                New-Item -ItemType Directory -Path $pDest -Force | Out-Null
                Get-ChildItem $pp['_Out'] -Filter '*.dll' | Copy-Item -Destination $pDest -Force
            }
        }
    }

    if ($DesktopProject -and $DesktopPub) {
        $dskSub  = ($DesktopProject['SubDir'] ?? 'desktop') -replace '/', '\'
        $dskDest = Join-Path $PkgRoot $dskSub
        New-Item -ItemType Directory -Path $dskDest -Force | Out-Null
        Copy-Item (Join-Path $DesktopPub '*') $dskDest -Recurse -Force
    }

    # ── AppxManifest.xml ──────────────────────────────────────────────────────
    $svcSubDir = if ($ServiceProject) { ($ServiceProject['SubDir'] ?? 'service') -replace '/', '\' } else { 'service' }
    $dskSubDir = if ($DesktopProject) { ($DesktopProject['SubDir'] ?? 'desktop') -replace '/', '\' } else { 'desktop' }

    $svcExeRel = $null
    if ($ServiceProject) {
        $svcExe    = $ServiceProject['Executable'] ?? ([System.IO.Path]::GetFileNameWithoutExtension($ServiceProject['Path']) + '.exe')
        $svcExeRel = "$svcSubDir\$svcExe"
    }
    $dskExeRel = $null
    if ($DesktopProject) {
        $dskExe    = $DesktopProject['Executable'] ?? ([System.IO.Path]::GetFileNameWithoutExtension($DesktopProject['Path']) + '.exe')
        $dskExeRel = "$dskSubDir\$dskExe"
    }

    $appExe  = if ($DesktopProject) { $dskExeRel } else { $svcExeRel }
    $appDisp = if ($DesktopProject) { $DesktopProject['DisplayName'] ?? $PackageDisplayName } else { $PackageDisplayName }
    $appDesc = if ($DesktopProject) { $DesktopProject['Description'] ?? "$PackageDisplayName desktop application" } else { "$PackageDisplayName service host" }
    $appId   = if ($DesktopProject) { $DesktopProject['AppId'] ?? "${PackageName}App" } else { "${PackageName}App" }

    # Build service extension / application XML.
    # When both service and desktop are present the service gets its own
    # Application entry so its SCM display name can include the SemVer.
    $svcExtXml = ''   # extension inside main Application (service-only packages)
    $svcAppXml = ''   # dedicated second Application (combined service+desktop)
    if ($ServiceProject) {
        $svcName     = $ServiceProject['ServiceName'] ?? $PackageName
        $startAcct   = $ServiceProject['StartAccount'] ?? 'localSystem'
        $startType   = $ServiceProject['StartupType']  ?? 'auto'
        $svcBaseDisp = $ServiceProject['DisplayName']  ?? "$PackageDisplayName Service"
        $svcDispName = if ($Version) { "$svcBaseDisp $Version" } else { $svcBaseDisp }
        $svcDesc     = "$svcDispName gRPC host"
        $svcExtBlock = @"

      <Extensions>
        <desktop6:Extension Category="windows.service"
                             Executable="$svcExeRel"
                             EntryPoint="Windows.FullTrustApplication">
          <desktop6:Service Name="$svcName"
                             StartupType="$startType"
                             StartAccount="$startAcct" />
        </desktop6:Extension>
      </Extensions>
"@
        if ($DesktopProject) {
            # Combined package: give the service its own Application entry so
            # services.msc shows the versioned display name, not the desktop name.
            $svcAppXml = @"
    <Application Id="${PackageName}Svc"
                 Executable="$svcExeRel"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="$svcDispName"
        Description="$svcDesc"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
      </uap:VisualElements>
$svcExtBlock
    </Application>
"@
        } else {
            # Service-only package: keep extension inside the main Application.
            $svcExtXml = $svcExtBlock
        }
    }

    $rescapNs  = 'xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"'
    $d6Ns      = if ($ServiceProject) { 'xmlns:desktop6="http://schemas.microsoft.com/appx/manifest/desktop/windows10/6"' } else { '' }
    $ignorable = if ($ServiceProject) { 'IgnorableNamespaces="rescap desktop6"' } else { 'IgnorableNamespaces="rescap"' }
    $svcCaps   = if ($ServiceProject) { "`n    <rescap:Capability Name=`"packagedServices`" />`n    <rescap:Capability Name=`"localSystemServices`" />" } else { '' }

    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  $rescapNs
  $d6Ns
  $ignorable>

  <Identity
    Name="$PackageName"
    Publisher="$Publisher"
    Version="$Version4"
    ProcessorArchitecture="$MsixArch" />

  <Properties>
    <DisplayName>$PackageDisplayName</DisplayName>
    <PublisherDisplayName>$PackageDisplayName</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <rescap:Capability Name="allowElevation" />$svcCaps
  </Capabilities>

  <Applications>
    <Application Id="$appId"
                 Executable="$appExe"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="$appDisp"
        Description="$appDesc"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png"
                          Square310x310Logo="Assets\Square310x310Logo.png" />
      </uap:VisualElements>
$svcExtXml
    </Application>
$svcAppXml
  </Applications>

</Package>
"@

    $manifestPath = Join-Path $PkgRoot 'AppxManifest.xml'
    $manifest | Set-Content -Path $manifestPath -Encoding UTF8
    Write-Host "$tag wrote AppxManifest.xml"

    Write-Host ''
    Write-Host '── Version applied ──────────────────────────────────────────────────────'
    Write-Host "  SemVer        : $Version"
    Write-Host "  MSIX Identity : $Version4"
    Write-Host ''
    Write-Host '── AppxManifest.xml ─────────────────────────────────────────────────────'
    Get-Content $manifestPath | ForEach-Object { Write-Host "  $_" }
    Write-Host '─────────────────────────────────────────────────────────────────────────'
    Write-Host ''

    if (-not $Force) {
        Write-Host 'Press Enter to continue packaging, or Ctrl+C to abort ...'
        $null = Read-Host
    }

    # ── makeappx ──────────────────────────────────────────────────────────────
    Write-Host "$tag packing $MsixFile ..."
    $proc = Start-Process -FilePath $MakeAppx `
        -ArgumentList 'pack', '/d', "`"$PkgRoot`"", '/p', "`"$MsixFile`"", '/o', '/nv' `
        -NoNewWindow -PassThru -Wait
    if ($proc.ExitCode -ne 0) { throw "$tag makeappx failed (exit $($proc.ExitCode))." }
    Write-Host "$tag packed: $MsixFile"

    # ── Sign ──────────────────────────────────────────────────────────────────
    $signThumb = $CertThumbprint
    if ($DevCert -and -not $signThumb) {
        if (-not $SignTool) {
            Write-Warning "$tag signtool.exe not found — skipping signing."
        } else {
            $existing = Get-ChildItem Cert:\CurrentUser\My |
                Where-Object { $_.Subject -eq $Publisher -and $_.NotAfter -gt (Get-Date) } |
                Sort-Object NotAfter -Descending | Select-Object -First 1
            if ($existing) {
                Write-Host "$tag reusing existing dev cert: $($existing.Thumbprint)"
                $signThumb = $existing.Thumbprint
            } else {
                Write-Host "$tag creating self-signed dev certificate for '$Publisher'..."
                $cert = New-SelfSignedCertificate `
                    -Type Custom -Subject $Publisher `
                    -KeyUsage DigitalSignature `
                    -FriendlyName "$PackageDisplayName Dev Certificate" `
                    -CertStoreLocation 'Cert:\CurrentUser\My' `
                    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
                $signThumb = $cert.Thumbprint
                Write-Host "$tag dev cert thumbprint: $signThumb"
                $cerPath = Join-Path $OutDir "$pkgSlug-dev.cer"
                Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
                Write-Host "$tag exported cert: $cerPath"
            }
        }
    }

    if ($signThumb -and $SignTool) {
        Write-Host "$tag signing with thumbprint $signThumb ..."
        & $SignTool sign /sha1 $signThumb /fd SHA256 /tr http://timestamp.digicert.com /td sha256 "$MsixFile"
        if ($LASTEXITCODE -ne 0) { throw "$tag signtool failed (exit $LASTEXITCODE)." }
        Write-Host "$tag signed: $MsixFile"
    } elseif (-not $signThumb) {
        Write-Warning "$tag Package is unsigned — Install-MsixPackage will auto-sign on install."
    }

    # ── Summary ───────────────────────────────────────────────────────────────
    $msixSz = [math]::Round((Get-Item $MsixFile).Length / 1MB, 1)
    Write-Host ''
    Write-Host '── MSIX package ready ───────────────────────────────────────────────────'
    Write-Host "  File     : $MsixFile  ($msixSz MB)"
    Write-Host "  Identity : $PackageName  $Version4  $MsixArch"
    Write-Host "  Publisher: $Publisher"
    Write-Host '────────────────────────────────────────────────────────────────────────'
    Write-Host ''
    Write-Host "$tag calculated version : $Version  (MSIX identity: $Version4)"

    # ── Auto-install ──────────────────────────────────────────────────────────
    if ($Install) {
        $iParams = @{ MsixPath = $MsixFile; PackageName = $PackageName }
        $cerFile  = Join-Path $OutDir "$pkgSlug-dev.cer"
        if (Test-Path $cerFile) { $iParams['CertPath'] = $cerFile }
        if ($ServiceProject)    { $iParams['ServiceName']        = $ServiceProject['ServiceName'] ?? $PackageName }
        if ($DesktopProject)    { $iParams['DesktopProcessName'] = [System.IO.Path]::GetFileNameWithoutExtension($DesktopProject['Path']) }
        Install-MsixPackage @iParams
    }

    return $MsixFile
}

function Install-MsixPackage {
    <#
    .SYNOPSIS
        Install or update an MSIX package and start the associated Windows service.

    .DESCRIPTION
        Optionally trusts a signing certificate, auto-signs unsigned packages, stops any
        running service and desktop process, removes any existing version of the package,
        then performs a fresh install. Waiting for service registration is configurable.

        Using Remove + Add (rather than -ForceUpdateFromAnyVersion) avoids HRESULT 0x80073CFB
        when reinstalling a package with identical version but changed contents.

    .PARAMETER MsixPath
        Path to the .msix file. Auto-detected from OutDir if omitted.

    .PARAMETER ConfigPath
        Path to msix.yml for reading defaults (PackageName, ServiceName, etc.).

    .PARAMETER CertPath
        Path to a .cer file to trust in Cert:\LocalMachine\Root before installing.

    .PARAMETER PackageName
        AppxPackage identity name (for Get-AppxPackage). Read from msix.yml if omitted.

    .PARAMETER ServiceName
        Windows service to stop before install and start after. Read from msix.yml if omitted.

    .PARAMETER DesktopProcessName
        Process name (without .exe) to kill before updating. Derived from msix.yml desktop.path if omitted.

    .PARAMETER Publisher
        Publisher subject for auto-sign flow. Default: "CN=<PackageName> Dev".

    .PARAMETER TimeoutSeconds
        Seconds to wait for the service to register in the SCM after install. Default: 30.

    .PARAMETER OutDir
        Directory to search for .msix when MsixPath is not specified.
    #>
    [CmdletBinding()]
    param(
        [string] $MsixPath = '',
        [string] $ConfigPath = '',
        [string] $CertPath = '',
        [string] $PackageName = '',
        [string] $ServiceName = '',
        [string] $DesktopProcessName = '',
        [string] $Publisher = '',
        [int]    $TimeoutSeconds = 30,
        [string] $OutDir = ''
    )

    $ErrorActionPreference = 'Stop'
    $tag = '[Install-MsixPackage]'

    # ── Load msix.yml defaults ────────────────────────────────────────────────
    if (-not $ConfigPath -and -not $PackageName) {
        $auto = Join-Path (Get-Location).Path 'msix.yml'
        if (Test-Path $auto) { $ConfigPath = $auto }
    }
    if ($ConfigPath -and (Test-Path $ConfigPath)) {
        $cfg    = Read-MsixConfig -Path $ConfigPath
        $cfgPkg = Get-CfgValue $cfg 'package'
        $cfgSvc = Get-CfgValue $cfg 'service'
        $cfgDsk = Get-CfgValue $cfg 'desktop'
        $cfgOut = Get-CfgValue $cfg 'output'
        if (-not $PackageName)        { $PackageName        = Get-CfgValue $cfgPkg 'name'        '' }
        if (-not $ServiceName)        { $ServiceName        = Get-CfgValue $cfgSvc 'serviceName' '' }
        if (-not $DesktopProcessName) {
            # Prefer explicit processName key, fall back to deriving from path
            $pn = Get-CfgValue $cfgDsk 'processName' ''
            if ($pn) {
                $DesktopProcessName = $pn
            } else {
                $dskPath = Get-CfgValue $cfgDsk 'path' ''
                if ($dskPath) { $DesktopProcessName = [System.IO.Path]::GetFileNameWithoutExtension($dskPath) }
            }
        }
        if (-not $Publisher)          { $Publisher = Get-CfgValue $cfgPkg 'publisher' '' }
        if (-not $OutDir) {
            $rel = Get-CfgValue $cfgOut 'dir' 'artifacts'
            $OutDir = if ([System.IO.Path]::IsPathRooted($rel)) { $rel } else { Join-Path (Get-Location).Path $rel }
        }
    }
    if (-not $Publisher -and $PackageName) { $Publisher = "CN=$PackageName Dev" }

    # ── Resolve MSIX ──────────────────────────────────────────────────────────
    if (-not $MsixPath) {
        if (-not $OutDir) { Write-Error "$tag Provide -MsixPath, -OutDir, or an msix.yml with output.dir." }
        $slug   = if ($PackageName) { $PackageName.ToLower() -replace '[^a-z0-9]', '-' } else { '' }
        $filter = if ($slug) { "${slug}*_*_*.msix" } else { '*.msix' }
        $latest = Get-ChildItem $OutDir -Filter $filter -ErrorAction SilentlyContinue |
                  Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $latest) { Write-Error "$tag No .msix found in $OutDir." }
        $MsixPath = $latest.FullName
        Write-Host "$tag Using most recent package: $MsixPath"
    }
    if (-not (Test-Path $MsixPath)) { Write-Error "$tag MSIX not found: $MsixPath" }

    # ── Trust certificate ─────────────────────────────────────────────────────
    if ($CertPath) {
        if (-not (Test-Path $CertPath)) { Write-Error "$tag Certificate not found: $CertPath" }
        Write-Host "$tag Importing certificate into Cert:\LocalMachine\Root..."
        Import-Certificate -FilePath $CertPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        Write-Host "$tag Certificate trusted."
    }

    # ── Auto-sign unsigned package ────────────────────────────────────────────
    $sig = Get-AuthenticodeSignature -FilePath $MsixPath -ErrorAction SilentlyContinue
    if (-not ($sig -and $sig.Status -eq 'Valid')) {
        Write-Host "$tag Package is unsigned — auto-signing with self-signed dev certificate."
        $signTool = Find-WinSdkTool 'signtool.exe'
        if (-not $signTool) {
            Write-Error "$tag signtool.exe not found. Build with -DevCert or install Windows SDK."
        }
        $devCert = Get-ChildItem Cert:\CurrentUser\My |
            Where-Object { $_.Subject -eq $Publisher -and $_.NotAfter -gt (Get-Date) } |
            Sort-Object NotAfter -Descending | Select-Object -First 1
        if (-not $devCert) {
            Write-Host "$tag Creating self-signed dev certificate for '$Publisher'..."
            $devCert = New-SelfSignedCertificate `
                -Type Custom -Subject $Publisher `
                -KeyUsage DigitalSignature `
                -FriendlyName "$PackageName Dev Certificate" `
                -CertStoreLocation 'Cert:\CurrentUser\My' `
                -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
            Write-Host "$tag Created: $($devCert.Thumbprint)"
        } else {
            Write-Host "$tag Reusing: $($devCert.Thumbprint)"
        }
        $already = Get-ChildItem Cert:\LocalMachine\Root |
            Where-Object { $_.Thumbprint -eq $devCert.Thumbprint } | Select-Object -First 1
        if (-not $already) {
            Write-Host "$tag Trusting certificate in Cert:\LocalMachine\Root..."
            $store = New-Object System.Security.Cryptography.X509Certificates.X509Store(
                [System.Security.Cryptography.X509Certificates.StoreName]::Root,
                [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($devCert)
            $store.Close()
            Write-Host "$tag Certificate trusted."
        }
        Export-Certificate -Cert $devCert -FilePath (Join-Path (Split-Path $MsixPath) "$PackageName-dev.cer") -Type CERT | Out-Null
        Write-Host "$tag Signing $MsixPath ..."
        & $signTool sign /sha1 $devCert.Thumbprint /fd SHA256 /tr http://timestamp.digicert.com /td sha256 "$MsixPath"
        if ($LASTEXITCODE -ne 0) { Write-Error "$tag signtool failed (exit $LASTEXITCODE)." }
        Write-Host "$tag Package signed."
    }

    # ── Stop service + close desktop app before touching the package ──────────
    if ($ServiceName) {
        $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -ne 'Stopped') {
            Write-Host "$tag Stopping '$ServiceName'..."
            Stop-Service $ServiceName -Force
            Write-Host "$tag Service stopped."
        }
    }
    if ($DesktopProcessName) {
        $procs = Get-Process -Name $DesktopProcessName -ErrorAction SilentlyContinue
        if ($procs) {
            Write-Host "$tag Closing $DesktopProcessName processes..."
            $procs | Stop-Process -Force
            Start-Sleep -Milliseconds 500
            Write-Host "$tag Process closed."
        }
    }

    # ── Remove existing package then fresh install ────────────────────────────
    # Using Remove + Add instead of -ForceUpdateFromAnyVersion avoids 0x80073CFB
    # when reinstalling a package with identical version but different contents.
    $existing = if ($PackageName) { Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue } else { $null }
    if ($existing) {
        Write-Host "$tag Removing existing package ($($existing.Version))..."
        Remove-AppxPackage -Package $existing.PackageFullName
        Write-Host "$tag Existing package removed."
    }

    Write-Host "$tag Installing package..."
    Add-AppxPackage -Path $MsixPath
    Write-Host "$tag Package installed."

    # ── Wait for service to register in SCM ───────────────────────────────────
    $svc = $null
    if ($ServiceName) {
        Write-Host "$tag Waiting for '$ServiceName' to register in SCM..."
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while ((Get-Date) -lt $deadline) {
            $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
            if ($svc) { break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $svc) {
            Write-Warning "$tag Timed out after ${TimeoutSeconds}s. Try: Start-Service $ServiceName"
        } else {
            Write-Host "$tag Service registered (status: $($svc.Status))."
            if ($svc.Status -eq 'Running') {
                Write-Host "$tag Service is already running."
            } else {
                Write-Host "$tag Starting '$ServiceName'..."
                Start-Service $ServiceName
                Start-Sleep -Seconds 2
                $svc = Get-Service $ServiceName
                Write-Host "$tag Service status: $($svc.Status)"
                if ($svc.Status -ne 'Running') {
                    Write-Warning "$tag Service did not reach Running state. Check Event Viewer."
                }
            }
        }
    }

    # ── Summary ───────────────────────────────────────────────────────────────
    $pkg = if ($PackageName) { Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue } else { $null }
    Write-Host ''
    Write-Host "── $PackageName installed ───────────────────────────────────────────────"
    if ($pkg)  { Write-Host "  Package : $($pkg.PackageFullName)"; Write-Host "  Version : $($pkg.Version)" }
    if ($svc)  {
        Write-Host "  Service : $ServiceName  [$($svc.Status)]  (StartType: Automatic)"
        Write-Host ''
        Write-Host '  Service management:'
        Write-Host "    Stop-Service    $ServiceName"
        Write-Host "    Start-Service   $ServiceName"
        Write-Host "    Restart-Service $ServiceName"
    }
    Write-Host ''
    Write-Host '  Uninstall:'
    Write-Host "    Uninstall-MsixPackage -PackageName $PackageName$(if ($ServiceName) { " -ServiceName $ServiceName" })"
    Write-Host '────────────────────────────────────────────────────────────────────────'
}

function Uninstall-MsixPackage {
    <#
    .SYNOPSIS
        Stop the associated Windows service and remove the MSIX package.

    .PARAMETER PackageName
        AppxPackage identity name. Read from msix.yml if omitted.

    .PARAMETER ServiceName
        Windows service to stop before removal. Read from msix.yml if omitted.

    .PARAMETER ConfigPath
        Path to msix.yml for reading defaults.
    #>
    [CmdletBinding()]
    param(
        [string] $PackageName = '',
        [string] $ServiceName = '',
        [string] $ConfigPath = ''
    )
    $ErrorActionPreference = 'Stop'
    $tag = '[Uninstall-MsixPackage]'

    if (-not $ConfigPath -and -not $PackageName) {
        $auto = Join-Path (Get-Location).Path 'msix.yml'
        if (Test-Path $auto) { $ConfigPath = $auto }
    }
    if ($ConfigPath -and (Test-Path $ConfigPath)) {
        $cfg = Read-MsixConfig -Path $ConfigPath
        if (-not $PackageName) { $PackageName = Get-CfgValue (Get-CfgValue $cfg 'package') 'name'        '' }
        if (-not $ServiceName) { $ServiceName = Get-CfgValue (Get-CfgValue $cfg 'service') 'serviceName' '' }
    }
    if (-not $PackageName) { Write-Error "$tag -PackageName required." }

    if ($ServiceName) {
        $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -ne 'Stopped') {
            Write-Host "$tag Stopping '$ServiceName'..."
            Stop-Service $ServiceName -Force
            Write-Host "$tag Service stopped."
        }
    }
    $pkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
    if ($pkg) {
        Write-Host "$tag Removing $($pkg.PackageFullName)..."
        Remove-AppxPackage -Package $pkg.PackageFullName
        Write-Host "$tag Package removed."
    } else {
        Write-Host "$tag No installed '$PackageName' package found."
    }
}

Export-ModuleMember -Function Read-MsixConfig, New-MsixPackage, Install-MsixPackage, Uninstall-MsixPackage
