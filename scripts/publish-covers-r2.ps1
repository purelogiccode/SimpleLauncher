#Requires -Version 7
<#
.SYNOPSIS
Uploads the staged cover catalog and JPEG derivatives to Cloudflare R2.

.DESCRIPTION
Copies the output of scripts/build-cover-catalog.ps1 to:

  r2:<bucket>/Simple Launcher/covers/v1/catalog.json
  r2:<bucket>/Simple Launcher/covers/v1/<system-slug>/index.json
  r2:<bucket>/Simple Launcher/covers/v1/<system-slug>/<canonical-name>.jpg

Images are uploaded first, system indexes second, and catalog.json last, so a client
never sees an index that points at an object that is not there yet. The script uses
rclone copy (never sync --delete), so existing R2 objects are never removed.

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

    [switch]$SkipVerify
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

$imageFiles = @(Get-ChildItem -LiteralPath $StageDir -Recurse -File -Filter '*.jpg' -ErrorAction SilentlyContinue)
if ($imageFiles.Count -eq 0) {
    throw "No JPEG derivatives were found in the staging folder: $StageDir"
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
Write-Host "  Covers:  $($imageFiles.Count) JPEG files"
Write-Host "  Indexes: $($indexFiles.Count)"
Write-Host "  Remote:  $remoteRoot"
Write-Host "  Public:  $PublicBaseUrl/"
Write-Host ''

function Invoke-Rclone {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $effective = @($Arguments)
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
    '--transfers', '16',
    '--checkers', '32',
    '--checksum',
    '--s3-no-check-bucket',
    '--header-upload', 'Content-Type: image/jpeg',
    '--header-upload', 'Cache-Control: public, max-age=86400',
    '--stats', '30s',
    '--stats-one-line'
)
Invoke-Rclone -Arguments $imageArguments

# 2. Per-system indexes. Short cache: an index can change when a cover is added or aliases
#    are updated, and clients revalidate it.
$indexArguments = @(
    'copy', $StageDir, $remoteRoot,
    '--include', '*/index.json',
    '--transfers', '16',
    '--checkers', '32',
    '--checksum',
    '--s3-no-check-bucket',
    '--header-upload', 'Content-Type: application/json',
    '--header-upload', 'Cache-Control: public, max-age=300',
    '--stats', '30s',
    '--stats-one-line'
)
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
Test-PublicObject -Url (Get-PublicUrl $imageRelative) -ExpectedContentType 'image/jpeg'

Write-Host ''
Write-Host 'Upload complete.'
Write-Host "  Catalog: $PublicBaseUrl/catalog.json"
Write-Host "  Sample:  $(Get-PublicUrl $imageRelative)"
