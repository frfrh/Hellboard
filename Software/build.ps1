# SPDX-License-Identifier: GPL-3.0-or-later
#
# Builds and tests Hellboard Companion.
# The Linux guard additionally builds the platform-neutral projects for linux-x64, so that
# Windows-only code cannot slip into Core/App unnoticed (implementation plan §3.1).
#
# Usage: .\build.ps1 [-Configuration Release] [-SkipLinuxGuard]

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipLinuxGuard
)

$ErrorActionPreference = 'Stop'

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

Push-Location $PSScriptRoot
try {
    Invoke-Dotnet build HellboardCompanion.slnx --configuration $Configuration
    Invoke-Dotnet test --solution HellboardCompanion.slnx --configuration $Configuration --no-build

    if (-not $SkipLinuxGuard) {
        $portableProjects = @(
            'src/HellboardCompanion.Core/HellboardCompanion.Core.csproj',
            'src/HellboardCompanion.App/HellboardCompanion.App.csproj',
            'tests/HellboardCompanion.Core.Tests/HellboardCompanion.Core.Tests.csproj'
        )
        foreach ($project in $portableProjects) {
            # Separate artifacts path, so the RID-specific restore does not disturb the regular build.
            Invoke-Dotnet build $project --configuration $Configuration --runtime linux-x64 --artifacts-path artifacts/linux-guard
        }
    }

    Write-Host 'Build succeeded.' -ForegroundColor Green
}
finally {
    Pop-Location
}
