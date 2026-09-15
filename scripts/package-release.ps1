#Requires -Version 7
<#
.SYNOPSIS
Packages the unified SimpleLauncher release artifacts (win-x64 / win-arm64).

.DESCRIPTION
For every runtime identifier this produces, in the output folder:

  release_{version}_{rid}.zip   the unified payload: SimpleLauncher.exe (WPF) and
                                SimpleLauncher.Avalonia.exe next to each other, sharing
                                the content files (images, tools, samples, appsettings.json)
  updater_{rid}.zip             the single standalone Updater.exe (framework-dependent,
                                single-file) used by both apps

Both apps are published framework-dependent (the .NET 10 Desktop Runtime is required on
the target machine), so the payload does not carry a runtime. The updater is the same
binary for both apps: it receives the target application in its command line (older WPF
releases pass only the PID and the updater detects the app from the process name).

The WPF and Avalonia payloads are merged into one folder. Files that exist in both
payloads must be byte-identical (the two appsettings.json sources are kept in sync and
guarded by a unit test); any other content conflict fails the build loudly.

The other architecture's bundled tools are pruned from the payload:
  win-x64   : drops *_arm64 files and tools/FindRomCover/arm64
  win-arm64 : drops *_x64 files, tools/FindRomCover/x64, and the unsuffixed
              executables that have an _arm64 sibling (7z.dll is kept)
Both RIDs drop the Linux-only extension-less RetroAchievementsSharp binaries.

Debug symbol files (*.pdb) are pruned from the payload: they are never needed at runtime
and the native SkiaSharp/HarfBuzzSharp symbols alone account for roughly 105 MB.

The version is validated against the canonical SimpleLauncher.csproj (plus both app
projects/manifests, SimpleLauncher.Core.csproj and the Avalonia updater project) before
publishing.

.PARAMETER Version
The release version, e.g. 5.7.0. Must match the csproj/manifest values.

.PARAMETER RuntimeIdentifiers
RIDs to package. Defaults to win-x64 and win-arm64.

.PARAMETER OutputDir
Where the zips are written. Defaults to artifacts\release.

.PARAMETER WorkDir
Scratch folder for the publish outputs. Defaults to artifacts\publish.

.EXAMPLE
pwsh scripts/package-release.ps1 -Version 5.7.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string[]]$RuntimeIdentifiers = @('win-x64', 'win-arm64'),

    [string]$OutputDir,

    [string]$WorkDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

$repoDir = Split-Path -Parent $PSScriptRoot
$wpfProject = Join-Path $repoDir 'SimpleLauncher\SimpleLauncher.csproj'
$avaloniaProject = Join-Path $repoDir 'SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj'
$updaterProject = Join-Path $repoDir 'SimpleLauncher.Avalonia.Updater\SimpleLauncher.Avalonia.Updater.csproj'

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoDir 'artifacts\release'
}
if (-not $WorkDir) {
    $WorkDir = Join-Path $repoDir 'artifacts\publish'
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$WorkDir = [System.IO.Path]::GetFullPath($WorkDir)

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' is not in Major.Minor.Patch form (e.g. 5.7.0)."
}

foreach ($rid in $RuntimeIdentifiers) {
    if ($rid -notin @('win-x64', 'win-arm64')) {
        throw "Unsupported runtime identifier '$rid'. Expected win-x64 or win-arm64."
    }
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
        'SimpleLauncher\SimpleLauncher.csproj'                       = $wpfProject
        'SimpleLauncher.Core\SimpleLauncher.Core.csproj'             = (Join-Path $repoDir 'SimpleLauncher.Core\SimpleLauncher.Core.csproj')
        'SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj'     = $avaloniaProject
        'SimpleLauncher.Avalonia.Updater\...csproj'                  = $updaterProject
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

    Write-Host "Version $Version is consistent across both app projects, the updater project and the manifests."
}

