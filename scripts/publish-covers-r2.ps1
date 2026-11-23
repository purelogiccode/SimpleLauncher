#Requires -Version 7
<#
.SYNOPSIS
Uploads the staged cover catalog and PNG derivatives (alpha preserved) to Cloudflare R2.

.DESCRIPTION
Copies the output of scripts/build-cover-catalog.ps1 to:

  r2:<bucket>/Simple Launcher/covers/v1/catalog.json
  r2:<bucket>/Simple Launcher/covers/v1/<system-slug>/index.json
  r2:<bucket>/Simple Launcher/covers/v1/<system-slug>/<canonical-name>.png

Images are uploaded first, system indexes second, and catalog.json last, so a client
never sees an index that points at an object that is not there yet. The script uses
rclone copy (never sync --delete); the only removal is the explicit opt-in
-RemoveLegacyJpegs cleanup, which runs after the PNG catalog is publicly verified.

Public URLs are served from:
  https://assets.purelogiccode.com/Simple%20Launcher/covers/v1/

.PARAMETER StageDir
Folder produced by scripts/build-cover-catalog.ps1.

.PARAMETER Remote
rclone remote name (default r2:).

.PARAMETER Bucket
R2 bucket (default simplelauncher-thumbnails).

.PARAMETER Prefix
Object prefix inside the bucket (default "Simple Launcher/covers/v1").

.PARAMETER PublicBaseUrl
Public base URL used for verification.

.PARAMETER DryRun
Print and validate the upload plan without writing to R2.

.PARAMETER SkipVerify
Skip the public HEAD checks after upload.

.PARAMETER Transfers
Parallel file transfers (default 32).

.PARAMETER Checkers
Parallel listing/checker workers (default 64).

.PARAMETER IgnoreExisting
Skip objects already present instead of comparing checksums. Useful for resuming an interrupted upload.

.PARAMETER SkipImages
Skip the image pass (for example after uploading images in batches).

.PARAMETER RemoveLegacyJpegs
After the PNG catalog is uploaded and publicly verified, delete the old `.jpg`/`.jpeg`
objects under the same prefix. This is the only destructive operation in the script and is
explicitly opt-in.

.PARAMETER FastList
Use rclone's fast-list for the S3/R2 backend.

.EXAMPLE
pwsh scripts/publish-covers-r2.ps1 -StageDir artifacts/covers-stage -DryRun

.EXAMPLE
pwsh scripts/publish-covers-r2.ps1 -StageDir artifacts/covers-stage
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StageDir,

    [string]$Remote = 'r2:',

    [string]$Bucket = 'simplelauncher-thumbnails',

    [string]$Prefix = 'Simple Launcher/covers/v1',

    [string]$PublicBaseUrl = 'https://assets.purelogiccode.com/Simple%20Launcher/covers/v1',

    [switch]$DryRun,

    [switch]$SkipVerify,

    [ValidateRange(1, 128)]
    [int]$Transfers = 32,

    [ValidateRange(1, 256)]
    [int]$Checkers = 64,

    [switch]$IgnoreExisting,

    [switch]$SkipImages,

    [switch]$RemoveLegacyJpegs,

    [switch]$FastList
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $StageDir -PathType Container)) {
    throw "The staging folder does not exist: $StageDir"
}
$StageDir = [System.IO.Path]::GetFullPath($StageDir)

$catalogPath = Join-Path $StageDir 'catalog.json'
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "catalog.json is missing from the staging folder: $catalogPath"
}

$indexFiles = @(Get-ChildItem -LiteralPath $StageDir -Recurse -File -Filter 'index.json' -ErrorAction SilentlyContinue)
if ($indexFiles.Count -eq 0) {
    throw "No <system>/index.json files were found in the staging folder: $StageDir"
}

$imageFiles = @(Get-ChildItem -LiteralPath $StageDir -Recurse -File -Filter '*.png' -ErrorAction SilentlyContinue)
if ($imageFiles.Count -eq 0) {
    throw "No PNG derivatives were found in the staging folder: $StageDir"
}

$rcloneCommand = Get-Command rclone -ErrorAction SilentlyContinue
$rclonePath = if ($null -ne $rcloneCommand) {
    $rcloneCommand.Source
} elseif (Test-Path -LiteralPath 'C:\rclone\rclone.exe' -PathType Leaf) {
    'C:\rclone\rclone.exe'
} else {
    $null
}
if ($null -eq $rclonePath) {
    throw 'rclone was not found. Install rclone or put rclone.exe on PATH.'
}

$Remote = $Remote.TrimEnd('/')
if (-not $Remote.EndsWith(':')) { $Remote += ':' }
$Prefix = $Prefix.Trim('/')
$PublicBaseUrl = $PublicBaseUrl.TrimEnd('/')
$remoteRoot = "$Remote$Bucket/$Prefix"

Write-Host 'Cover catalog upload plan'
Write-Host "  Stage:   $StageDir"
Write-Host "  Covers:  $($imageFiles.Count) PNG files"
Write-Host "  Indexes: $($indexFiles.Count)"
Write-Host "  Remote:  $remoteRoot"
Write-Host "  Public:  $PublicBaseUrl/"
Write-Host "  Workers: $Transfers transfers / $Checkers checkers"
if ($IgnoreExisting) { Write-Host '  Mode:    skip existing images (resume)' }
if ($SkipImages) { Write-Host '  Mode:    skip image pass' }
if ($RemoveLegacyJpegs) { Write-Host '  Cleanup: delete legacy .jpg/.jpeg objects after verification' }
Write-Host ''

