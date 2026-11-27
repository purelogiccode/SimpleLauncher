#Requires -Version 7
<#
.SYNOPSIS
Packages the macOS Apple Silicon release DMG (osx-arm64) for SimpleLauncher.Avalonia.

.DESCRIPTION
Produces, in the output folder:

  SimpleLauncher-{version}-osx-arm64.dmg

The DMG contains a self-contained `Simple Launcher.app` (the .NET 11 runtime travels
inside the bundle, so no runtime installation is required) and an Applications shortcut.
The bundle is ad-hoc signed with `codesign -s -`: it runs on Apple Silicon without an
Apple Developer account, but it is NOT Developer ID signed and NOT notarized, so the
first launch goes through the Gatekeeper "Open Anyway" flow.

The application payload is published self-contained (not single-file) so the macOS
bundle keeps the conventional `Contents/MacOS/` layout. Windows- and Linux-only helper
payloads are removed: no macOS payloads exist for CHDMounter, RetroAchievementsSharp,
SimpleXisoDrive, SimpleZipDrive or 7-Zip. The standalone Updater is removed too: macOS
installs update by downloading the next DMG (no in-app updater).

The version is validated against the canonical SimpleLauncher.Avalonia.csproj (plus the
Core and updater projects and the Avalonia manifest) before publishing.

This script must run on macOS; it uses `sips`/`iconutil`, `codesign`, `plutil` and
`hdiutil`.

.PARAMETER Version
The release version, e.g. 5.8.4. Must match the csproj/manifest values.

.PARAMETER OutputDir
Where the DMG is written. Defaults to artifacts/release.

.PARAMETER WorkDir
Scratch folder for the publish output and bundle. Defaults to artifacts/publish-macos.

.PARAMETER NoAdHocSign
Skip the ad-hoc `codesign -s -` pass (the bundle stays completely unsigned; it may be
rejected by Gatekeeper and by macOS's arm64 code-signature requirement).

.EXAMPLE
pwsh scripts/package-release-macos.ps1 -Version 5.8.4
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputDir,

    [string]$WorkDir,

    [switch]$NoAdHocSign
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $IsMacOS) {
    throw 'package-release-macos.ps1 must run on macOS (it needs sips, iconutil, codesign and hdiutil).'
}

$repoDir = Split-Path -Parent $PSScriptRoot
$avaloniaProject = Join-Path $repoDir 'SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj'
$rid = 'osx-arm64'
$appName = 'Simple Launcher'
$executableName = 'SimpleLauncher.Avalonia'

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoDir 'artifacts/release'
}
if (-not $WorkDir) {
    $WorkDir = Join-Path $repoDir 'artifacts/publish-macos'
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$WorkDir = [System.IO.Path]::GetFullPath($WorkDir)

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' is not in Major.Minor.Patch form (e.g. 5.8.4)."
}

function Get-CsprojVersion {
    param([Parameter(Mandatory = $true)][string]$Path)

    $xml = [xml](Get-Content -LiteralPath $Path -Raw)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $node) { throw "Could not read <Version> from $Path." }
    return $node.InnerText.Trim()
}

function Get-ManifestVersion {
    param([Parameter(Mandatory = $true)][string]$Path)

    $manifest = [xml](Get-Content -LiteralPath $Path -Raw)
    return $manifest.assembly.assemblyIdentity.GetAttribute('version')
}

