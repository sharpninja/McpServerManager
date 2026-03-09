const path = require('path');
const { spawn } = require('child_process');

const workspace = process.argv[2];
const extPath = process.argv[3];
if (!workspace || !extPath) {
  console.error('Usage: node launch-vscode-extension.js <workspaceFolder> <extensionDevelopmentPath>');
  process.exit(1);
}

const repoRoot = path.resolve(__dirname, '..');
const codeExe = process.env.VSCODE_PATH ||
  path.join(process.env.LOCALAPPDATA || '', 'Programs', 'Microsoft VS Code', 'Code.exe');

const args = [
  'run',
  '--project',
  path.join(repoRoot, 'build', 'Build.csproj'),
  '--',
  '--root',
  repoRoot,
  '--target',
  'LaunchVsCodeExtension',
  '--workspace-folder',
  workspace,
  '--extension-development-path',
  extPath,
];

if (codeExe) {
  args.push('--vs-code-path', codeExe);
}

const child = spawn('dotnet', args, {
  stdio: 'inherit',
  detached: true,
  shell: false,
});
child.unref();
process.exit(0);
