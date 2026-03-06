#!/usr/bin/env node
/**
 * MsixTools MCP Server (stdio)
 *
 * Exposes MsixTools PowerShell module commands as MCP tools so AI agents can
 * build, install, and query MSIX packages without knowing PowerShell syntax.
 *
 * Prerequisites: pwsh (PowerShell 7+) on PATH, MsixTools module reachable from
 * the workspace root (scripts/MsixTools submodule) or a sibling MsixTools/ dir.
 */

import { existsSync } from 'fs';
import { resolve } from 'path';
import { fileURLToPath } from 'url';
import { spawn } from 'child_process';
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  type Tool,
} from '@modelcontextprotocol/sdk/types.js';

// ── Paths ─────────────────────────────────────────────────────────────────────

const __dirname = fileURLToPath(new URL('.', import.meta.url));

/** Locate MsixTools.psd1 relative to the target workspace root. */
function resolveModule(workspaceRoot: string): string {
  const candidates = [
    resolve(workspaceRoot, 'scripts', 'MsixTools', 'MsixTools.psd1'),
    resolve(workspaceRoot, '..', 'MsixTools', 'MsixTools.psd1'),
    resolve(__dirname, '..', '..', 'MsixTools.psd1'),   // installed as submodule/sibling
    resolve(__dirname, '..', 'MsixTools.psd1'),
  ];
  for (const c of candidates) {
    if (existsSync(c)) return c;
  }
  return candidates[0]; // let pwsh surface a clear "not found" error
}

// ── PowerShell runner ─────────────────────────────────────────────────────────

interface PwshResult { stdout: string; stderr: string; exitCode: number }

