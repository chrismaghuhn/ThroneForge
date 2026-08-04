[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Plan', 'Prepare', 'Baseline', 'Install', 'Launch', 'Verify', 'Rollback', 'Full', 'Resume', 'Cleanup')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [string]$GamePath,

    [Parameter(Mandatory = $true)]
    [string]$ExperimentRoot,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedFingerprint,

    [string]$BepInExArchivePath,

    [switch]$AllowDownload,

    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not [IO.Path]::IsPathRooted($GamePath) -or -not [IO.Path]::IsPathRooted($ExperimentRoot)) {
    throw 'GamePath and ExperimentRoot must be explicit absolute paths.'
}

$archiveDigest = $null
$assetId = $null
$assetSize = $null
if ([string]::IsNullOrWhiteSpace($BepInExArchivePath) -and $AllowDownload -and -not $WhatIf) {
    $release = (& gh api 'repos/BepInEx/BepInEx/releases/tags/v5.4.23.5' | ConvertFrom-Json)
    if ($release.html_url -notmatch 'github\.com/BepInEx/BepInEx/releases/tag/v5\.4\.23\.5' -or $release.tag_name -ne 'v5.4.23.5') {
        throw 'Official BepInEx release metadata did not match the required repository and tag.'
    }

    $asset = @($release.assets | Where-Object { $_.name -eq 'BepInEx_win_x64_5.4.23.5.zip' })
    if ($asset.Count -ne 1) {
        throw 'The official release did not expose exactly the required Windows x64 asset.'
    }

    $downloadRoot = Join-Path $ExperimentRoot 'downloads'
    New-Item -ItemType Directory -Force -Path $downloadRoot | Out-Null
    $BepInExArchivePath = Join-Path $downloadRoot $asset[0].name
    Invoke-WebRequest -Uri $asset[0].browser_download_url -OutFile $BepInExArchivePath
    $assetId = [string]$asset[0].id
    $assetSize = [string]$asset[0].size
    if ($asset[0].digest -match '^sha256:(?<hash>[0-9a-fA-F]{64})$') {
        $archiveDigest = $Matches['hash'].ToLowerInvariant()
    }

    $metadataPath = Join-Path (Join-Path $ExperimentRoot 'manifests') 'official-release.json'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $metadataPath) | Out-Null
    [ordered]@{
        repository = $release.html_url
        tag = $release.tag_name
        publishedAt = $release.published_at
        assetName = $asset[0].name
        assetId = $assetId
        size = $assetSize
        officialDigest = if ($archiveDigest) { "sha256:$archiveDigest" } else { $null }
    } | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding utf8
}

if (-not [string]::IsNullOrWhiteSpace($BepInExArchivePath)) {
    $BepInExArchivePath = [IO.Path]::GetFullPath($BepInExArchivePath)
}

$preferredDotnet = 'C:\Program Files (x86)\dotnet\dotnet.exe'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if (Test-Path -LiteralPath $preferredDotnet) {
    $preferredDotnet
} elseif ($dotnetCommand) {
    $dotnetCommand.Path
} else {
    $null
}
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $fallback = 'C:\Program Files (x86)\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $fallback) {
        $dotnet = $fallback
    } else {
        throw 'dotnet was not found. Install the repository-pinned SDK or provide it on PATH.'
    }
}

$assembly = Join-Path $repositoryRoot 'artifacts\bin\ThroneForge.LoaderSmokeTest\Release\net10.0\ThroneForge.LoaderSmokeTest.dll'
$arguments = @(
    'exec', $assembly, $Mode,
    '--game-path', $GamePath,
    '--experiment-root', $ExperimentRoot,
    '--expected-fingerprint', $ExpectedFingerprint,
    '--repository-root', $repositoryRoot
)
if (-not [string]::IsNullOrWhiteSpace($BepInExArchivePath)) {
    $arguments += @('--bepinex-archive', $BepInExArchivePath)
}
if ($archiveDigest) {
    $arguments += @('--official-digest', $archiveDigest)
}
if ($assetId) {
    $arguments += @('--official-asset-id', $assetId)
}
if ($assetSize) {
    $arguments += @('--official-asset-size', $assetSize)
}
if ($WhatIf) {
    $arguments += '--what-if'
}

& $dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Loader smoke-test mode failed with exit code $LASTEXITCODE."
}
