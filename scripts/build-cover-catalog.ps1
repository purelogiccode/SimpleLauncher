#Requires -Version 7
<#
.SYNOPSIS
Builds the canonical Simple Launcher cover catalog and 800px JPEG derivatives.

.DESCRIPTION
Scans a local Simple Launcher images folder, reuses the app's annotation stripping
(FindCoverImageService.StripAnnotations), keeps one canonical cover per normalized game,
stores every variant name as an alias, and writes:

  <output>/catalog.json
  <output>/<system-slug>/index.json
  <output>/<system-slug>/<canonical-name>.jpg
  <output>/report.md

The output is a staging folder only. Nothing is uploaded by this script; use
scripts/publish-covers-r2.ps1 after reviewing the report and the R2 destination.

.PARAMETER Source
Local images folder, for example D:\Emulators\Simple Launcher\images.

.PARAMETER Output
Staging folder. Defaults to artifacts\covers-stage.

.PARAMETER Systems
Optional system-name filter (comma/semicolon separated or repeated).

.PARAMETER ExcludeSystem
Optional system names to skip (repeatable).

.PARAMETER MaxSize
Longest side of the derivative in pixels. Defaults to 800.

.PARAMETER Quality
JPEG quality 1-100. Defaults to 85.

.PARAMETER Force
Re-encode derivatives that already exist in the staging folder.

.EXAMPLE
pwsh scripts/build-cover-catalog.ps1 -Source "D:\Emulators\Simple Launcher\images"

.EXAMPLE
pwsh scripts/build-cover-catalog.ps1 -Source "D:\Emulators\Simple Launcher\images" -Systems "Atari 5200" -Force
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [string]$Output,

    [string[]]$Systems = @(),

    [string[]]$ExcludeSystem = @(),

    [ValidateRange(64, 4096)]
    [int]$MaxSize = 800,

    [ValidateRange(1, 100)]
    [int]$Quality = 85,

    [switch]$Force,

    [switch]$KeepStale
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoDir 'Tools/CoverCatalogBuilder/CoverCatalogBuilder.csproj'

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "The source images folder does not exist: $Source"
}
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "The cover catalog builder project is missing: $project"
}

if (-not $Output) {
    $Output = Join-Path $repoDir 'artifacts/covers-stage'
}
$Output = [System.IO.Path]::GetFullPath($Output)

$arguments = @(
    'run',
    '--project', $project,
    '-c', 'Release',
    '--',
    '--source', ([System.IO.Path]::GetFullPath($Source)),
    '--output', $Output,
    '--max-size', $MaxSize,
    '--quality', $Quality
)

foreach ($system in $Systems) {
    if (-not [string]::IsNullOrWhiteSpace($system)) {
        $arguments += @('--systems', $system)
    }
}
foreach ($system in $ExcludeSystem) {
    if (-not [string]::IsNullOrWhiteSpace($system)) {
        $arguments += @('--exclude-system', $system)
    }
}
if ($Force) {
    $arguments += '--force'
}
if (-not $KeepStale) {
    $arguments += '--clean'
}

Write-Host 'Building the cover catalog...'
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Cover catalog builder failed with exit code $LASTEXITCODE."
}

$catalogPath = Join-Path $Output 'catalog.json'
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "The builder finished without producing catalog.json: $catalogPath"
}

$images = Get-ChildItem -LiteralPath $Output -Recurse -File -Filter '*.jpg' -ErrorAction SilentlyContinue
$bytes = ($images | Measure-Object Length -Sum).Sum
if ($null -eq $bytes) { $bytes = 0 }

Write-Host ''
Write-Host 'Cover catalog ready (staging only; nothing uploaded):'
Write-Host "  Catalog: $catalogPath"
Write-Host ("  Covers:  {0:N0} JPEG files, {1:N1} MiB" -f $images.Count, ($bytes / 1MB))
Write-Host "  Report:  $(Join-Path $Output 'report.md')"
Write-Host ''
Write-Host 'R2 destination used by publish-covers-r2.ps1:'
Write-Host '  Bucket: simplelauncher-thumbnails'
Write-Host '  Prefix: Simple Launcher/covers/v1'
Write-Host '  Public: https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/'
Write-Host ''
Write-Host "Upload with: pwsh scripts/publish-covers-r2.ps1 -StageDir `"$Output`" -DryRun"