function Invoke-DotnetPublish {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

# Merges the WPF and Avalonia publish outputs into one payload folder. Files present in
# both outputs must have identical content — anything else indicates the two apps have
# drifted apart (for example appsettings.json, which the unified bundle ships once).
function Merge-Payload {
    param(
        [Parameter(Mandatory = $true)][string[]]$SourceDirs,
        [Parameter(Mandatory = $true)][string]$DestinationDir
    )

    $null = New-Item -ItemType Directory -Path $DestinationDir -Force
    $hashes = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $conflicts = [System.Collections.Generic.List[string]]::new()
    $copied = 0

    foreach ($sourceDir in $SourceDirs) {
        $files = @(Get-ChildItem -LiteralPath $sourceDir -Recurse -File -Force)
        foreach ($file in $files) {
            $relative = [System.IO.Path]::GetRelativePath($sourceDir, $file.FullName)
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash

            if ($hashes.ContainsKey($relative)) {
                if ($hashes[$relative] -ne $hash) {
                    [void]$conflicts.Add($relative)
                }
                continue
            }

            $hashes[$relative] = $hash
            $destination = Join-Path $DestinationDir $relative
            $destinationDirectory = Split-Path -Parent $destination
            if (-not (Test-Path -LiteralPath $destinationDirectory)) {
                $null = New-Item -ItemType Directory -Path $destinationDirectory -Force
            }
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            $copied++
        }
    }

    if ($conflicts.Count -gt 0) {
        throw ("The WPF and Avalonia payloads contain files with the same relative path but different content: " +
            ($conflicts -join ', ') +
            ". Keep the shared files in sync (see AppSettingsFilesAreIdentical in VersionConsistencyTests).")
    }

    Write-Host "Merged payload: $copied files copied from $($SourceDirs.Count) publish outputs."
}

# Safety net: the app project references the updater with ReferenceOutputAssembly=false and
# Private=false, so the SDK must not contribute the updater's plain build outputs (dll / deps /
# runtimeconfig) next to the single-file Updater.exe. Remove any that still show up so the
# payload can never ship sidecars that a single-file app does not use.
function Remove-UpdaterSidecars {
    param([Parameter(Mandatory = $true)][string]$PayloadDir)

    foreach ($name in @('Updater', 'Updater.dll', 'Updater.pdb', 'Updater.deps.json', 'Updater.runtimeconfig.json')) {
        $path = Join-Path $PayloadDir $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "Removed updater sidecar '$name' from the payload."
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $PayloadDir 'Updater.exe'))) {
        throw "Updater.exe was not produced in the merged payload. The Avalonia app's updater copy target did not run."
    }
}

# Debug symbol files are never needed to run the apps (the bundled tool binaries do not
# ship any today either); the native SkiaSharp/HarfBuzzSharp symbols are the bulk of it.
function Remove-DebugSymbols {
    param([Parameter(Mandatory = $true)][string]$PayloadDir)

    $symbols = @(Get-ChildItem -LiteralPath $PayloadDir -Recurse -File -Force -Filter '*.pdb')
    if ($symbols.Count -eq 0) {
        return
    }

    $totalBytes = ($symbols | Measure-Object -Property Length -Sum).Sum
    foreach ($symbol in $symbols) {
        Remove-Item -LiteralPath $symbol.FullName -Force
    }

    Write-Host ("Removed {0} debug symbol file(s) ({1} MB) from the payload." -f
        $symbols.Count, [math]::Round($totalBytes / 1MB, 1))
}

