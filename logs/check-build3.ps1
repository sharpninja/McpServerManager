$buildLog = dotnet build src/McpServerManager.UI.Core/McpServerManager.UI.Core.csproj -c Debug --no-restore -v minimal 2>&1
$errs = $buildLog | Select-String -Pattern "error CS" 
$errCount = $errs.Count
Write-Host "CS errors: $errCount"
$errs | Select -First 15