function Invoke-Rclone {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $effective = @($Arguments)
    # R2 occasionally returns a transient 502 on a single PutObject; retry the file and
    # the HTTP request instead of failing the whole 121k-object upload.
    if ($effective -notcontains '--retries') { $effective += @('--retries', '5') }
    if ($effective -notcontains '--low-level-retries') { $effective += @('--low-level-retries', '10') }
    if ($DryRun) { $effective += '--dry-run' }

    Write-Host ("rclone " + ($effective -join ' '))
    & $rclonePath @effective
    if ($LASTEXITCODE -ne 0) {
        throw "rclone failed with exit code $LASTEXITCODE."
    }
}

# 1. Images first. They are immutable once published; a changed image gets a new upload
#    with an updated index pointing at it.
$imageArguments = @(
    'copy', $StageDir, $remoteRoot,
    '--exclude', '*/index.json',
    '--exclude', 'catalog.json',
    '--exclude', 'report.md',
    '--exclude', '*.jpg',
    '--exclude', '*.jpeg',
    '--exclude', '*.webp',
    '--transfers', $Transfers,
    '--checkers', $Checkers,
    '--s3-no-check-bucket',
    '--header-upload', 'Content-Type: image/png',
    '--header-upload', 'Cache-Control: public, max-age=86400',
    '--stats', '30s',
    '--stats-one-line'
)
if ($IgnoreExisting) {
    $imageArguments += '--ignore-existing'
} else {
    $imageArguments += '--checksum'
}
if ($FastList) { $imageArguments += '--fast-list' }
if (-not $SkipImages) { Invoke-Rclone -Arguments $imageArguments }

# 2. Per-system indexes. Short cache: an index can change when a cover is added or aliases
#    are updated, and clients revalidate it.
$indexArguments = @(
    'copy', $StageDir, $remoteRoot,
    '--include', '*/index.json',
    '--transfers', $Transfers,
    '--checkers', $Checkers,
    '--checksum',
    '--s3-no-check-bucket',
    '--header-upload', 'Content-Type: application/json',
    '--header-upload', 'Cache-Control: public, max-age=300',
    '--stats', '30s',
    '--stats-one-line'
)
if ($FastList) { $indexArguments += '--fast-list' }
Invoke-Rclone -Arguments $indexArguments

# 3. The catalog is the commit point.
$catalogArguments = @(
    'copyto', $catalogPath, "$remoteRoot/catalog.json",
    '--checksum',
    '--s3-no-check-bucket',
    '--header-upload', 'Content-Type: application/json',
    '--header-upload', 'Cache-Control: public, max-age=300'
)
Invoke-Rclone -Arguments $catalogArguments

if ($DryRun) {
    Write-Host ''
    Write-Host 'Dry run complete; nothing was written to R2.'
    return
}

if ($SkipVerify) {
    Write-Host ''
    Write-Host 'Upload complete; public verification skipped.'
    return
}

function Get-PublicUrl {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $encoded = ($RelativePath.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    return "$PublicBaseUrl/$encoded"
}

function Test-PublicObject {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$ExpectedContentType
    )

    $response = Invoke-WebRequest -Uri $Url -Method Head -UseBasicParsing
    if ($response.StatusCode -ne 200) {
        throw "Public verification failed for $Url (HTTP $($response.StatusCode))."
    }

    $contentType = [string]$response.Headers['Content-Type']
    if ($contentType -and -not $contentType.StartsWith($ExpectedContentType, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected Content-Type for $Url`: $contentType (expected $ExpectedContentType)."
    }

    Write-Host "  verified $Url ($contentType)"
}

$firstIndex = $indexFiles | Select-Object -First 1
$firstImage = $imageFiles | Select-Object -First 1
$indexRelative = [System.IO.Path]::GetRelativePath($StageDir, $firstIndex.FullName).Replace('\', '/')
$imageRelative = [System.IO.Path]::GetRelativePath($StageDir, $firstImage.FullName).Replace('\', '/')

Write-Host ''
Write-Host 'Verifying public objects...'
Test-PublicObject -Url (Get-PublicUrl 'catalog.json') -ExpectedContentType 'application/json'
Test-PublicObject -Url (Get-PublicUrl $indexRelative) -ExpectedContentType 'application/json'
Test-PublicObject -Url (Get-PublicUrl $imageRelative) -ExpectedContentType 'image/png'

if ($RemoveLegacyJpegs) {
    Write-Host ''
    Write-Host 'Removing legacy JPEG derivatives (PNG catalog is verified)...'
    $deleteArguments = @(
        'delete', $remoteRoot,
        '--include', '*.jpg',
        '--include', '*.jpeg',
        '--s3-no-check-bucket',
        '--stats', '30s',
        '--stats-one-line'
    )
    if ($FastList) { $deleteArguments += '--fast-list' }
    Invoke-Rclone -Arguments $deleteArguments
}

Write-Host ''
Write-Host 'Upload complete.'
Write-Host "  Catalog: $PublicBaseUrl/catalog.json"
Write-Host "  Sample:  $(Get-PublicUrl $imageRelative)"
