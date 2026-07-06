# E1-Lib.ps1 - dot-sourced helpers for PLAN-VM-CQRS-REMEDIATION-001 E1 restructure
# Parse AGENTS once; no hardcodes
function Get-E1Agents {
  $agents = Get-Content -Raw "AGENTS-README-FIRST.yaml"
  $base = ([regex]::Match($agents, "baseUrl:\s*(\S+)")).Groups[1].Value.TrimEnd("/")
  $apiKey = ([regex]::Match($agents, "apiKey:\s*(\S+)")).Groups[1].Value
  return [pscustomobject]@{
    BaseUrl = $base
    ApiKey = $apiKey
    Headers = @{ "X-Api-Key" = $apiKey; "Content-Type" = "application/json" }
  }
}
function Get-E1ScratchPath {
  return "C:\Users\kingd\AppData\Local\Temp\grok-goal-392f738d92d8\implementer"
}
function Write-E1Probe {
  param([string]$Name, [string]$Content)
  $scratch = Get-E1ScratchPath
  if (-not (Test-Path $scratch)) { New-Item -ItemType Directory -Force -Path $scratch | Out-Null }
  $p = Join-Path $scratch $Name
  if (Test-Path $p) { Remove-Item $p -Force }
  Add-LinesToFile -Path $p -Content $Content
}
function Write-E1ChangedManifest {
  $scratch = Get-E1ScratchPath
  $porc = (git status --porcelain) -join "`n"
  $full = git status --porcelain -b 2>&1 | Out-String
  $man = [pscustomobject]@{
    capturedAt = (Get-Date).ToUniversalTime().ToString("o")
    lines = ($porc -split "`n" | Where-Object { $_ -and $_.Trim() })
    porcelain = $porc
  }
  $json = $man | ConvertTo-Json -Depth 5 -Compress
  Write-E1Probe "changed-manifest.json" $json
  $txtPath = Join-Path $scratch "FINAL-CHANGED_FILES.txt"
  if (Test-Path $txtPath) { Remove-Item $txtPath -Force }
  Add-LinesToFile -Path $txtPath -Content (($porc -split "`n" | Where-Object { $_ -and $_.Trim() }) -join "`n")
  return $txtPath
}
function Invoke-E1SessionTurnPost {
  param([object]$Entry, [string]$SessionId, [string]$SourceType = "GrokCode")
  $a = Get-E1Agents
  $uri = "$($a.BaseUrl)/mcpserver/sessionlog/$SourceType/$SessionId/turn"
  $body = @{
    requestId = $Entry.requestId
    queryTitle = $Entry.queryTitle
    queryText = $Entry.queryText
    response = $Entry.response
    status = $Entry.status
  } | ConvertTo-Json -Depth 5 -Compress
  Invoke-RestMethod -Uri $uri -Method Post -Headers $a.Headers -Body $body | Out-Null
  return $Entry
}
Export-ModuleMember -Function Get-E1Agents, Get-E1ScratchPath, Write-E1Probe, Write-E1ChangedManifest, Invoke-E1SessionTurnPost
