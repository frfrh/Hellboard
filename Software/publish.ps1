# SPDX-License-Identifier: GPL-3.0-or-later
#
# Builds the release packages: one self-contained HellboardCompanion.exe plus license, third-party
# notices and readme, zipped to publish/HellboardCompanion-<version>-win-x64.zip, and the installer
# publish/HellboardCompanion-<version>-setup.exe (Inno Setup 7, installer/HellboardCompanion.iss).
#
# Usage: .\publish.ps1 [-SkipTests] [-SkipInstaller]

param(
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'

# ISCC.exe of Inno Setup 7 (or 6): PATH or the default installation folders.
function Find-InnoSetup {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }
    foreach ($folder in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
        foreach ($name in @('Inno Setup 7', 'Inno Setup 6')) {
            if ($folder -and (Test-Path (Join-Path $folder "$name\ISCC.exe"))) {
                return Join-Path $folder "$name\ISCC.exe"
            }
        }
    }
    return $null
}

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$hostProject = 'src/HellboardCompanion.Desktop.Windows/HellboardCompanion.Desktop.Windows.csproj'

Push-Location $PSScriptRoot
try {
    if (-not $SkipTests) {
        Invoke-Dotnet build HellboardCompanion.slnx --configuration Release
        Invoke-Dotnet test --solution HellboardCompanion.slnx --configuration Release --no-build
    }

    $publishDir = Join-Path $PSScriptRoot 'publish/win-x64'
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Invoke-Dotnet publish $hostProject -p:PublishProfile=win-x64

    $version = (& dotnet msbuild $hostProject -getProperty:Version).Trim()
    $packageDir = Join-Path $PSScriptRoot "publish/HellboardCompanion-$version-win-x64"
    $zipPath = "$packageDir.zip"
    foreach ($path in @($packageDir, $zipPath)) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force
        }
    }

    # Only what testers need: the EXE has its own symbols embedded, native .pdb files are left out.
    New-Item -ItemType Directory -Path $packageDir | Out-Null
    Copy-Item (Join-Path $publishDir 'HellboardCompanion.exe') $packageDir
    Copy-Item LICENSE (Join-Path $packageDir 'LICENSE.txt')
    Copy-Item THIRD-PARTY-NOTICES.md $packageDir
    Copy-Item BETA-README.md (Join-Path $packageDir 'README.md')

    Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zipPath
    $size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "Release package: $zipPath ($size MB)"

    if (-not $SkipInstaller) {
        $iscc = Find-InnoSetup
        if (-not $iscc) {
            Write-Warning 'Inno Setup not found - installer skipped (https://jrsoftware.org/isdl.php, or use -SkipInstaller).'
        }
        else {
            & $iscc /Q "/DAppVersion=$version" "/DSourceDir=$packageDir" "/DOutputDir=$(Join-Path $PSScriptRoot 'publish')" (Join-Path $PSScriptRoot 'installer/HellboardCompanion.iss')
            if ($LASTEXITCODE -ne 0) {
                throw "Inno Setup failed with exit code $LASTEXITCODE"
            }
            $setup = Join-Path $PSScriptRoot "publish/HellboardCompanion-$version-setup.exe"
            Write-Host "Installer: $setup ($([math]::Round((Get-Item $setup).Length / 1MB, 1)) MB)"
        }
    }
}
finally {
    Pop-Location
}
