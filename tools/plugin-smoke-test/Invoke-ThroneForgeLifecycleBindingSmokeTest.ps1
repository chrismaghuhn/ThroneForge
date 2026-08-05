[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Full', 'Rollback', 'Cleanup')]
    [string]$Mode = 'Full',
    [Parameter(Mandatory = $true)] [string]$GamePath,
    [Parameter(Mandatory = $true)] [string]$ExperimentRoot,
    [Parameter(Mandatory = $true)] [string]$BepInExArchive,
    [Parameter(Mandatory = $true)] [string]$ExpectedFingerprint,
    [Parameter(Mandatory = $true)] [string]$ExpectedBepInExDigest,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$expectedFingerprint = $ExpectedFingerprint.ToLowerInvariant()
$expectedArchiveDigest = $ExpectedBepInExDigest.ToLowerInvariant()
$script:isWindows = [OperatingSystem]::IsWindows()
$archiveName = 'BepInEx_win_x64_5.4.23.5.zip'
$loaderVersion = '5.4.23.5'
$pluginGuid = 'dev.throneforge.m1.lifecycle-smoke'
$pluginRoot = 'BepInEx/plugins/dev.throneforge.m1.lifecycle-smoke'
$bindingId = 'unity-application-quitting-v1'

function Fail-Safe([string]$message) { throw "Lifecycle smoke test failed: $message" }
function Require-Absolute([string]$path, [string]$label) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not [IO.Path]::IsPathRooted($path)) { Fail-Safe "$label must be an explicit absolute path." }
}
function Normalize([string]$path) { [IO.Path]::GetFullPath($path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) }
function Under([string]$root, [string]$candidate) {
    $r = Normalize $root; $c = Normalize $candidate
    $comparison = if ($script:isWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $c.Equals($r, $comparison) -or $c.StartsWith($r + [IO.Path]::DirectorySeparatorChar, $comparison)
}
function Get-Dotnet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    foreach ($candidate in @('C:\Program Files\dotnet\dotnet.exe', 'C:\Program Files (x86)\dotnet\dotnet.exe')) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    Fail-Safe 'The .NET SDK executable could not be located.'
}
function Invoke-Dotnet([string[]]$arguments, [switch]$AllowFailure) {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = (& $script:dotnet @arguments 2>&1 | Out-String).Trim()
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    if ($code -ne 0 -and -not $AllowFailure) {
        $summary = (($output -split '\r?\n' | Where-Object { $_ -match '(?i)(error|failed|could not|nicht)' } | Select-Object -Last 4) -join ' ')
        if ([string]::IsNullOrWhiteSpace($summary)) { $summary = 'No sanitized diagnostic was returned.' }
        Fail-Safe "The required operation failed: $($summary.Substring(0, [Math]::Min(400, $summary.Length)))"
    }
    [pscustomobject]@{ ExitCode = $code; Output = $output }
}
function Invoke-Loader([string]$operation, [switch]$AllowFailure) {
    Invoke-Dotnet @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.LoaderSmokeTest'), '-c', 'Release', '--no-build', '--', $operation,
        '--game-path', $script:gameRoot, '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint,
        '--repository-root', $repositoryRoot, '--bepinex-archive', $script:archivePath, '--official-digest', $expectedArchiveDigest) -AllowFailure:$AllowFailure
}
function Invoke-Plugin([string[]]$operation, [switch]$AllowFailure) {
    $arguments = @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.PluginSmokeTest'), '-c', 'Release', '--no-build', '--') + $operation
    Invoke-Dotnet $arguments -AllowFailure:$AllowFailure
}
function Value([string]$output, [string]$key) {
    $line = ($output -split '\r?\n' | Where-Object { $_.StartsWith("$key=", [StringComparison]::Ordinal) } | Select-Object -First 1)
    if ($null -eq $line) { Fail-Safe "Required evidence '$key' was not returned." }
    $line.Substring($key.Length + 1).Trim()
}
function New-Nonce {
    $bytes = New-Object byte[] 24; $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}
