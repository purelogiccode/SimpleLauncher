#Requires -Version 7
<#
.SYNOPSIS
Packages the SimpleLauncher WPF release artifacts (win-x64 / win-arm64).

.DESCRIPTION
For every runtime identifier this produces, in the output folder:
  release_{version}_{rid}.zip   the app payload (framework-dependent single-file)
  updater_{rid}.zip             the standalone Updater.exe

The app is published with:
  dotnet publish SimpleLauncher/SimpleLauncher.csproj -c Release -r <rid> --self-contained false -p:PublishSingleFile=true

The other architecture's bundled tools are pruned from each payload:
  win-x64   : drops *_arm64 files and tools/FindRomCover/arm64
  win-arm64 : drops *_x64 files, tools/FindRomCover/x64, and the unsuffixed
              executables that have an _arm64 sibling (7z.dll is kept)
Both RIDs drop the Linux-only extension-less RetroAchievementsSharp binaries.

The version is validated against SimpleLauncher.csproj, SimpleLauncher.Core.csproj,
SimpleLauncher/app.manifest and SimpleLauncher.Updater/version.txt before publishing.

.PARAMETER Version
The release version, e.g. 5.7.0. Must match the csproj/manifest/version.txt values.

.PARAMETER RuntimeIdentifiers
RIDs to package. Defaults to win-x64 and win-arm64.

.PARAMETER OutputDir
Where the zips are written. Defaults to SimpleLauncher\bin\Release.

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
if (-not $OutputDir) { $OutputDir = Join-Path $repoDir 'SimpleLauncher\bin\Release' }
if (-not $WorkDir) { $WorkDir = Join-Path $repoDir 'artifacts\publish' }
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

function Assert-Versions {
    param([Parameter(Mandatory = $true)][string]$Version)

    $appVersion = Get-CsprojVersion (Join-Path $repoDir 'SimpleLauncher\SimpleLauncher.csproj')
    $coreVersion = Get-CsprojVersion (Join-Path $repoDir 'SimpleLauncher.Core\SimpleLauncher.Core.csproj')

    $manifestPath = Join-Path $repoDir 'SimpleLauncher\app.manifest'
    $manifest = [xml](Get-Content -LiteralPath $manifestPath -Raw)
    $manifestVersion = $manifest.assembly.assemblyIdentity.GetAttribute('version')

    $updaterVersionPath = Join-Path $repoDir 'SimpleLauncher.Updater\version.txt'
    $updaterVersion = (Get-Content -LiteralPath $updaterVersionPath -Raw).Trim()

    if ($appVersion -ne $Version) {
        throw "Version mismatch: requested $Version but SimpleLauncher.csproj says $appVersion."
    }
    if ($coreVersion -ne $Version) {
        throw "Version mismatch: requested $Version but SimpleLauncher.Core.csproj says $coreVersion."
    }
    if ($manifestVersion -ne "$Version.0") {
        throw "Version mismatch: SimpleLauncher/app.manifest says $manifestVersion, expected $Version.0."
    }
    if ($updaterVersion -ne "release$Version") {
        throw "Version mismatch: SimpleLauncher.Updater/version.txt says $updaterVersion, expected release$Version."
    }

    Write-Host "Version $Version is consistent across csproj, manifest and version.txt."
}

function Invoke-DotnetPublish {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
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

function New-SingleFileZipArchive {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$EntryName,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )

    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $FilePath, $EntryName, [System.IO.Compression.CompressionLevel]::Optimal)
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
    $appPublishDir = Join-Path $WorkDir "$rid\app"
    $updaterPublishDir = Join-Path $WorkDir "$rid\updater"

    Write-Host ""
    Write-Host "=== SimpleLauncher $Version ($rid) ==="

    Invoke-DotnetPublish @(
        'publish',
        (Join-Path $repoDir 'SimpleLauncher\SimpleLauncher.csproj'),
        '-c', 'Release',
        '-r', $rid,
        '--self-contained', 'false',
        '-p:PublishSingleFile=true',
        '--nologo',
        '-o', $appPublishDir
    )
    Remove-OtherArchitectureFiles -PublishDir $appPublishDir -Rid $rid

    $releaseZip = Join-Path $OutputDir "release_${Version}_${rid}.zip"
    New-ZipArchive -SourceDir $appPublishDir -ZipPath $releaseZip
    [void]$produced.Add($releaseZip)

    Invoke-DotnetPublish @(
        'publish',
        (Join-Path $repoDir 'SimpleLauncher.Updater\SimpleLauncher.Updater.csproj'),
        '-c', 'Release',
        '-r', $rid,
        '--self-contained', 'false',
        '-p:PublishSingleFile=true',
        '--nologo',
        '-o', $updaterPublishDir
    )

    $updaterExe = Join-Path $updaterPublishDir 'Updater.exe'
    if (-not (Test-Path -LiteralPath $updaterExe)) {
        throw "Updater.exe was not produced at $updaterExe."
    }

    $updaterZip = Join-Path $OutputDir "updater_${rid}.zip"
    New-SingleFileZipArchive -FilePath $updaterExe -EntryName 'Updater.exe' -ZipPath $updaterZip
    [void]$produced.Add($updaterZip)
}

Write-Host ""
Write-Host "Packages written to $OutputDir :"
foreach ($path in $produced) {
    $size = [math]::Round((Get-Item -LiteralPath $path).Length / 1MB, 1)
    Write-Host ("  {0} ({1} MB)" -f (Split-Path -Leaf $path), $size)
}
