# Assert-VmSurfaceThin.ps1 - non-empty gate per strategist rec for PLAN-VM-CQRS-REMEDIATION-001 skeptic fix.
# Usage: . scripts/Assert-VmSurfaceThin.ps1 ; Assert-VmSurfaceThin -VmFile '...' -EntryMethod 'SendAsync'
param([Parameter(Mandatory)][string]$VmFile, [Parameter(Mandatory)][string]$EntryMethod)
$ErrorActionPreference = 'Stop'
Write-Host "[ASSERT SURFACE] $VmFile :: $EntryMethod"
if (-not (Test-Path $VmFile)) { Write-Error "VM file not found"; exit 1 }
$src = Get-Content $VmFile -Raw
# Use line-based + brace counting to capture ONLY the direct method body (robust to later code containing forbidden words)
$lines = Get-Content $VmFile
$startIdx = -1
for ($i=0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match "Task\s+$EntryMethod\s*\(") { $startIdx = $i; break }
}
if ($startIdx -lt 0) { Write-Error "Entry $EntryMethod not found"; exit 1 }
$bodyLines = @()
$brace = 0
$started = $false
for ($i=$startIdx; $i -lt $lines.Count; $i++) {
  $line = $lines[$i]
  if (-not $started) {
    if ($line -match '\{') { $started = $true; $brace = 1; continue }
    continue
  }
  $open = ($line | Select-String -Pattern '\{' -AllMatches).Matches.Count
  $close = ($line | Select-String -Pattern '\}' -AllMatches).Matches.Count
  $brace += $open - $close
  if ($brace -le 0) { break }
  $bodyLines += $line
}
$body = ($bodyLines -join "`n").Trim()
$forb = @('\bif\s*\(', '\btry\b', '\bcatch\b', '\.Trim\(', 'NotifySend', 'new \w+Service', '\bwhile\s*\(' , 'LoadWorkspacesCoreAsync', 'SendCurrentInputAsync', 'RunAgentEventListenerLoopAsync', 'UpdateExistingWorkspaceAsync')
foreach($f in $forb){ if($body -match $f){ Write-Error "[FAIL] contains $f"; exit 1 } }
$awaitCnt = ([regex]::Matches($body, 'await ')).Count
if($awaitCnt -gt 1){ Write-Error "[FAIL] >1 await"; exit 1 }
if($body -notmatch 'new \S*(Command|Query)|Build\w+Command'){ Write-Error "[FAIL] must contain new*Command/Query dispatch"; exit 1 }
# Apply optional for pure dispatch thin entries (apply lives in *Internal reached via handler)
if($body.Split([Environment]::NewLine).Count -gt 15){ Write-Error "[FAIL] body too long"; exit 1 }
Write-Host "[PASS] thin surface"
exit 0
