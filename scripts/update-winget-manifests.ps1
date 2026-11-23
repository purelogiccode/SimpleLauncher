#Requires -Version 7
<#
.SYNOPSIS
Generates the winget manifests for PureLogicCode.SimpleLauncher from the built setup executables.

.DESCRIPTION
winget installs the per-user Inno Setup installer
(SimpleLauncher-{version}-win-{x64,arm64}-setup.exe), not the portable ZIP. This script
reads the version from the canonical SimpleLauncher.csproj, hashes both installers that
scripts/package-release.ps1 -BuildInstaller produced, and writes the three manifest files
in the layout expected by microsoft/winget-pkgs:

  <OutputDir>\manifests\p\PureLogicCode\SimpleLauncher\<version>\
    PureLogicCode.SimpleLauncher.yaml
    PureLogicCode.SimpleLauncher.installer.yaml
    PureLogicCode.SimpleLauncher.locale.en-US.yaml

The installer URLs point at the GitHub release tag release<version>, so the assets must be
published before the manifests are submitted to winget-pkgs. Pass -VerifyUrls to check that
the two setup executables are reachable first.

The .NET 11 runtime dependency is included because the payload stays
framework-dependent. The installer can also download the runtime itself, but the winget
dependency installs it silently before Setup runs and avoids a UAC prompt during a winget
install.

.PARAMETER Version
Release version, e.g. 5.8.4. Defaults to <Version> in SimpleLauncher\SimpleLauncher.csproj.

.PARAMETER SetupDir
Folder that contains the setup executables. Defaults to artifacts\release.

.PARAMETER OutputDir
Folder that receives the manifests\ tree. Defaults to artifacts\winget.

.PARAMETER Repository
GitHub repository in owner/name form. Defaults to purelogiccode/SimpleLauncher.

.PARAMETER TagPrefix
Prefix of the GitHub release tag; the tag is <TagPrefix><version> (release5.8.4).
Defaults to release.

.PARAMETER VerifyUrls
Check that both installer URLs return a successful response before writing the manifests.

.PARAMETER SkipValidation
Do not run winget validate after writing the manifests.

.EXAMPLE
pwsh scripts/update-winget-manifests.ps1

.EXAMPLE
pwsh scripts/update-winget-manifests.ps1 -Version 5.8.4 -VerifyUrls

.EXAMPLE
pwsh scripts/update-winget-manifests.ps1 -SetupDir D:\drop -OutputDir D:\winget-submit
#>
[CmdletBinding()]
param(
    [string]$Version,

    [string]$SetupDir,

    [string]$OutputDir,

    [string]$Repository = 'purelogiccode/SimpleLauncher',

    [string]$TagPrefix = 'release',

    [switch]$VerifyUrls,

    [switch]$SkipValidation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoDir 'SimpleLauncher\SimpleLauncher.csproj'
$schemaVersion = '1.28.0'

if (-not $SetupDir) {
    $SetupDir = Join-Path $repoDir 'artifacts\release'
}
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoDir 'artifacts\winget'
}

if (-not $Version) {
    $xml = [xml](Get-Content -LiteralPath $projectPath -Raw)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $node) { throw "Could not read <Version> from $projectPath." }
    $Version = $node.InnerText.Trim()
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' is not in Major.Minor.Patch form (e.g. 5.8.4)."
}
if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository '$Repository' is not in owner/name form."
}

$SetupDir = [System.IO.Path]::GetFullPath($SetupDir)
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

$packageIdentifier = 'PureLogicCode.SimpleLauncher'
$tag = "$TagPrefix$Version"
$manifestDir = Join-Path $OutputDir "manifests\p\PureLogicCode\SimpleLauncher\$Version"

# Both setup executables are required: winget would otherwise only know about one
# architecture, and an ARM64 machine would fall back to the (blocked) x64 installer.
$installers = @(
    @{ Architecture = 'x64'; Rid = 'win-x64' },
    @{ Architecture = 'arm64'; Rid = 'win-arm64' }
)

$hashes = @{}
foreach ($installer in $installers) {
    $name = "SimpleLauncher-$Version-$($installer.Rid)-setup.exe"
    $path = Join-Path $SetupDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw ("Setup executable '$name' was not found in $SetupDir. Build the release installers " +
            "first: pwsh scripts/package-release.ps1 -Version $Version -BuildInstaller")
    }

    $hashes[$installer.Rid] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host ("{0}: {1}" -f $name, $hashes[$installer.Rid])
}

$installerUrl = {
    param([string]$Rid)
    "https://github.com/$Repository/releases/download/$tag/SimpleLauncher-$Version-$Rid-setup.exe"
}

if ($VerifyUrls) {
    foreach ($installer in $installers) {
        $url = & $installerUrl $installer.Rid
        try {
            $response = Invoke-WebRequest -Uri $url -Method Head -MaximumRedirection 10 -UseBasicParsing
            if ($response.StatusCode -ne 200) {
                throw "HTTP $($response.StatusCode)"
            }
        }
        catch {
            throw ("Installer URL is not reachable: $url$([Environment]::NewLine)" +
                "Publish the release assets before submitting to winget-pkgs. $($_.Exception.Message)")
        }
        Write-Host "Reachable: $url"
    }
}

$versionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$schemaVersion.schema.json

PackageIdentifier: $packageIdentifier
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $schemaVersion

"@

$installerEntries = foreach ($installer in $installers) {
    $url = & $installerUrl $installer.Rid
    $hash = $hashes[$installer.Rid]
    @"
- Architecture: $($installer.Architecture)
  InstallerUrl: $url
  InstallerSha256: $hash
  ProductCode: '{2016DA44-0694-42F7-92A3-4015CF9069A5}_is1'
  AppsAndFeaturesEntries:
  - DisplayName: Simple Launcher
    Publisher: PureLogicCode
    DisplayVersion: $Version
    ProductCode: '{2016DA44-0694-42F7-92A3-4015CF9069A5}_is1'

"@
}

$installerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$schemaVersion.schema.json

PackageIdentifier: $packageIdentifier
PackageVersion: $Version
MinimumOSVersion: 10.0.17763.0
InstallerType: inno
Scope: user
InstallerSwitches:
  Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
  SilentWithProgress: /SILENT /SUPPRESSMSGBOXES /NORESTART /SP-
  Interactive: /SP-
  InstallLocation: /DIR="<INSTALLPATH>"
  Log: /LOG="<LOGPATH>"
UpgradeBehavior: install
Dependencies:
  PackageDependencies:
  - PackageIdentifier: Microsoft.DotNet.Runtime.11
    MinimumVersion: 11.0.0
Installers:
$($installerEntries -join '')
ManifestType: installer
ManifestVersion: $schemaVersion

"@

$localeManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$schemaVersion.schema.json

PackageIdentifier: $packageIdentifier
PackageVersion: $Version
PackageLocale: en-US
Publisher: PureLogicCode
PublisherUrl: https://www.purelogiccode.com/
PublisherSupportUrl: https://github.com/$Repository/issues
PrivacyUrl: https://github.com/$Repository/blob/master/docs/privacy.md
Author: PureLogicCode
PackageName: Simple Launcher
PackageUrl: https://github.com/$Repository
License: GPL-3.0-only
LicenseUrl: https://github.com/$Repository/blob/master/LICENSE.txt
Copyright: Copyright (C) PureLogicCode
ShortDescription: Open-source emulator frontend for Windows, Linux and macOS.
Description: |-
  Simple Launcher organizes your ROM collection, downloads and configures emulators
  in Easy Mode, and launches games with RetroAchievements, gamepad navigation,
  favorites and play history. It also scans PC store libraries on Windows. Free and
  open source under the GPLv3, with no ads and no account. ROMs, ISOs and BIOS files
  are not included.
Moniker: simplelauncher
Tags:
- emulator
- emulation
- frontend
- launcher
- retro
- retroachievements
- rom
- game-launcher
ReleaseNotesUrl: https://github.com/$Repository/releases/tag/$tag
InstallationNotes: Installs per user under %LocalAppData%\SimpleLauncher and adds Start Menu shortcuts. The .NET 11 runtime is installed as a winget dependency when missing. Uninstalling keeps settings, game lists and game media; delete the install folder to remove everything.
ManifestType: defaultLocale
ManifestVersion: $schemaVersion

"@

$manifests = [ordered]@{
    "$packageIdentifier.yaml" = $versionManifest
    "$packageIdentifier.installer.yaml" = $installerManifest
    "$packageIdentifier.locale.en-US.yaml" = $localeManifest
}

if (Test-Path -LiteralPath $manifestDir) {
    Remove-Item -LiteralPath $manifestDir -Recurse -Force
}
$null = New-Item -ItemType Directory -Path $manifestDir -Force

foreach ($entry in $manifests.GetEnumerator()) {
    $path = Join-Path $manifestDir $entry.Key
    # winget-pkgs stores LF line endings; keep the generated files identical to what
    # will be committed to the fork.
    $content = $entry.Value -replace "`r`n", "`n"
    Set-Content -LiteralPath $path -Value $content -Encoding utf8NoBOM -NoNewline
    Write-Host "Wrote $path"
}

if (-not $SkipValidation) {
    $winget = Get-Command 'winget' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $winget) {
        Write-Warning 'winget.exe was not found; skipping manifest validation.'
    }
    else {
        $validationOutput = & $winget.Source validate --manifest $manifestDir 2>&1
        $validationExitCode = $LASTEXITCODE
        foreach ($line in @($validationOutput)) {
            Write-Host $line
        }
        if ($validationExitCode -ne 0) {
            throw "winget validate failed with exit code $validationExitCode."
        }
    }
}

Write-Host ""
Write-Host "winget manifests for $packageIdentifier $Version are in:"
Write-Host "  $manifestDir"
Write-Host ""
Write-Host "Submit them to microsoft/winget-pkgs (copy the manifests\ tree into your fork):"
Write-Host "  $OutputDir\manifests\p\PureLogicCode\SimpleLauncher\$Version"
