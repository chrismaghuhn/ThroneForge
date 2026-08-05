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
$script:isWindows = [string]::Equals($env:OS, 'Windows_NT', [StringComparison]::OrdinalIgnoreCase)
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
function Invoke-RuntimeEvidence([string]$root, [switch]$AllowFailure) {
    $outputRoot = Join-Path $script:experimentRoot ("evidence\" + $root)
    $result = Invoke-Dotnet @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.Discovery'), '-c', 'Release', '--no-build', '--', 'runtime-compatibility-evidence', '--game-path', $script:gameRoot, '--fingerprint', $expectedFingerprint, '--output-root', $outputRoot, '--overwrite') -AllowFailure:$AllowFailure
    if ($result.ExitCode -ne 0) { if ($AllowFailure) { return $null } ; Fail-Safe 'The machine-readable runtime evidence operation failed.' }
    try { $evidence = $result.Output | ConvertFrom-Json } catch { if ($AllowFailure) { return $null }; Fail-Safe 'The machine-readable runtime evidence was not valid JSON.' }
    if ($null -eq $evidence -or $evidence.'schema-version' -ne 'throneforge-runtime-compatibility-evidence-v1') { if ($AllowFailure) { return $null }; Fail-Safe 'The machine-readable runtime evidence schema is unsupported.' }
    foreach ($field in @('game-fingerprint', 'selected-executable-relative-path', 'managed-runtime-profile', 'executable-architecture', 'smoke-test-readiness', 'loader-indicators-absent')) {
        $property = $evidence.PSObject.Properties[$field]
        if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) { if ($AllowFailure) { return $null }; Fail-Safe "The machine-readable runtime evidence field '$field' is missing." }
    }
    if ($evidence.'game-fingerprint' -ne $expectedFingerprint -or $evidence.'smoke-test-readiness' -ne 'ReadyForReversibleTest' -or -not [bool]$evidence.'loader-indicators-absent') { if ($AllowFailure) { return $null }; Fail-Safe 'The original installation is not ready for the clean-profile experiment.' }
    return $evidence
}
function Stage-FailureCategory([string]$stage) {
    switch ($stage) {
        'OriginalPreflight' { 'original-preflight-failed'; break }
        'DisposablePrepare' { 'disposable-prepare-failed'; break }
        'BaselineLaunch' { 'baseline-launch-failed'; break }
        'LoaderInstall' { 'loader-install-failed'; break }
        'LoaderLaunch' { 'loader-launch-failed'; break }
        'LoaderVerify' { 'loader-verify-failed'; break }
        'UnityMetadataPreflight' { 'unity-metadata-preflight-failed'; break }
        'PackageBuild' { 'package-build-failed'; break }
        'PackageCapture' { 'package-capture-failed'; break }
        'AdmitAndDeploy' { 'deployment-failed'; break }
        'LifecycleLaunch' { 'lifecycle-launch-failed'; break }
        'LogStability' { 'log-not-stable'; break }
        'LifecycleVerification' { 'lifecycle-marker-invalid'; break }
        'PluginRemoval' { 'plugin-removal-failed'; break }
        'LoaderRollback' { 'loader-rollback-failed'; break }
        'DisposablePostcheck' { 'disposable-restoration-failed'; break }
        'OriginalPostcheck' { 'original-postcheck-failed'; break }
        default { 'lifecycle-stage-failed' }
    }
}
function Write-StageState([string]$stage, [string]$lastCompleted, [string]$category, [switch]$AllowFailure) {
    if ([string]::IsNullOrWhiteSpace($script:experimentId)) { return $false }
    $arguments = @(
        'lifecycle-stage',
        '--experiment-root', $script:experimentRoot,
        '--experiment-id', $script:experimentId,
        '--expected-fingerprint', $expectedFingerprint,
        '--current-stage', $stage,
        '--result-category', $category)
    if (-not [string]::IsNullOrWhiteSpace($lastCompleted)) { $arguments += @('--last-completed-stage', $lastCompleted) }
    if (-not [string]::IsNullOrWhiteSpace($script:stageLoaderStatus)) { $arguments += @('--loader-status', $script:stageLoaderStatus) }
    if (-not [string]::IsNullOrWhiteSpace($script:stagePackageDigest)) { $arguments += @('--package-sha256', $script:stagePackageDigest) }
    if (-not [string]::IsNullOrWhiteSpace($script:stageBindingDigest)) { $arguments += @('--binding-digest', $script:stageBindingDigest) }
    $stateResult = Invoke-Plugin $arguments -AllowFailure:$AllowFailure
    if ($stateResult.ExitCode -ne 0) { return $false }
    $script:stageStatePersisted = $true
    return $true
}
function Start-Stage([string]$stage) {
    $script:currentStage = $stage
    if (-not (Write-StageState $stage $script:lastCompletedStage 'in-progress')) { Fail-Safe 'The lifecycle stage state could not be persisted.' }
}
function Complete-Stage([string]$nextStage) {
    $script:lastCompletedStage = $script:currentStage
    $script:currentStage = $nextStage
    if (-not (Write-StageState $nextStage $script:lastCompletedStage 'stage-completed')) { Fail-Safe 'The lifecycle stage state could not be persisted.' }
}
function Fail-CurrentStage([string]$category) {
    if (-not (Write-StageState $script:currentStage $script:lastCompletedStage $category -AllowFailure)) { $script:stageStatePersisted = $false }
}
function Verify-LoaderStage([string]$expectedStatus) {
    $result = Invoke-Plugin @('verify-loader-stage', '--repository-root', $repositoryRoot, '--original-game', $script:gameRoot, '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--expected-status', $expectedStatus)
    $script:stageLoaderStatus = Value $result.Output 'loader-status'
    return $result
}
function Get-ManifestIdentity([string]$root) {
    Value (Invoke-Plugin @('manifest', '--root', $root)).Output 'manifest-identity'
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
function Relative([string]$root, [string]$path) {
    $normalizedRoot = Normalize $root
    $normalizedPath = Normalize $path
    if (-not (Under $normalizedRoot $normalizedPath)) { Fail-Safe 'Manifest path escaped its validated root.' }
    if ($normalizedPath.Equals($normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) { return '' }
    $normalizedPath.Substring($normalizedRoot.Length + 1).Replace('\', '/')
}
function Complete-ManifestIdentity([string]$root) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { Fail-Safe 'A manifest root does not exist.' }
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($directory in (Get-ChildItem -LiteralPath $root -Recurse -Directory -Force | Sort-Object FullName)) {
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Safe 'A reparse point was found in a protected manifest tree.' }
        $relative = Relative $root $directory.FullName
        $lines.Add("D|$relative")
    }
    foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -File -Force | Sort-Object FullName)) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Safe 'A reparse point was found in a protected manifest tree.' }
        $relative = Relative $root $file.FullName
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines.Add("F|$relative|$($file.Length)|$hash")
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($lines | Sort-Object) -join "`n")
    $sha = New-Object Security.Cryptography.SHA256Managed
    try { $digest = $sha.ComputeHash($bytes) } finally { $sha.Dispose() }
    [BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant()
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
    Copy-Item -LiteralPath (Join-Path $template 'LifecycleHost.cs') -Destination $sourceRoot
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
function Invoke-LifecycleMetadataPreflight {
    $data = @(Get-ChildItem -LiteralPath $script:cleanGameRoot -Directory -Force | Where-Object { $_.Name.EndsWith('_Data', [StringComparison]::Ordinal) })
    if ($data.Count -ne 1) { Fail-Safe 'The disposable profile does not have one unambiguous Unity data directory.' }
    $core = Join-Path (Join-Path $data[0].FullName 'Managed') 'UnityEngine.CoreModule.dll'
    if (-not (Test-Path -LiteralPath $core -PathType Leaf)) { Fail-Safe 'UnityEngine.CoreModule metadata is missing from the disposable profile.' }
    $binding = Invoke-Plugin @('inspect-lifecycle-binding', '--assembly-path', $core)
    if ((Value $binding.Output 'metadata-valid') -ne 'True' -or (Value $binding.Output 'assembly-identity') -notlike 'UnityEngine.CoreModule,*') { Fail-Safe 'UnityEngine.CoreModule did not prove the exact public quitting event.' }
    return $core
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
$script:experimentId = $null
if ($Mode -eq 'Full') {
    $commit = ((& git -C $repositoryRoot rev-parse HEAD 2>$null) | Out-String).Trim()
    $ownership = Invoke-Plugin @('ownership', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--status', 'Prepared', '--repository-commit', $commit)
    $script:experimentId = Value $ownership.Output 'experiment-id'
}
if ($Mode -eq 'Plan' -or $WhatIf) { Invoke-Loader Plan | Out-Null; 'Plan succeeded; no lifecycle package was built or deployed.'; exit 0 }
if ($Mode -eq 'Rollback') {
    Invoke-Plugin @('remove', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--repository-root', $repositoryRoot, '--original-game', $script:gameRoot, '--plugin-guid', $pluginGuid) -AllowFailure | Out-Null
    Invoke-Loader Rollback | Out-Null; 'Explicit lifecycle rollback completed.'; exit 0
}
if (Test-Path -LiteralPath $script:cleanGameRoot) { Fail-Safe 'Full mode requires a fresh disposable profile.' }

$loaderApplied = $false
$pluginDeployed = $false
$manualClosure = $false
$result = 'Failed'
$failure = 'The lifecycle smoke test did not complete.'
$failureCategory = 'lifecycle-stage-failed'
$failedStage = ''
$package = $null
$currentStage = 'OriginalPreflight'
$lastCompletedStage = ''
$stageStatePersisted = $false
$stageLoaderStatus = ''
$stagePackageDigest = ''
$stageBindingDigest = ''
$pluginRemovalVerified = 'not-required'
$preLoaderRollbackManifestVerified = 'not-required'
$loaderRollbackVerified = $false
$disposableBaselineRestored = $false
$originalManifestUnchanged = $false
$originalRuntimePostcheckPassed = $false
$originalLoaderIndicatorsAbsent = $false
$preOriginalManifest = $null
$disposableBaselineIdentity = $null
$loaderOnlyManifestIdentity = $null
$nonce = $null
$initializationCount = 'not-observed'
$quittingCount = 'not-observed'
$shutdownCount = 'not-observed'
$markerSequence = 'not-observed'
$runtimeApiIdentity = 'not-observed'
$runtimeContractsIdentity = 'not-observed'
$pluginCount = 'not-observed'
$loaderWarnings = 'not-observed'
$loaderErrors = 'not-observed'
$loaderFatalErrors = 'not-observed'
$preRuntimeEvidence = $null
$postRuntimeEvidence = $null
$operationFailureCategory = $null
try {
    Start-Stage 'OriginalPreflight'
    $preRuntimeEvidence = Invoke-RuntimeEvidence 'original-pre-runtime'
    $preOriginalManifest = Complete-ManifestIdentity $script:gameRoot
    $selectedExecutableRelative = [string]$preRuntimeEvidence.'selected-executable-relative-path'
    if ([string]::IsNullOrWhiteSpace($selectedExecutableRelative)) { Fail-Safe 'The original installation did not expose an unambiguous executable.' }
    Complete-Stage 'DisposablePrepare'
    Start-Stage 'DisposablePrepare'
    Invoke-Loader Prepare | Out-Null
    Complete-Stage 'BaselineLaunch'
    Start-Stage 'BaselineLaunch'
    $nonce = New-Nonce
    $baseline = Invoke-Plugin @('launch', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--executable', (Join-Path $script:cleanGameRoot ($selectedExecutableRelative -replace '/', '\')), '--nonce', $nonce) -AllowFailure
    if ($baseline.ExitCode -ne 0) { Fail-Safe 'The copied baseline launch did not complete.' }
    Complete-Stage 'LoaderInstall'
    Start-Stage 'LoaderInstall'
    $install = Invoke-Loader Install -AllowFailure
    if ($install.ExitCode -ne 0) { Fail-Safe 'The loader installation did not complete.' }
    $loaderApplied = $true
    Invoke-Plugin @('ownership', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--status', 'LoaderApplied', '--loader-status', 'Applied') | Out-Null
    $loaderInstallEvidence = Verify-LoaderStage 'Applied'
    $disposableBaselineIdentity = Value $loaderInstallEvidence.Output 'baseline-manifest-identity'
    Complete-Stage 'LoaderLaunch'
    Start-Stage 'LoaderLaunch'
    $loaderLaunch = Invoke-Loader Launch -AllowFailure
    if ($loaderLaunch.ExitCode -ne 0) { Fail-Safe 'The loader-only launch did not complete.' }
    $loaderLaunchEvidence = Verify-LoaderStage 'LaunchObserved'
    Complete-Stage 'LoaderVerify'
    Start-Stage 'LoaderVerify'
    $loaderVerify = Invoke-Loader Verify -AllowFailure
    if ($loaderVerify.ExitCode -ne 0) { Fail-Safe 'The loader-only bootstrap verification did not pass.' }
    $loaderOnlyManifestIdentity = Get-ManifestIdentity $script:cleanGameRoot
    Invoke-Plugin @('ownership', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--status', 'LaunchObserved') | Out-Null
    Complete-Stage 'UnityMetadataPreflight'
    Start-Stage 'UnityMetadataPreflight'
    $unitySource = Invoke-LifecycleMetadataPreflight
    Complete-Stage 'PackageBuild'
    Start-Stage 'PackageBuild'
    $package = Build-LifecyclePackage
    Complete-Stage 'PackageCapture'
    Start-Stage 'PackageCapture'
    if ($null -eq $package -or [string]::IsNullOrWhiteSpace($package.PackageDigest)) { Fail-Safe 'The lifecycle package capture did not produce a digest.' }
    Complete-Stage 'AdmitAndDeploy'
    Start-Stage 'AdmitAndDeploy'
    $admit = Invoke-Plugin @('admit-and-deploy', '--package-kind', 'lifecycle', '--package-root', $package.PackageRoot, '--manifest-path', $package.ManifestPath, '--target-framework', 'netstandard2.1', '--expected-fingerprint', $expectedFingerprint, '--adapter-id', 'throneforge.adapter', '--adapter-version', '1.0.0', '--original-game', $script:gameRoot, '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--repository-root', $repositoryRoot) -AllowFailure
    if ($admit.ExitCode -ne 0) {
        $operationFailureCategory = ($admit.Output -split '\r?\n' | Where-Object { $_ -match '^.*phase-failure-category=' } | Select-Object -Last 1)
        if ($operationFailureCategory) { $operationFailureCategory = ($operationFailureCategory -split '=', 2)[1].TrimEnd('.') }
        Fail-Safe $(if ([string]::IsNullOrWhiteSpace($operationFailureCategory)) { 'The lifecycle package admit-and-deploy operation failed.' } else { "The lifecycle package admit-and-deploy operation failed with category $operationFailureCategory." })
    }
    if ((Value $admit.Output 'admission') -ne 'Approved') { Fail-Safe 'Lifecycle package admission was not approved.' }
    $package | Add-Member PackageDigest (Value $admit.Output 'package-sha256') -Force
    $package | Add-Member BindingDigest (Value $admit.Output 'binding-digest') -Force
    $stagePackageDigest = $package.PackageDigest
    $stageBindingDigest = $package.BindingDigest
    $pluginDeployed = $true
    Complete-Stage 'LifecycleLaunch'
    Start-Stage 'LifecycleLaunch'
    $launch = Invoke-Plugin @('launch', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--executable', (Join-Path $script:cleanGameRoot ($selectedExecutableRelative -replace '/', '\')), '--nonce', $nonce) -AllowFailure
    $manualClosure = $launch.Output -match 'manual-closure-required=True'
    if ($manualClosure) { Fail-Safe 'Manual closure is required before files can be changed.' }
    if ($launch.ExitCode -ne 0) { Fail-Safe 'The lifecycle-enabled launch did not complete.' }
    Start-Stage 'LogStability'
    $logs = @(
        @(
            (Join-Path $script:cleanGameRoot 'BepInEx\LogOutput.log')
            (Join-Path $script:cleanGameRoot 'BepInEx\LogOutput.txt')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )
    if ($logs.Count -ne 1) { $operationFailureCategory = if ($logs.Count -eq 0) { 'log-missing' } else { 'log-not-readable' }; Fail-Safe 'The lifecycle log candidate set was not exactly one file.' }
    $verified = Invoke-Plugin @('verify-lifecycle-log', '--log-path', $logs[0], '--nonce', $nonce, '--api-identity', $package.ApiIdentity, '--contracts-identity', $package.ContractsIdentity) -AllowFailure
    if ((Value $verified.Output 'log-stable') -ne 'true') { $operationFailureCategory = Value $verified.Output 'failure-category'; Fail-Safe 'The lifecycle log did not become stably readable.' }
    Complete-Stage 'LifecycleVerification'
    Start-Stage 'LifecycleVerification'
    if ($verified.ExitCode -ne 0 -or (Value $verified.Output 'lifecycle-criteria') -ne 'True') { Fail-Safe 'The lifecycle marker sequence did not pass verification.' }
    $initializationCount = Value $verified.Output 'initialization-count'
    $quittingCount = Value $verified.Output 'quitting-count'
    $shutdownCount = Value $verified.Output 'shutdown-count'
    $markerSequence = Value $verified.Output 'marker-sequence'
    $runtimeApiIdentity = Value $verified.Output 'runtime-api-identity'
    $runtimeContractsIdentity = Value $verified.Output 'runtime-contracts-identity'
    $pluginCount = Value $verified.Output 'plugins'
    $loaderWarnings = Value $verified.Output 'warnings'
    $loaderErrors = Value $verified.Output 'errors'
    $loaderFatalErrors = Value $verified.Output 'fatal-errors'
    $result = 'Passed'; $failure = 'Public Unity Application.quitting binding and synthetic lifecycle sequence passed.'; $failureCategory = 'stage-completed'
}
catch {
    $failedStage = $currentStage
    $failureCategory = if ($manualClosure) { 'manual-closure-required' } elseif (-not [string]::IsNullOrWhiteSpace($operationFailureCategory)) { $operationFailureCategory } else { Stage-FailureCategory $currentStage }
    $failure = "The lifecycle experiment stopped at stage $currentStage with stable category $failureCategory."
    if ($manualClosure) { $result = 'Inconclusive' }
    Fail-CurrentStage $failureCategory
}
finally {
    if (-not $manualClosure) {
        if ($pluginDeployed) {
            Start-Stage 'PluginRemoval'
            $remove = Invoke-Plugin @('remove', '--clean-game', $script:cleanGameRoot, '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--repository-root', $repositoryRoot, '--original-game', $script:gameRoot, '--plugin-guid', $pluginGuid) -AllowFailure
            $pluginRemovalVerified = $remove.ExitCode -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $script:cleanGameRoot $pluginRoot))
            if (-not $pluginRemovalVerified) { $result = 'Failed'; $failureCategory = 'plugin-removal-failed'; $failure = 'The lifecycle plugin removal could not be independently verified.' }
            if ($pluginRemovalVerified) {
                $afterRemovalIdentity = Get-ManifestIdentity $script:cleanGameRoot
                $preLoaderRollbackManifestVerified = $afterRemovalIdentity -eq $loaderOnlyManifestIdentity
                if (-not $preLoaderRollbackManifestVerified) { $result = 'Failed'; $failureCategory = 'disposable-restoration-failed'; $failure = 'The loader-only profile was not restored before loader rollback.' }
            }
        }
        if ($loaderApplied -and ($pluginRemovalVerified -eq 'not-required' -or $pluginRemovalVerified)) {
            Start-Stage 'LoaderRollback'
            $rollback = Invoke-Loader Rollback -AllowFailure
            $loaderRollbackVerified = $rollback.ExitCode -eq 0
            if (-not $loaderRollbackVerified) { $result = 'Failed'; $failureCategory = 'loader-rollback-failed'; $failure = 'The loader rollback could not be independently verified.' }
            if ($loaderRollbackVerified) {
                $afterRollbackIdentity = Get-ManifestIdentity $script:cleanGameRoot
                $disposableBaselineRestored = $afterRollbackIdentity -eq $disposableBaselineIdentity
                if (-not $disposableBaselineRestored) { $result = 'Failed'; $failureCategory = 'disposable-restoration-failed'; $failure = 'The disposable baseline was not restored after loader rollback.' }
            }
        }
    } else {
        Invoke-Plugin @('recovery', '--experiment-root', $script:experimentRoot, '--expected-fingerprint', $expectedFingerprint, '--plugin-root', $pluginRoot, '--loader-status', 'RollbackRequired') -AllowFailure | Out-Null
    }
}
Start-Stage 'DisposablePostcheck'
if ($loaderApplied -and -not $manualClosure) {
    $postDisposableLoaderIdentity = Get-ManifestIdentity $script:cleanGameRoot
    if ($null -ne $disposableBaselineIdentity -and $postDisposableLoaderIdentity -eq $disposableBaselineIdentity) { $disposableBaselineRestored = $true }
}
Complete-Stage 'OriginalPostcheck'
Start-Stage 'OriginalPostcheck'
$postOriginalManifest = Complete-ManifestIdentity $script:gameRoot
$postRuntimeEvidence = Invoke-RuntimeEvidence 'original-post-runtime' -AllowFailure
$originalUnchanged = $preOriginalManifest -eq $postOriginalManifest
$originalManifestUnchanged = $originalUnchanged
$originalRuntimePostcheckPassed = $null -ne $postRuntimeEvidence
$originalLoaderIndicatorsAbsent = $originalRuntimePostcheckPassed -and [bool]$postRuntimeEvidence.'loader-indicators-absent'
if (-not $originalManifestUnchanged -or -not $originalRuntimePostcheckPassed) { $result = 'Failed'; $failureCategory = 'original-postcheck-failed'; $failure = 'The original installation post-verification did not pass.' }
if ($result -eq 'Passed' -and (-not $pluginRemovalVerified -or -not $preLoaderRollbackManifestVerified -or -not $loaderRollbackVerified -or -not $disposableBaselineRestored)) { $result = 'Failed'; $failureCategory = 'disposable-restoration-failed'; $failure = 'A distinct plugin, loader, or disposable restoration check did not pass.' }
if ($result -eq 'Passed') { Complete-Stage 'Completed' }
$finalResult = [pscustomobject]@{
    OverallResult = $result
    CurrentStage = $currentStage
    FailedStage = if ([string]::IsNullOrWhiteSpace($failedStage)) { 'none' } else { $failedStage }
    LastCompletedStage = $lastCompletedStage
    StableCategory = $failureCategory
    StageStatePersisted = $stageStatePersisted
    SelectedExecutableRelativePath = if ($null -eq $preRuntimeEvidence) { 'not-observed' } else { $preRuntimeEvidence.'selected-executable-relative-path' }
    LoaderTransactionStatus = if ([string]::IsNullOrWhiteSpace($stageLoaderStatus)) { 'not-observed' } else { $stageLoaderStatus }
    UnitySourceAssemblyIdentity = if ($null -eq $unitySource) { 'not-observed' } else { 'UnityEngine.CoreModule' }
    PackageSha256 = if ($null -eq $package) { 'not-produced' } else { $package.PackageDigest }
    AdmissionBindingDigest = if ($null -eq $package) { 'not-produced' } else { $package.BindingDigest }
    InitializationCount = $initializationCount
    QuittingCount = $quittingCount
    ShutdownCount = $shutdownCount
    MarkerEncounterOrder = $markerSequence
    RuntimeApiIdentity = $runtimeApiIdentity
    RuntimeContractsIdentity = $runtimeContractsIdentity
    PluginCount = $pluginCount
    WarningCount = $loaderWarnings
    ErrorCount = $loaderErrors
    FatalErrorCount = $loaderFatalErrors
    PluginRemovalVerified = $pluginRemovalVerified
    LoaderRollbackVerified = $loaderRollbackVerified
    DisposableRestorationVerified = $disposableBaselineRestored
    OriginalManifestVerified = $originalManifestUnchanged
    OriginalRuntimeVerified = $originalRuntimePostcheckPassed
    OriginalLoaderIndicatorsAbsent = $originalLoaderIndicatorsAbsent
}
$report = Join-Path $repositoryRoot "docs\discovery\$expectedFingerprint-lifecycle-binding.md"
$lines = @(
    '# Thronefall Lifecycle Binding Report', '',
    '- Report version: throneforge-lifecycle-binding-v1', "- Game fingerprint: $expectedFingerprint", '- Unity version: 2022.3.62f2', '- Backend: Mono', '- Architecture: X64',
    "- BepInEx: $loaderVersion", "- Binding ID: $bindingId", '- Source: public UnityEngine.Application.quitting event',
    '- Historical private attempt: Failed; stage before LoaderInstall completion; transaction persisted: false; package admitted: false; plugin deployed: false; lifecycle evidence: none.',
    "- Current stage: $($finalResult.CurrentStage)", "- Failed stage: $($finalResult.FailedStage)", "- Last completed stage: $($finalResult.LastCompletedStage)", "- Stable result category: $($finalResult.StableCategory)", "- Stage state persisted: $($finalResult.StageStatePersisted)",
    '- Unity metadata source assembly: UnityEngine.CoreModule', '- Metadata preflight: public static System.Action event required',
    "- Package digest: $($finalResult.PackageSha256)",
    "- Admission binding digest: $($finalResult.AdmissionBindingDigest)",
    "- Initialization count: $($finalResult.InitializationCount)", "- Unity-quitting count: $($finalResult.QuittingCount)", "- Shutdown count: $($finalResult.ShutdownCount)", "- Marker encounter order: $($finalResult.MarkerEncounterOrder)",
    "- Runtime API identity: $($finalResult.RuntimeApiIdentity)", "- Runtime Contracts identity: $($finalResult.RuntimeContractsIdentity)", "- Plugin count: $($finalResult.PluginCount)", "- Loader warnings/errors/fatal: $($finalResult.WarningCount)/$($finalResult.ErrorCount)/$($finalResult.FatalErrorCount)",
    "- Plugin removal verified: $($finalResult.PluginRemovalVerified)", "- Loader-only manifest restored before rollback: $preLoaderRollbackManifestVerified", "- Loader rollback verified: $($finalResult.LoaderRollbackVerified)", "- Disposable baseline restored: $($finalResult.DisposableRestorationVerified)",
    "- Original complete manifest unchanged: $($finalResult.OriginalManifestVerified)", "- Original runtime/readiness postcheck passed: $($finalResult.OriginalRuntimeVerified)", "- Original loader indicators absent: $($finalResult.OriginalLoaderIndicatorsAbsent)",
    "- Result: $($finalResult.OverallResult)", "- Notes: $failure", '- This is a public Unity Application.quitting binding observed while Thronefall was running, not a verified Thronefall-internal lifecycle method.', '- Privacy: nonce, paths, logs, binaries, manifests, usernames and machine data are omitted.', '- Remaining uncertainty: no Harmony, game API, gameplay state, catalog, save, wave, async lifecycle or cross-version compatibility is claimed.')
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
