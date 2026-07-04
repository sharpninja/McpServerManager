$ErrorActionPreference = "Stop"
$scratch = "C:\Users\kingd\AppData\Local\Temp\grok-goal-392f738d92d8\implementer"
$out = Join-Path $scratch "e1_final_verif.log"
Remove-Item $out -Force -ErrorAction SilentlyContinue
$files = Get-ChildItem $scratch -Filter "probe-*.json" | % FullName
foreach ($f in $files) {
  $raw = Get-Content -Raw $f
  Add-LinesToFile -Path $out -Content ("=== " + (Split-Path $f -Leaf) + " ===`n" + $raw + "`n=== END ===`n")
}
Write-Host "ASSEMBLE_SIZE: $((Get-Item $out).Length)"
Write-Host "ASSEMBLE_OK"