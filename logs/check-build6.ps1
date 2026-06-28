$buildLog = dotnet build src/McpServerManager.UI.Core/McpServerManager.UI.Core.csproj -c Debug --no-restore -v minimal 2>&1
$errs = $buildLog | Select-String -Pattern "error CS" 
Write-Host "CS errors: $($errs.Count)"
$errs | Select-Object -First 10
if ($errs.Count -eq 0) { Write-Host "BUILD SUCCEEDED for UI.Core" }
$buildLog | Select-String -Pattern "Build (succeeded|FAILED)" | Select -Last 1
