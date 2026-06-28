$buildLog = dotnet build src/McpServerManager.UI.Core/McpServerManager.UI.Core.csproj -c Debug --no-restore -v minimal 2>&1
$errs = $buildLog | Select-String -Pattern "error CS" 
$errCount = $errs.Count
Write-Host "Remaining CS errors after dedup: $errCount"
$errs | Select -First 30
if ($errCount -gt 0) {
  $buildLog | Out-File -Encoding utf8 logs/build-remediation-errors2.txt
}
