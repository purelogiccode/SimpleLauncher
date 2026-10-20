#Requires -Version 7
<#
.SYNOPSIS
Publishes a SimpleLauncher release to the Cloudflare R2 download fallback.

.DESCRIPTION
Uploads the four release ZIPs and four updater ZIPs to the R2 bucket behind
https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/, verifies that
every object is publicly readable, and only then updates version.txt.

The in-app update check reads version.txt first and then downloads
release_{version}_{rid}.zip / updater_{rid}.zip from the same prefix. Uploading the
bundles before version.txt prevents clients from seeing a version whose assets are
not available yet.

Credentials are read from the process environment (R2_ACCOUNT_ID,
R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET, optional R2_PREFIX and
R2_PUBLIC_BASE_URL) or, when absent, from the R2_* block in AGENTS.local.md
(gitignored). Values from the environment take precedence.

.PARAMETER Version
Release version in Major.Minor.Patch form, e.g. 5.8.4.

.PARAMETER ReleaseDir
Folder containing the eight ZIPs. Defaults to artifacts\release-{version}, then
SimpleLauncher\bin\Release.

.PARAMETER CredentialFile
File with the R2_* key=value block. Defaults to AGENTS.local.md in the repo root.

.PARAMETER PublicBaseUrl
Public fallback URL. Defaults to R2_PUBLIC_BASE_URL from the credential file.

.PARAMETER SkipPublicVerification
Skip the post-upload public HEAD/GET checks. Not recommended.

.PARAMETER DryRun
Print the planned uploads without touching R2.

.PARAMETER VerifyOnly
Skip the uploads and verify the currently published assets and version.txt.

.EXAMPLE
pwsh scripts/publish-fallback-r2.ps1 -Version 5.8.4

.EXAMPLE
pwsh scripts/publish-fallback-r2.ps1 -Version 5.8.4 -ReleaseDir artifacts/release-5.8.4 -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$ReleaseDir,

    [string]$CredentialFile,

    [string]$PublicBaseUrl,

    [switch]$SkipPublicVerification,

    [switch]$DryRun,

    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($DryRun -and $VerifyOnly) {
    throw 'Use either -DryRun or -VerifyOnly, not both.'
}

$repoDir = Split-Path -Parent $PSScriptRoot

# ---------------------------------------------------------------------------
# Locate and validate the release payload
# ---------------------------------------------------------------------------
if (-not $ReleaseDir) {
    $candidates = @(
        (Join-Path $repoDir "artifacts\release-$Version"),
        (Join-Path $repoDir 'SimpleLauncher\bin\Release')
    )

    $ReleaseDir = $candidates | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ "release_${Version}_win-x64.zip") -PathType Leaf
    } | Select-Object -First 1

    if (-not $ReleaseDir) {
        throw "No release folder containing release_${Version}_win-x64.zip was found. Use -ReleaseDir."
    }
}

$ReleaseDir = [IO.Path]::GetFullPath($ReleaseDir)

$requiredAssets = [System.Collections.Generic.List[string]]::new()
foreach ($rid in @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64')) {
    $requiredAssets.Add("release_${Version}_$rid.zip")
    $requiredAssets.Add("updater_$rid.zip")
}

foreach ($asset in $requiredAssets) {
    $path = Join-Path $ReleaseDir $asset
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing release asset: $path"
    }

    $length = (Get-Item -LiteralPath $path).Length
    if ($length -lt 1MB) {
        throw "Release asset looks too small ($length bytes): $path"
    }
}

# ---------------------------------------------------------------------------
# Credentials: environment first, then the gitignored AGENTS.local.md block
# ---------------------------------------------------------------------------
if (-not $CredentialFile) {
    $CredentialFile = Join-Path $repoDir 'AGENTS.local.md'
}
$CredentialFile = [IO.Path]::GetFullPath($CredentialFile)

$config = @{}
foreach ($name in @('R2_ACCOUNT_ID', 'R2_ACCESS_KEY_ID', 'R2_SECRET_ACCESS_KEY', 'R2_BUCKET',
        'R2_PREFIX', 'R2_PUBLIC_BASE_URL')) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        $config[$name] = $value.Trim()
    }
}

