@{
    ModuleVersion     = '1.0.0'
    GUID              = '42C84315-80A0-46F0-990F-F8A90EDAAB07'
    Author            = 'Remote Agent Contributors'
    Description       = 'Build and install MSIX packages from .NET projects. Reads project and build configuration from msix.yml in the workspace root.'
    PowerShellVersion = '7.0'
    RootModule        = 'MsixTools.psm1'
    FunctionsToExport = @('Read-MsixConfig', 'New-MsixPackage', 'Install-MsixPackage', 'Uninstall-MsixPackage')
    CmdletsToExport   = @()
    VariablesToExport = @()
    AliasesToExport   = @()
    PrivateData       = @{
        PSData = @{
            Tags       = @('msix', 'dotnet', 'windows', 'packaging', 'installer')
            ProjectUri = 'https://github.com/sharpninja/remote-agent'
        }
    }
}
