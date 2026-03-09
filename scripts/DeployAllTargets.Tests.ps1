BeforeAll {
    Import-Module (Join-Path $PSScriptRoot 'DeployAllTargets.psm1') -Force
}

Describe 'DeployAllTargets compatibility module' {
    BeforeEach {
        Mock Invoke-NukeBuild {
            [pscustomobject]@{ ExitCode = 0; Output = @() }
        } -ModuleName DeployAllTargets
    }

    It 'supports independent Director deployment with WhatIf' {
        $result = Invoke-DeployDirectorTool -Configuration Debug -PackageVersion '1.2.3' -WhatIf

        $result.Target | Should -Be 'Director'
        $result.Status | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-NukeBuild -ModuleName DeployAllTargets -Times 1 -ParameterFilter {
            $Arguments -contains '--deploy-selection' -and
            $Arguments -contains 'Director' -and
            $Arguments -contains '--configuration' -and
            $Arguments -contains 'Debug' -and
            $Arguments -contains '--package-version' -and
            $Arguments -contains '1.2.3' -and
            $Arguments -contains '--what-if'
        }
    }

    It 'supports independent Android emulator deployment with WhatIf' {
        $result = Invoke-DeployAndroidEmulator -Configuration Debug -DeviceSerial 'emulator-5554' -WhatIf

        $result.Target | Should -Be 'AndroidEmulator'
        $result.Status | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-NukeBuild -ModuleName DeployAllTargets -Times 1 -ParameterFilter {
            $Arguments -contains '--deploy-selection' -and
            $Arguments -contains 'AndroidEmulator' -and
            $Arguments -contains '--android-emulator-serial' -and
            $Arguments -contains 'emulator-5554' -and
            $Arguments -contains '--what-if'
        }
    }

    It 'orchestrates requested targets in order with WhatIf' {
        $results = Invoke-DeployTargetSet -Targets @('Director', 'WebUi') -Configuration Debug -PackageVersion '1.2.3' -WhatIf

        $results.Target | Should -Be @('Director', 'WebUi')
        $results.Status | Should -Be @('WhatIf', 'WhatIf')
        Assert-MockCalled Invoke-NukeBuild -ModuleName DeployAllTargets -Times 2
    }
}