if (Test-Path -LiteralPath $CredentialFile -PathType Leaf) {
    $pattern = '^\s*(R2_[A-Z0-9_]+)\s*=\s*(.*?)\s*$'
    foreach ($line in [IO.File]::ReadLines($CredentialFile)) {
        if ($line -match $pattern) {
            $name = $Matches[1]
            $value = $Matches[2]
            if (-not $config.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace($value)) {
                $config[$name] = $value.Trim()
            }
        }
    }
}

foreach ($name in @('R2_ACCOUNT_ID', 'R2_ACCESS_KEY_ID', 'R2_SECRET_ACCESS_KEY', 'R2_BUCKET')) {
    if (-not $config.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($config[$name])) {
        throw "Missing $name. Set the environment variable or add it to $CredentialFile."
    }
}

if (-not $config.ContainsKey('R2_PREFIX')) {
    $config['R2_PREFIX'] = ''
}

$prefix = $config['R2_PREFIX'].Trim('/')
if ($prefix) {
    $prefix += '/'
}

if (-not $PublicBaseUrl) {
    if (-not $config.ContainsKey('R2_PUBLIC_BASE_URL') -or
        [string]::IsNullOrWhiteSpace($config['R2_PUBLIC_BASE_URL'])) {
        throw "Missing R2_PUBLIC_BASE_URL. Set the environment variable or add it to $CredentialFile."
    }

    $PublicBaseUrl = $config['R2_PUBLIC_BASE_URL']
}
$PublicBaseUrl = $PublicBaseUrl.TrimEnd('/') + '/'

$curl = Get-Command curl.exe -ErrorAction SilentlyContinue
if (-not $curl) {
    $curl = Get-Command curl -ErrorAction SilentlyContinue
}
if (-not $curl) {
    throw 'curl was not found. It is required for AWS SigV4 uploads to R2.'
}

$accountId = $config['R2_ACCOUNT_ID']
$bucket = $config['R2_BUCKET']
$accessKey = $config['R2_ACCESS_KEY_ID']
$secretKey = $config['R2_SECRET_ACCESS_KEY']

function Get-R2ObjectUrl {
    param([Parameter(Mandatory = $true)][string]$Key)

    $encodedKey = ($Key.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    return "https://$accountId.r2.cloudflarestorage.com/$bucket/$encodedKey"
}

function Get-PublicAssetUrl {
    param([Parameter(Mandatory = $true)][string]$AssetName)

    return $PublicBaseUrl + [Uri]::EscapeDataString($AssetName)
}

function Invoke-R2Put {
    param(
        [Parameter(Mandatory = $true)][string]$LocalPath,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$ContentType
    )

    if ($DryRun) {
        Write-Host "[dry-run] PUT $Key"
        return
    }

    $url = Get-R2ObjectUrl -Key $Key
    $sizeMb = (Get-Item -LiteralPath $LocalPath).Length / 1MB
    $maxAttempts = 5

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $output = & $curl.Source -sS --fail-with-body --aws-sigv4 'aws:amz:auto:s3' `
            --user "${accessKey}:${secretKey}" -X PUT -H "Content-Type: $ContentType" `
            --data-binary "@$LocalPath" -w "`n%{http_code}" $url 2>&1

        $exitCode = $LASTEXITCODE
        $lines = @($output)
        $status = if ($lines.Count -gt 0) { [string]$lines[-1] } else { '' }
        $body = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { '' }
        if ($body.Length -gt 600) {
            $body = $body.Substring(0, 600) + '...'
        }

        if ($exitCode -eq 0 -and $status -match '^2\d\d$') {
            Write-Host ("  uploaded {0} ({1:N1} MB)" -f $Key, $sizeMb)
            return
        }

        if ($attempt -ge $maxAttempts) {
            throw "Upload failed for $Key (HTTP $status): $body"
        }

        Write-Warning ("Upload attempt {0}/{1} failed for {2} (HTTP {3}): {4}" -f
            $attempt, $maxAttempts, $Key, $status, $body)
        Start-Sleep -Seconds (2 * $attempt)
    }
}

