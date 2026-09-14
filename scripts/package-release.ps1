#Requires -Version 7
<#
.SYNOPSIS
Packages the SimpleLauncher release artifacts (win-x64 / win-arm64).

.DESCRIPTION
For every runtime identifier this produces, in the output folder:

  WPF (default):
    release_{version}_{rid}.zip            the app payload (framework-dependent single-file)
    updater_{rid}.zip                      the standalone Updater.exe

  Avalonia (-App Avalonia):
    release_avalonia_{version}_{rid}.zip   the app payload (self-contained)
    updater_avalonia_{rid}.zip             the Avalonia updater (exe + dll + deps + runtimeconfig)

The Avalonia assets carry the "avalonia_" prefix because both apps are attached to the
same GitHub release; without it the WPF and Avalonia packages would overwrite each other
and each app would download the other app's payload.

The other architecture's bundled tools are pruned from each payload:
  win-x64   : drops *_arm64 files and tools/FindRomCover/arm64
  win-arm64 : drops *_x64 files, tools/FindRomCover/x64, and the unsuffixed
              executables that have an _arm64 sibling (7z.dll is kept)
Both RIDs drop the Linux-only extension-less RetroAchievementsSharp binaries.

The version is validated against the canonical SimpleLauncher.csproj (plus the app's own
csproj/manifest and SimpleLauncher.Core.csproj) before publishing.

.PARAMETER Version
The release version, e.g. 5.7.0. Must match the csproj/manifest values.

.PARAMETER App
Which application to package: Wpf (default) or Avalonia.

.PARAMETER RuntimeIdentifiers
RIDs to package. Defaults to win-x64 and win-arm64.

.PARAMETER OutputDir
Where the zips are written. Defaults to SimpleLauncher\bin\Release (WPF) or
SimpleLauncher.Avalonia\bin\Release (Avalonia).

.PARAMETER WorkDir
Scratch folder for the publish outputs. Defaults to artifacts\publish (WPF) or
artifacts\publish-avalonia (Avalonia).

.EXAMPLE
pwsh scripts/package-release.ps1 -Version 5.7.0
pwsh scripts/package-release.ps1 -Version 5.7.0 -App Avalonia
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet('Wpf', 'Avalonia')]
    [string]$App = 'Wpf',

    [string[]]$RuntimeIdentifiers = @('win-x64', 'win-arm64'),

    [string]$OutputDir,

    [string]$WorkDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

