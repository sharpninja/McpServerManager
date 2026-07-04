$ErrorActionPreference = "Stop"
$vmData = "temp_vm_data.json"
# load as PS object
$vm = Get-Content $vmData -Raw | ConvertFrom-Json
Write-Host "VM loaded via ConvertFrom-Json (PS object)"
Write-Host ("Chat:" + $vm.Chat + " Connection:" + $vm.Connection)
# update to baseline 0
$vm.Chat = 0
$vm.Connection = 0
# serialize and write with Add-Lines $var1 compliant
Remove-Item $vmData -Force -ErrorAction SilentlyContinue
$j = $vm | ConvertTo-Json -Compress
$var1 = $j
Add-LinesToFile -Path $vmData -Content $var1
Write-Host "Rebuild of McpServerManager Management Interfaces Matrix complete. See docs/McpServerManager_Management_Interfaces_Matrix_rebuilt.xlsx"
# no WriteAllText/Set-Content