function Test-PublicAsset {
    param(
        [Parameter(Mandatory = $true)][string]$AssetName,
        [Parameter(Mandatory = $true)][long]$ExpectedLength
    )

    if ($SkipPublicVerification -or $DryRun) {
        return
    }

    $url = Get-PublicAssetUrl -AssetName $AssetName
    $maxAttempts = 5

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $head = & $curl.Source -sS -I $url 2>&1
        $exitCode = $LASTEXITCODE
        $statusLine = @($head | Where-Object { $_ -match '^HTTP/' } | Select-Object -Last 1)
        $lengthLine = @($head | Where-Object { $_ -match '^Content-Length:' } | Select-Object -Last 1)

        $status = if ([string]$statusLine -match ' (\d{3})') { $Matches[1] } else { '' }
        $remoteLength = if ([string]$lengthLine -match '^Content-Length:\s*(\d+)') { [long]$Matches[1] } else { -1 }

        if ($exitCode -eq 0 -and $status -match '^2\d\d$' -and $remoteLength -eq $ExpectedLength) {
            Write-Host ("  verified {0} ({1:N0} bytes)" -f $AssetName, $ExpectedLength)
            return
        }

        if ($attempt -ge $maxAttempts) {
            throw ("Public verification failed for {0} (HTTP {1}, length {2}, expected {3})." -f
                $url, $status, $remoteLength, $ExpectedLength)
        }

        Start-Sleep -Seconds (2 * $attempt)
    }
}

# ---------------------------------------------------------------------------
# Publish: bundles first, version.txt last
# ---------------------------------------------------------------------------
$prefixDisplay = if ($prefix) { $prefix } else { '<bucket root>' }
Write-Host "Publishing SimpleLauncher $Version to R2"
Write-Host "  release dir: $ReleaseDir"
Write-Host "  bucket:      $bucket"
Write-Host "  prefix:      $prefixDisplay"
Write-Host "  public base: $PublicBaseUrl"
if ($DryRun) {
    Write-Host '  mode:        DRY RUN'
}
elseif ($VerifyOnly) {
    Write-Host '  mode:        VERIFY ONLY'
}

foreach ($asset in $requiredAssets) {
    $localPath = Join-Path $ReleaseDir $asset
    $objectKey = $prefix + $asset
    $localLength = (Get-Item -LiteralPath $localPath).Length
    if (-not $VerifyOnly) {
        Invoke-R2Put -LocalPath $localPath -Key $objectKey -ContentType 'application/zip'
    }
    Test-PublicAsset -AssetName $asset -ExpectedLength $localLength
}

# version.txt is the switch clients read first: update it only after every bundle
# has been uploaded and verified.
$versionContent = "release$Version"
if (-not $VerifyOnly) {
    $versionTemp = Join-Path ([IO.Path]::GetTempPath()) "simplelauncher-version-$Version.txt"
    try {
        [IO.File]::WriteAllText($versionTemp, $versionContent)
        Invoke-R2Put -LocalPath $versionTemp -Key ($prefix + 'version.txt') -ContentType 'text/plain'
    }
    finally {
        Remove-Item -LiteralPath $versionTemp -Force -ErrorAction SilentlyContinue
    }
}

if (-not $SkipPublicVerification -and -not $DryRun) {
    $versionUrl = Get-PublicAssetUrl -AssetName 'version.txt'
    $maxAttempts = 5
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $content = (& $curl.Source -sS --fail $versionUrl 2>&1) -join ''
        if ($LASTEXITCODE -eq 0 -and $content.Trim() -ceq $versionContent) {
            Write-Host "  verified version.txt = $versionContent"
            break
        }

        if ($attempt -ge $maxAttempts) {
            throw "Public version.txt verification failed (expected '$versionContent', got '$($content.Trim())')."
        }

        Start-Sleep -Seconds (2 * $attempt)
    }
}

if ($DryRun) {
    Write-Host 'Dry run complete; no objects were uploaded.'
}
elseif ($VerifyOnly) {
    Write-Host "Fallback verification succeeded: $($PublicBaseUrl)version.txt = $versionContent"
}
else {
    Write-Host "Fallback published successfully: $($PublicBaseUrl)version.txt = $versionContent"
}