function Assert-Versions {
    param([Parameter(Mandatory = $true)][string]$Version)

    $projectVersions = [ordered]@{
        'SimpleLauncher.Core/SimpleLauncher.Core.csproj'         = (Join-Path $repoDir 'SimpleLauncher.Core/SimpleLauncher.Core.csproj')
        'SimpleLauncher.Diagnostics/...csproj'                   = (Join-Path $repoDir 'SimpleLauncher.Diagnostics/SimpleLauncher.Diagnostics.csproj')
        'SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj' = $avaloniaProject
        'SimpleLauncher.Avalonia.Updater/...csproj'              = (Join-Path $repoDir 'SimpleLauncher.Avalonia.Updater/SimpleLauncher.Avalonia.Updater.csproj')
    }

    foreach ($entry in $projectVersions.GetEnumerator()) {
        $projectVersion = Get-CsprojVersion $entry.Value
        if ($projectVersion -ne $Version) {
            throw "Version mismatch: requested $Version but $($entry.Key) says $projectVersion."
        }
    }

    $manifests = @(
        'SimpleLauncher.Avalonia/app.manifest'
    )
    foreach ($manifestRelative in $manifests) {
        $manifestVersion = Get-ManifestVersion (Join-Path $repoDir $manifestRelative)
        if ($manifestVersion -ne "$Version.0") {
            throw "Version mismatch: $manifestRelative says $manifestVersion, expected $Version.0."
        }
    }

    Write-Host "Version $Version is consistent across the Avalonia app, the updater project and the manifest."
}

function Invoke-DotnetPublish {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

# The macOS bundle has no native payloads for the bundled tools (all of them ship only
# win-*/linux-* folders), and no in-app updater (updates replace the whole .app from a
# new DMG). Remove both so the bundle does not advertise features it cannot run.
function Remove-UnsupportedPayload {
    param([Parameter(Mandatory = $true)][string]$PublishDir)

    $toolsDir = Join-Path $PublishDir 'tools'
    if (Test-Path -LiteralPath $toolsDir) {
        Remove-Item -LiteralPath $toolsDir -Recurse -Force
        Write-Host 'Removed the bundled tools (no osx-arm64 payloads).'
    }

    $removedUpdaters = 0
    foreach ($file in @(Get-ChildItem -LiteralPath $PublishDir -File -Force |
            Where-Object { $_.Name -like 'Updater*' })) {
        Remove-Item -LiteralPath $file.FullName -Force
        $removedUpdaters++
    }
    Write-Host "Removed $removedUpdaters Updater file(s) (macOS updates use a new DMG)."
}

function Copy-BundleDocuments {
    param([Parameter(Mandatory = $true)][string]$PayloadDir)

    $documents = @(
        @{ Source = (Join-Path $repoDir 'LICENSE.txt'); Name = 'LICENSE.txt' },
        @{ Source = (Join-Path $repoDir 'README.md'); Name = 'README.md' },
        @{ Source = (Join-Path $repoDir 'SimpleLauncher/WhatsNew.md'); Name = 'WhatsNew.md' }
    )

    foreach ($document in $documents) {
        if (-not (Test-Path -LiteralPath $document.Source)) {
            throw "Bundle document not found: $($document.Source)"
        }
        Copy-Item -LiteralPath $document.Source -Destination (Join-Path $PayloadDir $document.Name) -Force
    }

    Write-Host 'Added LICENSE.txt, README.md and WhatsNew.md to the payload.'
}

function New-AppIcon {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePng,
        [Parameter(Mandatory = $true)][string]$IcnsPath,
        [Parameter(Mandatory = $true)][string]$ScratchDir
    )

    if (-not (Test-Path -LiteralPath $SourcePng)) {
        Write-Warning "Icon source '$SourcePng' not found; the bundle will use the default icon."
        return $false
    }

    $iconset = Join-Path $ScratchDir 'SimpleLauncher.iconset'
    if (Test-Path -LiteralPath $iconset) { Remove-Item -LiteralPath $iconset -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $iconset -Force

    $variants = @(
        @{ Name = 'icon_16x16.png'; Size = 16 },
        @{ Name = 'icon_16x16@2x.png'; Size = 32 },
        @{ Name = 'icon_32x32.png'; Size = 32 },
        @{ Name = 'icon_32x32@2x.png'; Size = 64 },
        @{ Name = 'icon_128x128.png'; Size = 128 },
        @{ Name = 'icon_128x128@2x.png'; Size = 256 },
        @{ Name = 'icon_256x256.png'; Size = 256 },
        @{ Name = 'icon_256x256@2x.png'; Size = 512 },
        @{ Name = 'icon_512x512.png'; Size = 512 },
        @{ Name = 'icon_512x512@2x.png'; Size = 1024 }
    )

    foreach ($variant in $variants) {
        $destination = Join-Path $iconset $variant.Name
        & sips -z $variant.Size $variant.Size $SourcePng --out $destination | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "sips failed to render $($variant.Name) from $SourcePng."
        }
    }

    & iconutil -c icns $iconset -o $IcnsPath
    if ($LASTEXITCODE -ne 0) {
        throw "iconutil failed to build $IcnsPath."
    }

    Write-Host "Built the application icon from $SourcePng."
    return $true
}

