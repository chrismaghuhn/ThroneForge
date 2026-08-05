[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$GamePath,
    [Parameter(Mandatory = $true)] [string]$ExperimentRoot,
    [Parameter(Mandatory = $true)] [string]$BepInExArchive,
    [Parameter(Mandatory = $true)] [string]$ExpectedFingerprint,
    [Parameter(Mandatory = $true)] [string]$ExpectedBepInExDigest,
    [Parameter(Mandatory = $true)] [string]$PackageRoot,
    [Parameter(Mandatory = $true)] [string]$ManifestPath,
    [Parameter(Mandatory = $true)] [string]$UnityAssemblyPath,
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
    @($BepInExArchive, 'BepInExArchive'),
    @($PackageRoot, 'PackageRoot'),
    @($ManifestPath, 'ManifestPath'),
    @($UnityAssemblyPath, 'UnityAssemblyPath')
)) {
    Require-AbsolutePath $item[0] $item[1]
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    throw 'The .NET SDK executable could not be located.'
}

$arguments = @(
    'run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.PluginSmokeTest'),
    '-c', 'Release', '--no-build', '--', 'run-lifecycle-experiment',
    '--repository-root', $repositoryRoot,
    '--original-game', $GamePath,
    '--experiment-root', $ExperimentRoot,
    '--expected-fingerprint', $ExpectedFingerprint,
    '--bepinex-archive', $BepInExArchive,
    '--official-digest', $ExpectedBepInExDigest,
    '--package-root', $PackageRoot,
    '--manifest-path', $ManifestPath,
    '--unity-assembly', $UnityAssemblyPath
)
if (-not [string]::IsNullOrWhiteSpace($Nonce)) {
    $arguments += @('--nonce', $Nonce)
}

& $dotnetCommand.Source @arguments
exit $LASTEXITCODE
