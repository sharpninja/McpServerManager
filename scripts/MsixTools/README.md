# MsixTools

A reusable PowerShell module (PS 7.0+) for building and installing MSIX packages from .NET projects.

Configuration is driven by an `msix.yml` file in your workspace root — no hard-coded paths.

## Requirements

- PowerShell 7.0+
- Windows SDK ≥ 10.0.19041 (`makeappx.exe`, `signtool.exe`)
- .NET SDK (version matching your project's target framework)
- [`powershell-yaml`](https://www.powershellgallery.com/packages/powershell-yaml) module (auto-installed on first use)

## Installation

```powershell
# Clone alongside your project
git clone https://github.com/sharpninja/MsixTools.git

# Or add as a git submodule
git submodule add https://github.com/sharpninja/MsixTools.git scripts/MsixTools
```

## Quick start

**1. Add `msix.yml` to your workspace root:**

```yaml
package:
  name: MyApp
  displayName: My App
  publisher: CN=MyApp Dev

service:
  path: src/MyApp.Service/MyApp.Service.csproj
  framework: net10.0
  serviceName: MyAppService
  subDir: service
  startAccount: localSystem
  startupType: auto

desktop:
  path: src/MyApp.Desktop/MyApp.Desktop.csproj
  framework: net9.0
  subDir: desktop
  displayName: My App Desktop
  appId: MyAppDesktop
  processName: MyApp.Desktop

plugins:
  - path: src/MyApp.Plugin/MyApp.Plugin.csproj
    framework: net10.0
    destSubDir: service/plugins

build:
  configuration: Release
  rid: win-x64
  selfContained: true

output:
  dir: artifacts

icons:
  svg: src/MyApp.Desktop/Assets/appicon.svg
```

**2. Import and call:**

```powershell
Import-Module .\MsixTools\MsixTools.psd1

# Build, sign with dev cert, skip review prompt
New-MsixPackage -WorkspaceRoot . -DevCert -Force

# Install (requires Administrator)
Install-MsixPackage

# Uninstall
Uninstall-MsixPackage
```

## Exported functions

| Function | Description |
|---|---|
| `Read-MsixConfig` | Parse `msix.yml` → hashtable |
| `New-MsixPackage` | Publish .NET projects, assemble layout, run `makeappx`, sign |
| `Install-MsixPackage` | Stop service/process → remove old install → `Add-AppxPackage` → start service |
| `Uninstall-MsixPackage` | Stop service → `Remove-AppxPackage` |

```powershell
Get-Help New-MsixPackage -Full
Get-Help Install-MsixPackage -Full
```

## Key parameters — `New-MsixPackage`

| Parameter | Default | Description |
|---|---|---|
| `-WorkspaceRoot` | `.` | Workspace root (resolved to absolute) |
| `-ConfigPath` | `msix.yml` | YAML config path |
| `-Version` | auto-detected | SemVer; tries dotnet-gitversion → GitVersion.yml → git tag |
| `-DevCert` | — | Create/reuse self-signed dev cert and sign |
| `-CertThumbprint` | — | Sign with existing cert from `Cert:\CurrentUser\My` |
| `-SelfContained` | `true` | Single-file self-contained publish |
| `-Clean` | — | Delete `bin/` and `obj/` before publishing |
| `-NoBuild` | — | Skip publish, use existing `artifacts/publish-*` output |
| `-Force` | — | Skip AppxManifest review pause |
| `-Install` | — | Call `Install-MsixPackage` after packaging |
| `-ExcludeService` | — | Skip service project |
| `-ExcludeDesktop` | — | Skip desktop project |

> `-Clean` and `-NoBuild` are mutually exclusive.

## Version detection order

1. `dotnet tool run dotnet-gitversion`
2. `next-version` in `GitVersion.yml`
3. Most recent `git describe --tags`
4. Fallback: `0.1.0`

## Signing

| Scenario | Flags |
|---|---|
| Dev/CI self-signed | `-DevCert` |
| Existing cert in store | `-CertThumbprint <sha1>` |
| Unsigned (install with `-AllowUnsigned`) | neither |

`Install-MsixPackage` auto-signs unsigned packages with a self-signed dev cert before calling `Add-AppxPackage`.

## Icon generation

Icons are sourced in priority order:
1. Directory of pre-sized PNGs (`-IconSourceDir`)
2. SVG rasterized via Inkscape or ImageMagick (`icons.svg` in `msix.yml`)
3. Generated placeholder PNGs (blue with initials)

## License

MIT

---

## MCP Server

The `mcp-server/` directory contains a **stdio MCP server** that exposes all MsixTools commands as tools for AI agents (GitHub Copilot, Claude, etc.).

### Prerequisites

| Requirement | Notes |
|---|---|
| Node.js ≥ 18 | For running the server |
| PowerShell 7+ | `pwsh` must be on PATH |
| Windows SDK | `makeappx.exe` / `signtool.exe` (for `new_msix_package`) |
| .NET SDK | 9 + 10 (for `new_msix_package`) |

### Build

```powershell
cd mcp-server
npm install
npm run build
```

### Configure your MCP client

Add to your MCP client config (e.g. `.github/copilot/mcp.json` or `~/.config/claude/mcp.json`):

```json
{
  "mcpServers": {
    "msixtools": {
      "type": "stdio",
      "command": "node",
      "args": ["E:/github/MsixTools/mcp-server/dist/index.js"]
    }
  }
}
```

Or if installed globally via npm link:

```json
{
  "mcpServers": {
    "msixtools": {
      "type": "stdio",
      "command": "msixtools-mcp"
    }
  }
}
```

### Available tools

| Tool | Description |
|---|---|
| `read_msix_config` | Parse and return `msix.yml` as JSON |
| `get_msix_version` | Resolve current semver + 4-part MSIX version from `GitVersion.yml` / git tags |
| `new_msix_package` | Full build → sign → (optionally install) pipeline |
| `install_msix_package` | Install an existing `.msix` and start the service |
| `uninstall_msix_package` | Stop the service and remove the package |

### Example agent prompt

```
Use the msixtools MCP server to bump the patch version and build a new
MSIX package for workspace_root E:/github/remote-agent, using a dev cert,
with force=true.
```
