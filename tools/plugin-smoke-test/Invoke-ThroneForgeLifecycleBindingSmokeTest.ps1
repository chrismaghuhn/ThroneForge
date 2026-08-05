[CmdletBinding()]
param(
    [ValidateSet('Full', 'Rollback')]
    [string]$Mode = 'Full',
    [Parameter(Mandatory = $true)] [string]$GamePath,
    [Parameter(Mandatory = $true)] [string]$ExperimentRoot,
    [Parameter(Mandatory = $true)] [string]$BepInExArchive,
    [Parameter(Mandatory = $true)] [string]$ExpectedFingerprint,
    [Parameter(Mandatory = $true)] [string]$ExpectedBepInExDigest,
    [string]$PackageRoot,
    [string]$ManifestPath,
    [string]$UnityAssemblyPath,
    [string]$ExecutableRelativePath,
    [string]$Nonce
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Require-AbsolutePath([string]$PathValue, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($PathValue) -or -not [IO.Path]::IsPathRooted($PathValue)) {
        throw "$Label must be an explicit absolute path."
    }
}

foreach ($item in @(
    @($GamePath, 'GamePath'),
    @($ExperimentRoot, 'ExperimentRoot'),
    @($BepInExArchive, 'BepInExArchive')
)) {
    Require-AbsolutePath $item[0] $item[1]
}

if ($Mode -eq 'Full') {
    foreach ($item in @(
        @($PackageRoot, 'PackageRoot'),
        @($ManifestPath, 'ManifestPath'),
        @($UnityAssemblyPath, 'UnityAssemblyPath'),
        @($ExecutableRelativePath, 'ExecutableRelativePath')
    )) {
        if ([string]::IsNullOrWhiteSpace($item[0])) {
            throw "$($item[1]) is required in Full mode."
        }
        if ($item[1] -ne 'ExecutableRelativePath') {
            Require-AbsolutePath $item[0] $item[1]
        }
    }
}

$repositoryCommit = ((& git -C $repositoryRoot rev-parse HEAD 2>$null) | Out-String).Trim()
if ($repositoryCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'The repository HEAD is not a valid 40-character commit SHA.'
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    throw 'The .NET SDK executable could not be located.'
}

$operation = if ($Mode -eq 'Rollback') { 'rollback-lifecycle-experiment' } else { 'run-lifecycle-experiment' }
$arguments = @(
    'run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.PluginSmokeTest'),
    '-c', 'Release', '--no-build', '--', $operation,
    '--repository-root', $repositoryRoot,
    '--original-game', $GamePath,
    '--experiment-root', $ExperimentRoot,
    '--expected-fingerprint', $ExpectedFingerprint,
    '--bepinex-archive', $BepInExArchive,
    '--official-digest', $ExpectedBepInExDigest,
    '--dotnet-path', $dotnetCommand.Source
)
if ($Mode -eq 'Full') {
    $arguments += @(
        '--package-root', $PackageRoot,
        '--manifest-path', $ManifestPath,
        '--unity-assembly', $UnityAssemblyPath,
        '--executable-relative-path', $ExecutableRelativePath,
        '--repository-baseline-commit', $repositoryCommit
    )
}
if (-not [string]::IsNullOrWhiteSpace($Nonce)) {
    $arguments += @('--nonce', $Nonce)
}

& $dotnetCommand.Source @arguments
exit $LASTEXITCODE