function New-InfoPlist {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Version
    )

    # .NET 11 supports macOS 15 and newer on arm64. The bundle is ad-hoc signed only;
    # there is no Developer ID signature or notarization ticket.
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>$appName</string>
    <key>CFBundleExecutable</key>
    <string>$executableName</string>
    <key>CFBundleIconFile</key>
    <string>SimpleLauncher</string>
    <key>CFBundleIdentifier</key>
    <string>com.purelogiccode.simplelauncher</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$appName</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$Version</string>
    <key>CFBundleVersion</key>
    <string>$Version</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>LSMinimumSystemVersion</key>
    <string>15.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
"@

    [System.IO.File]::WriteAllText($Path, $plist, [System.Text.UTF8Encoding]::new($false))
}

function New-Dmg {
    param(
        [Parameter(Mandatory = $true)][string]$BundlePath,
        [Parameter(Mandatory = $true)][string]$DmgPath,
        [Parameter(Mandatory = $true)][string]$ScratchDir,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $staging = Join-Path $ScratchDir 'dmg-root'
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $staging -Force

    Copy-Item -LiteralPath $BundlePath -Destination $staging -Recurse -Force
    $null = New-Item -ItemType SymbolicLink -Path (Join-Path $staging 'Applications') -Target '/Applications'

    if (Test-Path -LiteralPath $DmgPath) { Remove-Item -LiteralPath $DmgPath -Force }
    & hdiutil create -quiet -volname "Simple Launcher $Version" -srcfolder $staging -ov -format UDZO $DmgPath
    if ($LASTEXITCODE -ne 0) {
        throw "hdiutil create failed with exit code $LASTEXITCODE."
    }

    & hdiutil verify -quiet $DmgPath
    if ($LASTEXITCODE -ne 0) {
        throw "hdiutil verify failed with exit code $LASTEXITCODE."
    }

    # Mount the finished image and confirm the bundle really contains the launcher, so a
    # broken DMG cannot reach the release.
    $mountPoint = Join-Path $ScratchDir 'dmg-mount'
    if (Test-Path -LiteralPath $mountPoint) { Remove-Item -LiteralPath $mountPoint -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $mountPoint -Force

    & hdiutil attach -quiet -nobrowse -readonly -mountpoint $mountPoint $DmgPath
    if ($LASTEXITCODE -ne 0) {
        throw "hdiutil attach failed with exit code $LASTEXITCODE."
    }

    try {
        $mountedExecutable = Join-Path $mountPoint "$appName.app/Contents/MacOS/$executableName"
        if (-not (Test-Path -LiteralPath $mountedExecutable)) {
            throw "The DMG does not contain $appName.app/Contents/MacOS/$executableName."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $mountPoint 'Applications'))) {
            throw 'The DMG does not contain the Applications shortcut.'
        }
    }
    finally {
        & hdiutil detach -quiet $mountPoint
    }

    Write-Host 'Verified the DMG contents (launcher executable and Applications shortcut).'
}

Assert-Versions -Version $Version