function runPwsh(script: string): Promise<PwshResult> {
  return new Promise((res) => {
    const proc = spawn('pwsh', ['-NoProfile', '-NonInteractive', '-Command', script], {
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    let stdout = '', stderr = '';
    proc.stdout.on('data', (d: Buffer) => { stdout += d.toString(); });
    proc.stderr.on('data', (d: Buffer) => { stderr += d.toString(); });
    proc.on('close', (code) => res({ stdout, stderr, exitCode: code ?? 1 }));
  });
}

/** Escape a string for single-quoted PowerShell literals. */
const psEsc = (s: string) => s.replace(/'/g, "''");

/** Build an Import-Module + cmdlet invocation script. */
function buildScript(
  modulePath: string,
  fn: string,
  params: Record<string, unknown>,
): string {
  const args = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '' && v !== false)
    .map(([k, v]) => v === true ? `-${k}` : `-${k} '${psEsc(String(v))}'`)
    .join(' ');

  return [
    `$ErrorActionPreference = 'Stop'`,
    `Import-Module '${psEsc(modulePath)}' -Force`,
    `${fn} ${args}`,
  ].join('; ');
}

// ── Tool schemas ──────────────────────────────────────────────────────────────

const TOOLS: Tool[] = [
  {
    name: 'read_msix_config',
    description:
      'Read and return the parsed msix.yml configuration as JSON. ' +
      'Use this to inspect what the module will build before invoking new_msix_package.',
    inputSchema: {
      type: 'object',
      properties: {
        workspace_root: { type: 'string', description: 'Absolute path to the repository root containing msix.yml.' },
        config_path:    { type: 'string', description: 'Explicit path to the YAML config. Defaults to <workspace_root>/msix.yml.' },
      },
      required: ['workspace_root'],
    },
  },
  {
    name: 'new_msix_package',
    description:
      'Build a combined MSIX package (.NET service + Avalonia desktop app) using the MsixTools module. ' +
      'Publishes projects, assembles the MSIX layout, signs it, and optionally installs it. ' +
      'Requires Windows with Windows SDK (makeappx.exe, signtool.exe) and .NET SDK on PATH.',
    inputSchema: {
      type: 'object',
      properties: {
        workspace_root:  { type: 'string',  description: 'Absolute path to the repository root.' },
        config_path:     { type: 'string',  description: 'Path to msix.yml. Defaults to <workspace_root>/msix.yml.' },
        configuration:   { type: 'string',  enum: ['Release', 'Debug'], description: 'Build configuration. Default: Release.' },
        version:         { type: 'string',  description: 'Package version override (e.g. 1.2.3).' },
        publisher:       { type: 'string',  description: 'MSIX Identity/Publisher string.' },
        cert_thumbprint: { type: 'string',  description: 'SHA1 thumbprint of signing cert in CurrentUser\\My.' },
        dev_cert:        { type: 'boolean', description: 'Create/reuse a self-signed dev cert and sign with it.' },
        self_contained:  { type: 'boolean', description: 'Publish self-contained (bundles .NET runtime). Default: true.' },
        clean:           { type: 'boolean', description: 'Delete bin/ and obj/ before publishing. Mutually exclusive with no_build.' },
        no_build:        { type: 'boolean', description: 'Skip dotnet publish; repackage existing publish output. Mutually exclusive with clean.' },
        bump_major:      { type: 'boolean', description: 'Increment major in GitVersion.yml before building.' },
        bump_minor:      { type: 'boolean', description: 'Increment minor in GitVersion.yml before building.' },
        bump_patch:      { type: 'boolean', description: 'Increment patch in GitVersion.yml before building.' },
        force:           { type: 'boolean', description: 'Skip the AppxManifest review pause.' },
        install:         { type: 'boolean', description: 'Install the MSIX after packaging. Requires Administrator.' },
        out_dir:         { type: 'string',  description: 'Output directory. Default: <workspace_root>/artifacts.' },
        exclude_service: { type: 'boolean', description: 'Omit the gRPC service from the package.' },
        exclude_desktop: { type: 'boolean', description: 'Omit the Avalonia desktop app from the package.' },
      },
      required: ['workspace_root'],
    },
  },
  {
    name: 'install_msix_package',
    description:
      'Install an MSIX built by MsixTools and optionally start the Windows service. ' +
      'Removes any existing installation of the same package first to avoid version conflicts. ' +
      'Requires Administrator.',
    inputSchema: {
      type: 'object',
      properties: {
        workspace_root:  { type: 'string',  description: 'Absolute path to the repository root.' },
        config_path:     { type: 'string',  description: 'Path to msix.yml.' },
        msix_path:       { type: 'string',  description: 'Explicit path to the .msix file. Overrides config-derived path.' },
        start_service:   { type: 'boolean', description: 'Start the Windows service after installing. Default: true.' },
        cert_thumbprint: { type: 'string',  description: 'SHA1 thumbprint of the signing certificate to trust.' },
        dev_cert:        { type: 'boolean', description: 'Trust the self-signed dev certificate before installing.' },
        force:           { type: 'boolean', description: 'Remove existing installation before reinstalling.' },
      },
      required: ['workspace_root'],
    },
  },
  {
    name: 'uninstall_msix_package',
    description:
      'Stop the Windows service and remove the MSIX package. Requires Administrator.',
    inputSchema: {
      type: 'object',
      properties: {
        workspace_root: { type: 'string',  description: 'Absolute path to the repository root.' },
        config_path:    { type: 'string',  description: 'Path to msix.yml.' },
        stop_service:   { type: 'boolean', description: 'Stop the Windows service before uninstalling. Default: true.' },
      },
      required: ['workspace_root'],
    },
  },
  {
    name: 'get_msix_version',
    description:
      'Resolve the current package version from GitVersion.yml or git tags, ' +
      'returning both the semver string and the 4-part MSIX version.',
    inputSchema: {
      type: 'object',
      properties: {
        workspace_root: { type: 'string', description: 'Absolute path to the repository root.' },
      },
      required: ['workspace_root'],
    },
  },
];

// ── Tool handler ──────────────────────────────────────────────────────────────

async function handleTool(name: string, args: Record<string, unknown>) {
  const workspaceRoot = String(args['workspace_root'] ?? '');
  const configPath    = String(args['config_path'] ?? resolve(workspaceRoot, 'msix.yml'));
  const modulePath    = resolveModule(workspaceRoot);

  let script: string;

  switch (name) {
    case 'read_msix_config': {
      script = buildScript(modulePath, 'Read-MsixConfig', { ConfigPath: configPath });
      script += ' | ConvertTo-Json -Depth 10';
      break;
    }

    case 'new_msix_package': {
      const p: Record<string, unknown> = {
        WorkspaceRoot: workspaceRoot,
        ConfigPath:    configPath,
        Configuration: args['configuration'] ?? 'Release',
      };
      if (args['version'])             p['Version']        = args['version'];
      if (args['publisher'])           p['Publisher']      = args['publisher'];
      if (args['cert_thumbprint'])     p['CertThumbprint'] = args['cert_thumbprint'];
      if (args['dev_cert'])            p['DevCert']        = true;
      if (args['self_contained'] != null) p['SelfContained'] = args['self_contained'];
      if (args['clean'])               p['Clean']          = true;
      if (args['no_build'])            p['NoBuild']        = true;
      if (args['bump_major'])          p['BumpMajor']      = true;
      if (args['bump_minor'])          p['BumpMinor']      = true;
      if (args['bump_patch'])          p['BumpPatch']      = true;
      if (args['force'])               p['Force']          = true;
      if (args['install'])             p['Install']        = true;
      if (args['out_dir'])             p['OutDir']         = args['out_dir'];
      if (args['exclude_service'])     p['ExcludeService'] = true;
      if (args['exclude_desktop'])     p['ExcludeDesktop'] = true;
      script = buildScript(modulePath, 'New-MsixPackage', p);
      break;
    }

    case 'install_msix_package': {
      const p: Record<string, unknown> = { ConfigPath: configPath };
      if (args['msix_path'])           p['MsixPath']      = args['msix_path'];
      if (args['start_service'] != null) p['StartService'] = args['start_service'];
      if (args['cert_thumbprint'])     p['CertThumbprint'] = args['cert_thumbprint'];
      if (args['dev_cert'])            p['DevCert']        = true;
      if (args['force'])               p['Force']          = true;
      script = buildScript(modulePath, 'Install-MsixPackage', p);
      break;
    }

    case 'uninstall_msix_package': {
      const p: Record<string, unknown> = { ConfigPath: configPath };
      if (args['stop_service'] != null) p['StopService']  = args['stop_service'];
      script = buildScript(modulePath, 'Uninstall-MsixPackage', p);
      break;
    }

    case 'get_msix_version': {
      const ws  = psEsc(workspaceRoot);
      const mod = psEsc(modulePath);
      script = [
        `$ErrorActionPreference = 'Stop'`,
        `Import-Module '${mod}' -Force`,
        `$ver = ''`,
        `$gv = Join-Path '${ws}' 'GitVersion.yml'`,
        `if (Test-Path $gv) {`,
        `  $m = (Get-Content $gv | Select-String '^\\s*next-version:\\s*(.+)').Matches`,
        `  if ($m.Count -gt 0) { $ver = $m[0].Groups[1].Value.Trim() }`,
        `}`,
        `if (-not $ver) {`,
        `  try { $ver = (git -C '${ws}' describe --tags --abbrev=0 2>$null) -replace '^v','' } catch {}`,
        `}`,
        `if (-not $ver) { $ver = '0.1.0' }`,
        `$msix = ($ver -replace '-.*','') + '.0'`,
        `[PSCustomObject]@{ Version = $ver; MsixVersion = $msix } | ConvertTo-Json`,
      ].join('; ');
      break;
    }

    default:
      return { content: [{ type: 'text' as const, text: `Unknown tool: ${name}` }], isError: true };
  }

  const { stdout, stderr, exitCode } = await runPwsh(script);
  const text = [stdout.trim(), stderr.trim()].filter(Boolean).join('\n\n') || '(no output)';
  return {
    content: [{ type: 'text' as const, text }],
    isError: exitCode !== 0,
  };
}

// ── Server bootstrap ──────────────────────────────────────────────────────────

const server = new Server(
  { name: 'msixtools', version: '1.0.0' },
  { capabilities: { tools: {} } },
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: TOOLS }));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const { name, arguments: args } = req.params;
  return handleTool(name, (args ?? {}) as Record<string, unknown>);
});

const transport = new StdioServerTransport();
await server.connect(transport);