function Complete-ManifestIdentity([string]$root) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { Fail-Safe 'A manifest root does not exist.' }
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($directory in (Get-ChildItem -LiteralPath $root -Recurse -Directory -Force | Sort-Object FullName)) {
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Safe 'A reparse point was found in a protected manifest tree.' }
        $relative = [IO.Path]::GetRelativePath($root, $directory.FullName).Replace('\', '/')
        $lines.Add("D|$relative")
    }
    foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -File -Force | Sort-Object FullName)) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Safe 'A reparse point was found in a protected manifest tree.' }
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines.Add("F|$relative|$($file.Length)|$hash")
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($lines | Sort-Object) -join "`n")
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Build-LifecyclePackage {
    $buildRoot = Join-Path $script:experimentRoot 'lifecycle-package-build'
    $sourceRoot = Join-Path $buildRoot 'source'; $packageRoot = Join-Path $buildRoot 'package'
    New-Item -ItemType Directory -Force -Path $sourceRoot, $packageRoot | Out-Null
    $data = @(Get-ChildItem -LiteralPath $script:cleanGameRoot -Directory -Force | Where-Object { $_.Name.EndsWith('_Data', [StringComparison]::Ordinal) })
    if ($data.Count -ne 1) { Fail-Safe 'The disposable profile does not have one unambiguous Unity data directory.' }
    $managed = Join-Path $data[0].FullName 'Managed'
    $paths = @{
        BepInEx = Join-Path $script:cleanGameRoot 'BepInEx\core\BepInEx.dll'
        Unity = Join-Path $managed 'UnityEngine.dll'
        Core = Join-Path $managed 'UnityEngine.CoreModule.dll'
        Api = Join-Path $repositoryRoot 'artifacts\bin\ThroneForge.API\Release\netstandard2.1\ThroneForge.API.dll'
        Contracts = Join-Path $repositoryRoot 'artifacts\bin\ThroneForge.Contracts\Release\netstandard2.1\ThroneForge.Contracts.dll'
    }
    foreach ($path in $paths.Values) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail-Safe 'Required runtime or API evidence is missing.' } }
    $binding = Invoke-Plugin @('inspect-lifecycle-binding', '--assembly-path', $paths.Core)
    if ((Value $binding.Output 'metadata-valid') -ne 'True') { Fail-Safe 'UnityEngine.CoreModule did not prove the public quitting event.' }
    $template = Join-Path $repositoryRoot 'templates\lifecycle-plugin-smoke'
    Copy-Item -LiteralPath (Join-Path $template 'ThroneForgeLifecyclePlugin.cs') -Destination $sourceRoot
    $project = (Get-Content -LiteralPath (Join-Path $template 'PluginProject.csproj.template') -Raw).Replace('__TARGET_FRAMEWORK__', 'netstandard2.1').Replace('__BEPINEX_CORE__', $paths.BepInEx).Replace('__UNITY_ENGINE__', $paths.Unity).Replace('__UNITY_CORE_MODULE__', $paths.Core).Replace('__THRONEFORGE_API__', $paths.Api).Replace('__THRONEFORGE_CONTRACTS__', $paths.Contracts)
    [IO.File]::WriteAllText((Join-Path $sourceRoot 'ThroneForge.M1.LifecycleSmoke.csproj'), $project, [Text.UTF8Encoding]::new($false))
    Invoke-Dotnet @('restore', (Join-Path $sourceRoot 'ThroneForge.M1.LifecycleSmoke.csproj')) | Out-Null
    Invoke-Dotnet @('build', (Join-Path $sourceRoot 'ThroneForge.M1.LifecycleSmoke.csproj'), '-c', 'Release', '--no-restore') | Out-Null
    $plugin = Join-Path $sourceRoot 'bin\Release\netstandard2.1\ThroneForge.M1.LifecycleSmoke.dll'
    if (-not (Test-Path -LiteralPath $plugin -PathType Leaf)) { Fail-Safe 'The lifecycle plugin build produced no primary assembly.' }
    Copy-Item -LiteralPath $plugin -Destination (Join-Path $packageRoot 'ThroneForge.M1.LifecycleSmoke.dll')
    Copy-Item -LiteralPath $paths.Api -Destination (Join-Path $packageRoot 'ThroneForge.API.dll')
    Copy-Item -LiteralPath $paths.Contracts -Destination (Join-Path $packageRoot 'ThroneForge.Contracts.dll')
    $manifest = Join-Path $buildRoot 'package-manifest.json'
    $pack = Invoke-Plugin @('lifecycle-package', '--package-root', $packageRoot, '--manifest-path', $manifest)
    [pscustomobject]@{ PackageRoot = $packageRoot; ManifestPath = $manifest; PackageDigest = (Value $pack.Output 'package-sha256'); ApiIdentity = (Value (Invoke-Plugin @('inspect', '--assembly-path', $paths.Api, '--relative-path', 'ThroneForge.API.dll')).Output 'assembly-identity'); ContractsIdentity = (Value (Invoke-Plugin @('inspect', '--assembly-path', $paths.Contracts, '--relative-path', 'ThroneForge.Contracts.dll')).Output 'assembly-identity') }
}