function Remove-OtherArchitectureFiles {
    param(
        [Parameter(Mandatory = $true)][string]$PublishDir,
        [Parameter(Mandatory = $true)][string]$Rid
    )

    $files = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File -Force)
    $relativeByFullName = @{}
    $fileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($file in $files) {
        $relative = [System.IO.Path]::GetRelativePath($PublishDir, $file.FullName).Replace('\', '/')
        $relativeByFullName[$file.FullName] = $relative
        [void]$fileSet.Add($relative)
    }

    $removed = 0
    foreach ($file in $files) {
        $relative = $relativeByFullName[$file.FullName]
        $remove = $false

        if ($Rid -eq 'win-x64') {
            if ($relative -like '*_arm64*' -or $relative -like 'tools/FindRomCover/arm64/*') {
                $remove = $true
            }
        }
        else {
            if ($relative -like '*_x64*' -or $relative -like 'tools/FindRomCover/x64/*') {
                $remove = $true
            }
            elseif ($relative -match '\.exe$' -and $relative -notmatch '_arm64\.exe$') {
                $sibling = $relative -replace '\.exe$', '_arm64.exe'
                if ($fileSet.Contains($sibling)) { $remove = $true }
            }
        }

        if (-not $remove -and $relative -like 'tools/RetroAchievementsSharp/*') {
            $leaf = [System.IO.Path]::GetFileName($relative)
            if ($leaf -eq 'RetroAchievementsSharp' -or $leaf -eq 'RetroAchievementsSharp_arm64') {
                $remove = $true
            }
        }

        if ($remove) {
            Remove-Item -LiteralPath $file.FullName -Force
            $removed++
        }
    }

    $directories = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -Directory -Force |
        Sort-Object -Property @{ Expression = { $_.FullName.Length } } -Descending)
    foreach ($directory in $directories) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) {
            Remove-Item -LiteralPath $directory.FullName -Force
        }
    }

    Write-Host "Pruned $removed files that do not belong to $Rid."
}

function New-ZipArchive {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $SourceDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
}

function New-FilesZipArchive {
    param(
        [Parameter(Mandatory = $true)][string[]]$FilePaths,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($filePath in $FilePaths) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $filePath, [System.IO.Path]::GetFileName($filePath),
                [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $archive.Dispose()
    }
}

Assert-Versions -Version $Version

$null = New-Item -ItemType Directory -Path $OutputDir -Force
if (Test-Path -LiteralPath $WorkDir) { Remove-Item -LiteralPath $WorkDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path $WorkDir -Force

$produced = [System.Collections.Generic.List[string]]::new()

foreach ($rid in $RuntimeIdentifiers) {
    $wpfPublishDir = Join-Path $WorkDir "$rid\wpf"
    $avaloniaPublishDir = Join-Path $WorkDir "$rid\avalonia"
    $payloadDir = Join-Path $WorkDir "$rid\payload"

    Write-Host ""
    Write-Host "=== SimpleLauncher (WPF + Avalonia) $Version ($rid) ==="

    # WPF: framework-dependent single file (managed assemblies bundled into SimpleLauncher.exe).
    Invoke-DotnetPublish @(
        'publish',
        $wpfProject,
        '-c', 'Release',
        '-r', $rid,
        '--self-contained', 'false',
        '-p:PublishSingleFile=true',
        '--nologo',
        '-o', $wpfPublishDir
    )

    # Avalonia: framework-dependent multi-file. The app project publishes the single
    # updater (framework-dependent single file) and copies Updater.exe into this output.
    Invoke-DotnetPublish @(
        'publish',
        $avaloniaProject,
        '-c', 'Release',
        '-f', 'net10.0-windows',
        '-r', $rid,
        '--self-contained', 'false',
        '--nologo',
        '-o', $avaloniaPublishDir
    )

    Merge-Payload -SourceDirs @($wpfPublishDir, $avaloniaPublishDir) -DestinationDir $payloadDir
    Remove-UpdaterSidecars -PayloadDir $payloadDir
    Remove-DebugSymbols -PayloadDir $payloadDir
    Remove-OtherArchitectureFiles -PublishDir $payloadDir -Rid $rid

    $releaseZip = Join-Path $OutputDir "release_${Version}_${rid}.zip"
    New-ZipArchive -SourceDir $payloadDir -ZipPath $releaseZip
    [void]$produced.Add($releaseZip)

    $updaterZip = Join-Path $OutputDir "updater_${rid}.zip"
    New-FilesZipArchive -FilePaths @((Join-Path $payloadDir 'Updater.exe')) -ZipPath $updaterZip
    [void]$produced.Add($updaterZip)
}

Write-Host ""
Write-Host "Packages written to $OutputDir :"
foreach ($path in $produced) {
    $size = [math]::Round((Get-Item -LiteralPath $path).Length / 1MB, 1)
    Write-Host ("  {0} ({1} MB)" -f (Split-Path -Leaf $path), $size)
}
