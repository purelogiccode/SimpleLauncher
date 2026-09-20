#Requires -Version 7
<#
.SYNOPSIS
Packages the Linux release artifacts (linux-x64 / linux-arm64) for SimpleLauncher.Avalonia.

.DESCRIPTION
For every runtime identifier this produces, in the output folder:

  release_{version}_{rid}.zip   the self-contained SimpleLauncher.Avalonia payload
  updater_{rid}.zip             the standalone Updater binary (self-contained single file)

The asset names match the ones the app and the updater look up in the release
(AvaloniaCheckForUpdatesService.ReleaseAssetName / UpdaterAssetName), so the same
GitHub release serves Windows and Linux without collisions.

The application payload is published self-contained (Linux users are not expected to
install the .NET runtime) and Windows-only bundled tools are pruned:
  * tools/**/*.exe and tools/**/*.dll (Windows binaries)
  * tools/FindRomCover/** (WebView2-based Windows tool)
  * the other architecture's RetroAchievementsSharp and 7-Zip binaries
RetroAchievementsSharp and 7-Zip ship per-architecture extension-less binaries on Linux;
the matching ones are kept and marked executable.

ZIP files cannot store Unix permissions reliably when written from Windows, so the
script writes the permission bits into the ZIP entries' external attributes
(0755 for executables, 0644 for everything else). Extraction with `unzip` restores
them; the in-app updater additionally preserves the installed files' modes when it
swaps files, and re-applies the executable bit to a freshly downloaded updater.

The version is validated against the canonical SimpleLauncher.csproj (plus the
Avalonia/Core/updater projects and both manifests) before publishing.

.PARAMETER Version
The release version, e.g. 5.8.0. Must match the csproj/manifest values.

.PARAMETER RuntimeIdentifiers
RIDs to package. Defaults to linux-x64 and linux-arm64.

.PARAMETER OutputDir
Where the zips are written. Defaults to artifacts\release.

.PARAMETER WorkDir
Scratch folder for the publish outputs. Defaults to artifacts\publish-linux.

.EXAMPLE
pwsh scripts/package-release-linux.ps1 -Version 5.8.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string[]]$RuntimeIdentifiers = @('linux-x64', 'linux-arm64'),

    [string]$OutputDir,

    [string]$WorkDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression | Out-Null

$repoDir = Split-Path -Parent $PSScriptRoot
$wpfProject = Join-Path $repoDir 'SimpleLauncher\SimpleLauncher.csproj'
$avaloniaProject = Join-Path $repoDir 'SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj'
$updaterProject = Join-Path $repoDir 'SimpleLauncher.Avalonia.Updater\SimpleLauncher.Avalonia.Updater.csproj'

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoDir 'artifacts\release'
}
if (-not $WorkDir) {
    $WorkDir = Join-Path $repoDir 'artifacts\publish-linux'
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$WorkDir = [System.IO.Path]::GetFullPath($WorkDir)

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' is not in Major.Minor.Patch form (e.g. 5.8.0)."
}

foreach ($rid in $RuntimeIdentifiers) {
    if ($rid -notin @('linux-x64', 'linux-arm64')) {
        throw "Unsupported runtime identifier '$rid'. Expected linux-x64 or linux-arm64."
    }
}

# Unix permission bits stored in the ZIP external attributes (high 16 bits).
$fileMode = 0x81A4 -shl 16      # 0100644
$executableMode = 0x81ED -shl 16 # 0100755

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
        'SimpleLauncher\SimpleLauncher.csproj'                   = $wpfProject
        'SimpleLauncher.Core\SimpleLauncher.Core.csproj'         = (Join-Path $repoDir 'SimpleLauncher.Core\SimpleLauncher.Core.csproj')
        'SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj' = $avaloniaProject
        'SimpleLauncher.Avalonia.Updater\...csproj'              = $updaterProject
    }

    foreach ($entry in $projectVersions.GetEnumerator()) {
        $projectVersion = Get-CsprojVersion $entry.Value
        if ($projectVersion -ne $Version) {
            throw "Version mismatch: requested $Version but $($entry.Key) says $projectVersion."
        }
    }

    $manifests = @(
        'SimpleLauncher\app.manifest',
        'SimpleLauncher.Avalonia\app.manifest'
    )
    foreach ($manifestRelative in $manifests) {
        $manifestVersion = Get-ManifestVersion (Join-Path $repoDir $manifestRelative)
        if ($manifestVersion -ne "$Version.0") {
            throw "Version mismatch: $manifestRelative says $manifestVersion, expected $Version.0."
        }
    }

    Write-Host "Version $Version is consistent across the app projects, the updater project and the manifests."
}

function Invoke-DotnetPublish {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

# Removes the Windows-only bundled tools from the Linux payload and returns the number removed.
function Remove-WindowsOnlyFiles {
    param(
        [Parameter(Mandatory = $true)][string]$PublishDir,
        [Parameter(Mandatory = $true)][string]$Rid
    )

    $removed = 0
    $files = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File -Force)

    foreach ($file in $files) {
        $relative = [System.IO.Path]::GetRelativePath($PublishDir, $file.FullName).Replace('\', '/')
        $remove = $false

        if ($relative -like 'tools/FindRomCover/*') {
            $remove = $true
        }
        elseif ($relative -like 'tools/*') {
            if ($file.Extension -in @('.exe', '.dll')) {
                $remove = $true
            }
            elseif ($Rid -eq 'linux-x64' -and $relative -in @(
                'tools/RetroAchievementsSharp/RetroAchievementsSharp_arm64',
                'tools/SevenZip/7zz_arm64'
            )) {
                $remove = $true
            }
            elseif ($Rid -eq 'linux-arm64' -and $relative -in @(
                'tools/RetroAchievementsSharp/RetroAchievementsSharp',
                'tools/SevenZip/7zz'
            )) {
                $remove = $true
            }
        }

        if ($remove) {
            Remove-Item -LiteralPath $file.FullName -Force
            $removed++
        }
    }

    # Drop directories left empty by the pruning.
    $directories = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -Directory -Force |
        Sort-Object -Property @{ Expression = { $_.FullName.Length } } -Descending)
    foreach ($directory in $directories) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) {
            Remove-Item -LiteralPath $directory.FullName -Force
        }
    }

    Write-Host "Pruned $removed Windows-only files from the $Rid payload."
}

# Files that must be executable on Linux (relative paths with forward slashes).
function Test-ExecutableEntry {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Rid
    )

    if ($RelativePath -eq 'SimpleLauncher.Avalonia' -or $RelativePath -eq 'Updater') {
        return $true
    }

    if ($Rid -eq 'linux-arm64') {
        return $RelativePath -eq 'tools/RetroAchievementsSharp/RetroAchievementsSharp_arm64' -or
            $RelativePath -eq 'tools/SevenZip/7zz_arm64'
    }

    return $RelativePath -eq 'tools/RetroAchievementsSharp/RetroAchievementsSharp' -or
        $RelativePath -eq 'tools/SevenZip/7zz'
}

# unzip only honours the Unix permission bits in a ZIP entry's external attributes when the
# entry's "version made by" reports a Unix host. .NET's ZipArchive always stamps the MS-DOS
# host, so after writing the archive the OS byte of every central-directory entry is patched
# to 3 (Unix). The archives are generated by this script, so the PK\x01\x02 scan cannot hit
# anything but real central-directory records in practice.
function Set-UnixHostSystem {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    $bytes = [System.IO.File]::ReadAllBytes($ZipPath)
    $patched = 0
    for ($i = 0; $i -le $bytes.Length - 6; $i++) {
        if ($bytes[$i] -eq 0x50 -and $bytes[$i + 1] -eq 0x4B -and
            $bytes[$i + 2] -eq 0x01 -and $bytes[$i + 3] -eq 0x02) {
            if ($bytes[$i + 5] -ne 0x03) {
                $bytes[$i + 5] = 0x03
                $patched++
            }
        }
    }

    [System.IO.File]::WriteAllBytes($ZipPath, $bytes)
    Write-Host "Marked $patched zip entries as created on Unix (permission bits)."
}

function New-LinuxZipArchive {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string]$Rid
    )

    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }

    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in @(Get-ChildItem -LiteralPath $SourceDir -Recurse -File -Force)) {
            $relative = [System.IO.Path]::GetRelativePath($SourceDir, $file.FullName).Replace('\', '/')
            $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $file.LastWriteTime
            $entry.ExternalAttributes = if (Test-ExecutableEntry -RelativePath $relative -Rid $Rid) {
                $executableMode
            }
            else {
                $fileMode
            }

            $entryStream = $entry.Open()
            try {
                $fileStream = [System.IO.File]::OpenRead($file.FullName)
                try {
                    $fileStream.CopyTo($entryStream)
                }
                finally {
                    $fileStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    Set-UnixHostSystem -ZipPath $ZipPath
}

Assert-Versions -Version $Version

$null = New-Item -ItemType Directory -Path $OutputDir -Force
if (Test-Path -LiteralPath $WorkDir) { Remove-Item -LiteralPath $WorkDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path $WorkDir -Force

$produced = [System.Collections.Generic.List[string]]::new()

foreach ($rid in $RuntimeIdentifiers) {
    $appPublishDir = Join-Path $WorkDir "$rid\app"
    $updaterPublishDir = Join-Path $WorkDir "$rid\updater"

    Write-Host ""
    Write-Host "=== SimpleLauncher.Avalonia $Version ($rid) ==="

    # Application: self-contained (the Linux payload carries its runtime).
    Invoke-DotnetPublish @(
        'publish',
        $avaloniaProject,
        '-c', 'Release',
        '-f', 'net10.0',
        '-r', $rid,
        '--self-contained', 'true',
        '--nologo',
        '-o', $appPublishDir
    )

    Remove-WindowsOnlyFiles -PublishDir $appPublishDir -Rid $rid

    $releaseZip = Join-Path $OutputDir "release_${Version}_${rid}.zip"
    New-LinuxZipArchive -SourceDir $appPublishDir -ZipPath $releaseZip -Rid $rid
    [void]$produced.Add($releaseZip)

    # Standalone updater: self-contained single file so it runs on a machine without
    # the .NET runtime (the app downloads this from the release assets before updating).
    Invoke-DotnetPublish @(
        'publish',
        $updaterProject,
        '-c', 'Release',
        '-f', 'net10.0',
        '-r', $rid,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '--nologo',
        '-o', $updaterPublishDir
    )

    $updaterZip = Join-Path $OutputDir "updater_${rid}.zip"
    New-LinuxZipArchive -SourceDir $updaterPublishDir -ZipPath $updaterZip -Rid $rid
    [void]$produced.Add($updaterZip)
}

Write-Host ""
Write-Host "Packages written to $OutputDir :"
foreach ($path in $produced) {
    $size = [math]::Round((Get-Item -LiteralPath $path).Length / 1MB, 1)
    Write-Host ("  {0} ({1} MB)" -f (Split-Path -Leaf $path), $size)
}