Require-Absolute $GamePath 'Game path'; Require-Absolute $ExperimentRoot 'Experiment root'; Require-Absolute $BepInExArchive 'BepInEx archive'
$script:gameRoot = Normalize $GamePath; $script:experimentRoot = Normalize $ExperimentRoot; $script:archivePath = Normalize $BepInExArchive; $script:cleanGameRoot = Join-Path $script:experimentRoot 'clean-game'; $script:dotnet = Get-Dotnet
if ($expectedFingerprint -notmatch '^[0-9a-f]{64}$' -or $expectedArchiveDigest -notmatch '^[0-9a-f]{64}$') { Fail-Safe 'Fingerprint and archive digest must be SHA-256 values.' }
if ($expectedFingerprint -ne '1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d') { Fail-Safe 'The fingerprint is not the fixed Task-7 evidence fingerprint.' }
if ($expectedArchiveDigest -ne '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4') { Fail-Safe 'The archive digest is not the fixed selected BepInEx asset digest.' }
if ((Split-Path -Leaf $script:archivePath) -ne $archiveName -or -not (Test-Path -LiteralPath $script:archivePath -PathType Leaf)) { Fail-Safe 'The exact official BepInEx archive is missing.' }
if ((Get-FileHash -LiteralPath $script:archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedArchiveDigest) { Fail-Safe 'The supplied BepInEx archive digest does not match.' }
if (-not (Test-Path -LiteralPath $script:gameRoot -PathType Container) -or (Under $repositoryRoot $script:experimentRoot) -or (Under $script:gameRoot $script:experimentRoot)) { Fail-Safe 'The experiment root is not an external explicit root.' }

if ($Mode -eq 'Cleanup') {
    Invoke-Plugin @('cleanup-owned', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--repository-root', $repositoryRoot, '--original-game', $script:gameRoot) | Out-Null
    'Owned cleanup completed after ownership validation.'; exit 0
}
if ($Mode -eq 'Full' -and (Test-Path -LiteralPath $script:experimentRoot) -and @(Get-ChildItem -LiteralPath $script:experimentRoot -Force).Count -ne 0) { Fail-Safe 'Full mode requires a nonexistent or empty experiment root.' }
if ($Mode -eq 'Full') {
    $commit = ((& git -C $repositoryRoot rev-parse HEAD 2>$null) | Out-String).Trim()
    Invoke-Plugin @('ownership', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--status', 'Prepared', '--repository-commit', $commit) | Out-Null
}
if ($Mode -eq 'Plan' -or $WhatIf) { Invoke-Loader Plan | Out-Null; 'Plan succeeded; no lifecycle package was built or deployed.'; exit 0 }
if ($Mode -eq 'Rollback') {
    Invoke-Plugin @('remove', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--repository-root', $repositoryRoot, '--original-game', $script:gameRoot, '--plugin-guid', $pluginGuid) -AllowFailure | Out-Null
    Invoke-Loader Rollback | Out-Null; 'Explicit lifecycle rollback completed.'; exit 0
}
if (Test-Path -LiteralPath $script:cleanGameRoot) { Fail-Safe 'Full mode requires a fresh disposable profile.' }

$loaderApplied = $false; $pluginDeployed = $false; $manualClosure = $false; $result = 'Failed'; $failure = 'The lifecycle smoke test did not complete.'; $package = $null; $rollbackVerified = $false
try {
    $preRuntime = Invoke-Dotnet @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.Discovery'), '-c', 'Release', '--no-build', '--', 'runtime-compatibility', '--game-path', $script:gameRoot, '--fingerprint', $expectedFingerprint, '--output-root', (Join-Path $script:experimentRoot 'evidence\original-pre-runtime'), '--overwrite')
    $preOriginalManifest = Complete-ManifestIdentity $script:gameRoot
    Invoke-Loader Prepare | Out-Null
    $selectedExecutableRelative = Value $preRuntime.Output 'Selected executable'
    if ($selectedExecutableRelative -eq 'unknown') { Fail-Safe 'The original installation did not expose an unambiguous executable.' }
    $nonce = New-Nonce
    $baseline = Invoke-Plugin @('launch', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--executable', (Join-Path $script:cleanGameRoot ($selectedExecutableRelative -replace '/', '\')), '--nonce', $nonce) -AllowFailure
    if ($baseline.ExitCode -ne 0) { Fail-Safe 'The copied baseline launch did not complete.' }
    Invoke-Loader Install | Out-Null; $loaderApplied = $true
    if ((Invoke-Loader Launch -AllowFailure).ExitCode -ne 0 -or (Invoke-Loader Verify -AllowFailure).ExitCode -ne 0) { Fail-Safe 'Loader-only bootstrap did not pass.' }
    Invoke-Plugin @('ownership', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--status', 'LaunchObserved') | Out-Null
    $package = Build-LifecyclePackage
    $admit = Invoke-Plugin @('admit-and-deploy', '--package-kind', 'lifecycle', '--package-root', $package.PackageRoot, '--manifest-path', $package.ManifestPath, '--target-framework', 'netstandard2.1', '--expected-fingerprint', $expectedFingerprint, '--adapter-id', 'throneforge.adapter', '--adapter-version', '1.0.0', '--original-game', $script:gameRoot, '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--repository-root', $repositoryRoot)
    if ((Value $admit.Output 'admission') -ne 'Approved') { Fail-Safe 'Lifecycle package admission was not approved.' }
    $package | Add-Member PackageDigest (Value $admit.Output 'package-sha256') -Force; $package | Add-Member BindingDigest (Value $admit.Output 'binding-digest') -Force; $pluginDeployed = $true
    $launch = Invoke-Plugin @('launch', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--executable', (Join-Path $script:cleanGameRoot ($selectedExecutableRelative -replace '/', '\')), '--nonce', $nonce) -AllowFailure
    $manualClosure = $launch.Output -match 'manual-closure-required=True'
    if ($manualClosure) { Fail-Safe 'Manual closure is required before files can be changed.' }
    if ($launch.ExitCode -ne 0) { Fail-Safe 'The lifecycle-enabled launch did not complete.' }
    $logs = @((Join-Path $script:cleanGameRoot 'BepInEx\LogOutput.log'), (Join-Path $script:cleanGameRoot 'BepInEx\LogOutput.txt') | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($logs.Count -ne 1) { Fail-Safe 'No single stable BepInEx log was identified.' }
    $verified = Invoke-Plugin @('verify-lifecycle-log', '--log-path', $logs[0], '--nonce', $nonce, '--api-identity', $package.ApiIdentity, '--contracts-identity', $package.ContractsIdentity)
    if ((Value $verified.Output 'lifecycle-criteria') -ne 'True') { Fail-Safe 'The lifecycle marker sequence did not pass verification.' }
    Invoke-Plugin @('ownership', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--status', 'LaunchObserved', '--package-sha256', $package.PackageDigest, '--binding-digest', $package.BindingDigest, '--plugin-root', $pluginRoot) | Out-Null
    $result = 'Passed'; $failure = 'Public Unity Application.quitting binding and synthetic lifecycle sequence passed.'
}
catch { $failure = 'Sanitized operation failure; see stable lifecycle failure category in local output.'; if ($manualClosure) { $result = 'Inconclusive' } }
finally {
    if (-not $manualClosure) {
        if ($pluginDeployed) { Invoke-Plugin @('remove', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--repository-root', $repositoryRoot, '--original-game', $script:gameRoot, '--plugin-guid', $pluginGuid) -AllowFailure | Out-Null }
        if ($loaderApplied) { $rollback = Invoke-Loader Rollback -AllowFailure; $rollbackVerified = $rollback.ExitCode -eq 0 }
    } else {
        Invoke-Plugin @('recovery', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--plugin-root', $pluginRoot, '--loader-status', 'RollbackRequired') -AllowFailure | Out-Null
    }
}
$postOriginalManifest = Complete-ManifestIdentity $script:gameRoot
$postRuntime = Invoke-Dotnet @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.Discovery'), '-c', 'Release', '--no-build', '--', 'runtime-compatibility', '--game-path', $script:gameRoot, '--fingerprint', $expectedFingerprint, '--output-root', (Join-Path $script:experimentRoot 'evidence\original-post-runtime'), '--overwrite') -AllowFailure
$originalUnchanged = $preOriginalManifest -eq $postOriginalManifest
if ($result -eq 'Passed' -and (-not $originalUnchanged -or $postRuntime.ExitCode -ne 0 -or -not $rollbackVerified)) { $result = 'Failed'; $failure = 'Original post-verification or rollback verification failed.' }
$report = Join-Path $repositoryRoot "docs\discovery\$expectedFingerprint-lifecycle-binding.md"
$lines = @('# Thronefall Lifecycle Binding Report', '', '- Report version: throneforge-lifecycle-binding-v1', "- Game fingerprint: $expectedFingerprint", '- Unity version: 2022.3.62f2', '- Backend: Mono', '- Architecture: X64', "- BepInEx: $loaderVersion", "- Binding ID: $bindingId", '- Source: public UnityEngine.Application.quitting event', '- Metadata preflight: public static System.Action event required before the private run', "- Package digest: $($package.PackageDigest)", "- Admission binding digest: $($package.BindingDigest)", '- Initialization count: exact marker sequence required', '- Unity-quitting count: exact marker sequence required', '- Shutdown count: exact marker sequence required', '- Runtime API/Contracts identities: measured from loaded types and compared with admitted package', "- Plugin removal: $rollbackVerified", "- Loader rollback: $rollbackVerified", "- Disposable restoration: $rollbackVerified", "- Original pre/post manifest: $originalUnchanged", "- Original runtime/readiness postcheck: $($postRuntime.ExitCode -eq 0)", "- Result: $result", "- Notes: $failure", '- This is a public Unity Application.quitting binding observed while Thronefall was running, not a verified Thronefall-internal lifecycle method.', '- Privacy: nonce, paths, logs, binaries, manifests, usernames and machine data are omitted.', '- Remaining uncertainty: no Harmony, game API, gameplay state, catalog, save, wave, async lifecycle or cross-version compatibility is claimed.')
$reportDirectory = Get-Item -LiteralPath (Split-Path -Parent $report) -Force
if (($reportDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Safe 'The committed report directory is a reparse point.' }
if (-not (Under $repositoryRoot $report) -or (Under $script:gameRoot $report) -or (Under $script:cleanGameRoot $report)) { Fail-Safe 'The committed report path is outside the repository discovery boundary.' }
if (Test-Path -LiteralPath $report -PathType Leaf) {
    $existingReport = Get-Item -LiteralPath $report -Force
    if (($existingReport.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Safe 'The committed report path is a reparse point.' }
}
[IO.File]::WriteAllText($report, ($lines -join [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
Write-Output "Lifecycle smoke-test result: $result"
Write-Output "Sanitized report: $([IO.Path]::GetFileName($report))"
if ($result -eq 'Passed') { exit 0 }; exit 1