$null = New-Item -ItemType Directory -Path $OutputDir -Force
if (Test-Path -LiteralPath $WorkDir) { Remove-Item -LiteralPath $WorkDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path $WorkDir -Force

$publishDir = Join-Path $WorkDir 'publish'
$bundlePath = Join-Path $WorkDir "$appName.app"
$contentsDir = Join-Path $bundlePath 'Contents'
$macOsDir = Join-Path $contentsDir 'MacOS'
$resourcesDir = Join-Path $contentsDir 'Resources'

Write-Host ''
Write-Host "=== SimpleLauncher.Avalonia $Version ($rid) ==="

# Self-contained publish: the .NET 11 runtime travels inside the .app, so the DMG needs
# no runtime installation. Non-single-file keeps the conventional Contents/MacOS layout.
Invoke-DotnetPublish @(
    'publish',
    $avaloniaProject,
    '-c', 'Release',
    '-f', 'net11.0',
    '-r', $rid,
    '--self-contained', 'true',
    '-p:PublishSingleFile=false',
    '--nologo',
    '-o', $publishDir
)

Remove-UnsupportedPayload -PublishDir $publishDir
Copy-BundleDocuments -PayloadDir $publishDir

# Ship the macOS Easy Mode catalog inside the bundle so Easy Mode works on first launch
# (the API/fallback path is still used by the other platforms, and the same file is
# mirrored to R2 by publish-fallback-r2.ps1).
$easyModeCatalog = Join-Path $repoDir 'SimpleLauncher/Services/EasyMode/Samples/easymode_macos_arm64.xml'
if (Test-Path -LiteralPath $easyModeCatalog) {
    Copy-Item -LiteralPath $easyModeCatalog -Destination (Join-Path $publishDir 'easymode_macos_arm64.xml') -Force
    Write-Host 'Added easymode_macos_arm64.xml to the payload.'
}
else {
    Write-Warning "macOS Easy Mode catalog not found: $easyModeCatalog"
}

$null = New-Item -ItemType Directory -Path $macOsDir, $resourcesDir -Force
Get-ChildItem -LiteralPath $publishDir -Force | Copy-Item -Destination $macOsDir -Recurse -Force

$executablePath = Join-Path $macOsDir $executableName
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "The publish output did not contain $executableName."
}
& chmod +x $executablePath

$iconSource = Join-Path $repoDir 'SimpleLauncher.Avalonia/icon/icon.png'
$icnsPath = Join-Path $resourcesDir 'SimpleLauncher.icns'
try {
    $null = New-AppIcon -SourcePng $iconSource -IcnsPath $icnsPath -ScratchDir $WorkDir
}
catch {
    Write-Warning "Could not build the application icon: $($_.Exception.Message)"
}

$infoPlistPath = Join-Path $contentsDir 'Info.plist'
New-InfoPlist -Path $infoPlistPath -Version $Version

& plutil -lint $infoPlistPath | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'plutil rejected the generated Info.plist.'
}

if (-not $NoAdHocSign) {
    # Ad-hoc signature only: this is NOT Developer ID signing and NOT notarization.
    # Apple Silicon requires at least an ad-hoc signature for modified bundles.
    & codesign --force --deep --sign - $bundlePath
    if ($LASTEXITCODE -ne 0) {
        throw "codesign (ad-hoc) failed with exit code $LASTEXITCODE."
    }

    & codesign --verify --deep --strict $bundlePath
    if ($LASTEXITCODE -ne 0) {
        throw 'codesign verification of the ad-hoc signed bundle failed.'
    }

    Write-Host 'Ad-hoc signed the app bundle (not Developer ID, not notarized).'
}
else {
    Write-Host 'Skipped code signing (-NoAdHocSign).'
}

$dmgPath = Join-Path $OutputDir "SimpleLauncher-${Version}-${rid}.dmg"
New-Dmg -BundlePath $bundlePath -DmgPath $dmgPath -ScratchDir $WorkDir -Version $Version

$size = [math]::Round((Get-Item -LiteralPath $dmgPath).Length / 1MB, 1)
$hash = (Get-FileHash -LiteralPath $dmgPath -Algorithm SHA256).Hash

Write-Host ''
Write-Host 'Package written to the output folder:'
Write-Host ("  {0} ({1} MB)" -f (Split-Path -Leaf $dmgPath), $size)
Write-Host "  SHA256: $hash"