$repoDir = Split-Path -Parent $PSScriptRoot
$isAvalonia = $App -eq 'Avalonia'
$assetPrefix = if ($isAvalonia) { 'avalonia_' } else { '' }
$appProjectRelative = if ($isAvalonia) { 'SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj' } else { 'SimpleLauncher\SimpleLauncher.csproj' }
$appProject = Join-Path $repoDir $appProjectRelative
$updaterProjectRelative = if ($isAvalonia) { 'SimpleLauncher.Avalonia.Updater\SimpleLauncher.Avalonia.Updater.csproj' } else { 'SimpleLauncher.Updater\SimpleLauncher.Updater.csproj' }
$updaterProject = Join-Path $repoDir $updaterProjectRelative
$updaterExeName = if ($isAvalonia) { 'SimpleLauncher.Avalonia.Updater.exe' } else { 'Updater.exe' }
$targetFramework = if ($isAvalonia) { 'net10.0-windows' } else { $null }
$selfContained = if ($isAvalonia) { 'true' } else { 'false' }
$publishSingleFile = -not $isAvalonia

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoDir $(if ($isAvalonia) { 'SimpleLauncher.Avalonia\bin\Release' } else { 'SimpleLauncher\bin\Release' })
}
if (-not $WorkDir) {
    $WorkDir = Join-Path $repoDir $(if ($isAvalonia) { 'artifacts\publish-avalonia' } else { 'artifacts\publish' })
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

function Assert-Versions {
    param([Parameter(Mandatory = $true)][string]$Version)

    $canonicalVersion = Get-CsprojVersion (Join-Path $repoDir 'SimpleLauncher\SimpleLauncher.csproj')
    $coreVersion = Get-CsprojVersion (Join-Path $repoDir 'SimpleLauncher.Core\SimpleLauncher.Core.csproj')
    $appVersion = Get-CsprojVersion $appProject

    $manifestRelative = if ($isAvalonia) { 'SimpleLauncher.Avalonia\app.manifest' } else { 'SimpleLauncher\app.manifest' }
    $manifestPath = Join-Path $repoDir $manifestRelative
    $manifest = [xml](Get-Content -LiteralPath $manifestPath -Raw)
    $manifestVersion = $manifest.assembly.assemblyIdentity.GetAttribute('version')

    if ($canonicalVersion -ne $Version) {
        throw "Version mismatch: requested $Version but SimpleLauncher.csproj says $canonicalVersion."
    }
    if ($appVersion -ne $Version) {
        throw "Version mismatch: requested $Version but $appProjectRelative says $appVersion."
    }
    if ($coreVersion -ne $Version) {
        throw "Version mismatch: requested $Version but SimpleLauncher.Core.csproj says $coreVersion."
    }
    if ($manifestVersion -ne "$Version.0") {
        throw "Version mismatch: $manifestRelative says $manifestVersion, expected $Version.0."
    }

    if ($isAvalonia) {
        $updaterVersion = Get-CsprojVersion $updaterProject
        if ($updaterVersion -ne $Version) {
            throw "Version mismatch: requested $Version but $updaterProjectRelative says $updaterVersion."
        }
    }
    else {
        $updaterVersionPath = Join-Path $repoDir 'SimpleLauncher.Updater\version.txt'
        $updaterVersion = (Get-Content -LiteralPath $updaterVersionPath -Raw).Trim()
        if ($updaterVersion -ne "release$Version") {
            throw "Version mismatch: SimpleLauncher.Updater/version.txt says $updaterVersion, expected release$Version."
        }
    }

    Write-Host "Version $Version is consistent across csproj, manifest and updater metadata."
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

function Get-AvaloniaUpdaterPublishDir {
    param([Parameter(Mandatory = $true)][string]$Rid)

    return Join-Path $repoDir "SimpleLauncher.Avalonia.Updater\bin\Release\net10.0-windows\$Rid\publish"
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
    Write-Host "=== SimpleLauncher $App $Version ($rid) ==="

    if ($isAvalonia) {
        # The Avalonia app csproj copies the updater from its default publish folder into the
        # app publish output, so the updater must be published first and to its default path.
        $updaterPublishDir = Get-AvaloniaUpdaterPublishDir -Rid $rid

        Invoke-DotnetPublish @(
            'publish',
            $updaterProject,
            '-c', 'Release',
            '-f', $targetFramework,
            '-r', $rid,
            '--self-contained', $selfContained,
            '--nologo'
        )

        Invoke-DotnetPublish @(
            'publish',
            $appProject,
            '-c', 'Release',
            '-f', $targetFramework,
            '-r', $rid,
            '--self-contained', $selfContained,
            '--nologo',
            '-o', $appPublishDir
        )
    }
    else {
        $publishArguments = [System.Collections.Generic.List[string]]::new()
        $publishArguments.AddRange([string[]]@('publish', $appProject, '-c', 'Release', '-r', $rid))
        $publishArguments.Add('--self-contained')
        $publishArguments.Add($selfContained)
        if ($publishSingleFile) {
            $publishArguments.Add('-p:PublishSingleFile=true')
        }
        $publishArguments.Add('--nologo')
        $publishArguments.Add('-o')
        $publishArguments.Add($appPublishDir)

        Invoke-DotnetPublish -Arguments $publishArguments.ToArray()

        Invoke-DotnetPublish @(
            'publish',
            $updaterProject,
            '-c', 'Release',
            '-r', $rid,
            '--self-contained', $selfContained,
            '-p:PublishSingleFile=true',
            '--nologo',
            '-o', $updaterPublishDir
        )
    }

    Remove-OtherArchitectureFiles -PublishDir $appPublishDir -Rid $rid

    $releaseZip = Join-Path $OutputDir "release_${assetPrefix}${Version}_${rid}.zip"
    New-ZipArchive -SourceDir $appPublishDir -ZipPath $releaseZip
    [void]$produced.Add($releaseZip)

    if ($isAvalonia) {
        # Ship only the updater's own files: it runs with the self-contained runtime already
        # present in the application directory (same release), keeping the asset small.
        $updaterBaseName = 'SimpleLauncher.Avalonia.Updater'
        $updaterFiles = @(
            (Join-Path $updaterPublishDir "$updaterBaseName.exe"),
            (Join-Path $updaterPublishDir "$updaterBaseName.dll"),
            (Join-Path $updaterPublishDir "$updaterBaseName.deps.json"),
            (Join-Path $updaterPublishDir "$updaterBaseName.runtimeconfig.json")
        )
        foreach ($updaterFile in $updaterFiles) {
            if (-not (Test-Path -LiteralPath $updaterFile)) {
                throw "Expected updater file was not produced: $updaterFile"
            }
        }
    }
    else {
        $updaterExe = Join-Path $updaterPublishDir $updaterExeName
        if (-not (Test-Path -LiteralPath $updaterExe)) {
            throw "$updaterExeName was not produced at $updaterExe."
        }
        $updaterFiles = @($updaterExe)
    }

    $updaterZip = Join-Path $OutputDir "updater_${assetPrefix}${rid}.zip"
    New-FilesZipArchive -FilePaths $updaterFiles -ZipPath $updaterZip
    [void]$produced.Add($updaterZip)
}

Write-Host ""
Write-Host "Packages written to $OutputDir :"
foreach ($path in $produced) {
    $size = [math]::Round((Get-Item -LiteralPath $path).Length / 1MB, 1)
    Write-Host ("  {0} ({1} MB)" -f (Split-Path -Leaf $path), $size)
}
