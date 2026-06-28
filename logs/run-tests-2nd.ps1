cd tests/McpServerManager.UI.Core.Tests
dotnet test -c Debug --no-build --logger "console;verbosity=minimal" 2>&1 | Tee-Object -Variable out | Select-String -Pattern "Test run|Passed!|Failed:" | Select -Last 3
Write-Host "2ND RUN COMPLETE"
