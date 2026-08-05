[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Full', 'Rollback', 'Cleanup')]
    [string]$Mode = 'Full',
    [Parameter(Mandatory = $true)]
    [string]$GamePath,
    [Parameter(Mandatory = $true)]
    [string]$ExperimentRoot,
    [Parameter(Mandatory = $true)]
    [string]$BepInExArchive,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedFingerprint,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedBepInExDigest,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$pluginGuid = 'dev.throneforge.m1.synthetic-smoke'
$pluginVersion = '0.0.1'
$expectedArchiveName = 'BepInEx_win_x64_5.4.23.5.zip'
$expectedLoaderVersion = '5.4.23.5'

function Throw-Sanitized([string]$message) {
    throw "Synthetic plugin smoke test failed: $message"
}

function Assert-AbsolutePath([string]$path, [string]$description) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not [IO.Path]::IsPathRooted($path)) {
        Throw-Sanitized "$description must be an explicit absolute path."
    }
}

function Get-NormalizedPath([string]$path) {
    return [IO.Path]::GetFullPath($path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

function Test-RunningOnWindows {
    return [string]::Equals($env:OS, 'Windows_NT', [StringComparison]::OrdinalIgnoreCase)
}

function Test-SameOrDescendant([string]$root, [string]$candidate) {
    $normalizedRoot = Get-NormalizedPath $root
    $normalizedCandidate = Get-NormalizedPath $candidate
    $comparison = if (Test-RunningOnWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    return $normalizedCandidate.Equals($normalizedRoot, $comparison) -or $normalizedCandidate.StartsWith($normalizedRoot + [IO.Path]::DirectorySeparatorChar, $comparison)
}

function Get-DotnetPath {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    foreach ($candidate in @('C:\Program Files\dotnet\dotnet.exe', 'C:\Program Files (x86)\dotnet\dotnet.exe')) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    Throw-Sanitized 'The .NET SDK executable could not be located.'
}

$dotnet = Get-DotnetPath
$script:dotnet = $dotnet
$script:repositoryRoot = $repositoryRoot

function Invoke-DotnetOperation([string[]]$arguments, [switch]$AllowFailure) {
    $output = (& $script:dotnet @arguments 2>&1 | Out-String).Trim()
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        $diagnosticLines = @($output -split '\r?\n' | Where-Object { $_ -match '(?i)(error|failed|could not|nicht)' } | Select-Object -Last 5)
        if ($diagnosticLines.Count -eq 0) {
            $diagnosticLines = @($output -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 3)
        }
        $diagnostic = ($diagnosticLines -join ' ') -replace '(?i)([A-Z]:\\|/)[^\s"'']+', '<redacted-path>'
        if ($diagnostic.Length -gt 400) { $diagnostic = $diagnostic.Substring(0, 400) }
        if ([string]::IsNullOrWhiteSpace($diagnostic)) { $diagnostic = 'No sanitized diagnostic was returned.' }
        Throw-Sanitized "A required .NET operation failed (exit code $exitCode): $diagnostic"
    }
    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function Invoke-Loader([string]$loaderMode, [switch]$AllowFailure) {
    $arguments = @(
        'run', '--project', (Join-Path $script:repositoryRoot 'src\ThroneForge.LoaderSmokeTest'),
        '-c', 'Release', '--no-build', '--', $loaderMode,
        '--game-path', $script:gameRoot,
        '--experiment-root', $script:experimentRoot,
        '--expected-fingerprint', $script:expectedFingerprint,
        '--repository-root', $script:repositoryRoot,
        '--bepinex-archive', $script:archivePath,
        '--official-digest', $script:expectedArchiveDigest
    )
    return Invoke-DotnetOperation $arguments -AllowFailure:$AllowFailure
}

function Invoke-PluginTool([string[]]$toolArguments, [switch]$AllowFailure) {
    $arguments = @(
        'run', '--project', (Join-Path $script:repositoryRoot 'src\ThroneForge.PluginSmokeTest'),
        '-c', 'Release', '--no-build', '--'
    ) + $toolArguments
    return Invoke-DotnetOperation $arguments -AllowFailure:$AllowFailure
}

function Get-OutputValue([string]$output, [string]$key) {
    $line = $null
    $prefix = $null
    foreach ($candidate in ($output -split '\r?\n')) {
        if ($candidate.StartsWith("$key=", [StringComparison]::Ordinal)) {
            $line = $candidate
            $prefix = "$key="
            break
        }

        if ($candidate.StartsWith($key + ':', [StringComparison]::Ordinal)) {
            $line = $candidate
            $prefix = $key + ':'
            break
        }
    }

    if ($null -eq $line) { Throw-Sanitized "The operation did not produce required evidence '$key'." }
    return $line.Substring($prefix.Length).Trim()
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
}

function New-Nonce {
    $bytes = New-Object byte[] 24
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}

function Assert-Archive {
    if ((Split-Path -Leaf $script:archivePath) -ne $script:expectedArchiveName) { Throw-Sanitized 'The archive filename is not the exact selected official BepInEx asset.' }
    if (-not (Test-Path -LiteralPath $script:archivePath -PathType Leaf)) { Throw-Sanitized 'The explicitly supplied BepInEx archive does not exist.' }
    $observed = (Get-FileHash -LiteralPath $script:archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($observed -ne $script:expectedArchiveDigest) { Throw-Sanitized 'The supplied BepInEx archive digest does not match the expected official asset digest.' }
    return $observed
}

function Invoke-PrivatePluginBuild {
    $buildRoot = Join-Path $script:experimentRoot 'plugin-build'
    $sourceRoot = Join-Path $buildRoot 'source'
    $packageRoot = Join-Path $buildRoot 'package'
    New-Item -ItemType Directory -Force -Path $sourceRoot, $packageRoot | Out-Null

    $dataDirectories = @(Get-ChildItem -LiteralPath $script:cleanGameRoot -Directory -Force | Where-Object { $_.Name.EndsWith('_Data', [StringComparison]::Ordinal) })
    if ($dataDirectories.Count -ne 1) { Throw-Sanitized 'The disposable copy does not have one unambiguous Unity data directory.' }
    $managedRoot = Join-Path $dataDirectories[0].FullName 'Managed'
    $bepInExCore = Join-Path $script:cleanGameRoot 'BepInEx\core\BepInEx.dll'
    $unityEngine = Join-Path $managedRoot 'UnityEngine.dll'
    $unityCore = Join-Path $managedRoot 'UnityEngine.CoreModule.dll'
    $netstandard = Join-Path $managedRoot 'netstandard.dll'
    foreach ($candidate in @($bepInExCore, $unityEngine, $unityCore, $netstandard)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { Throw-Sanitized 'Required local runtime evidence is missing from the disposable copy.' }
    }

    $tfmOutput = Invoke-PluginTool @('tfm', '--unity-version', '2022.3.62f2', '--assembly-path', $bepInExCore, '--assembly-path', $unityCore, '--assembly-path', (Join-Path $script:repositoryRoot 'artifacts\bin\ThroneForge.API\Release\netstandard2.1\ThroneForge.API.dll'), '--assembly-path', (Join-Path $script:repositoryRoot 'artifacts\bin\ThroneForge.Contracts\Release\netstandard2.1\ThroneForge.Contracts.dll'), '--assembly-path', $netstandard)
    $recommendation = Get-OutputValue $tfmOutput.Output 'recommendation'
    if ($recommendation -ne 'Netstandard21Candidate' -and $recommendation -ne 'Netstandard20Candidate') { Throw-Sanitized 'Local runtime metadata did not yield a conclusive supported plugin TFM candidate.' }
    $targetFramework = if ($recommendation -eq 'Netstandard21Candidate') { 'netstandard2.1' } else { 'netstandard2.0' }

    $apiPath = Join-Path $script:repositoryRoot "artifacts\bin\ThroneForge.API\Release\$targetFramework\ThroneForge.API.dll"
    $contractsPath = Join-Path $script:repositoryRoot "artifacts\bin\ThroneForge.Contracts\Release\$targetFramework\ThroneForge.Contracts.dll"
    if (-not (Test-Path -LiteralPath $apiPath -PathType Leaf) -or -not (Test-Path -LiteralPath $contractsPath -PathType Leaf)) { Throw-Sanitized 'The evidence-selected API/Contracts target is not built.' }
    $apiIdentity = Get-OutputValue (Invoke-PluginTool @('inspect', '--assembly-path', $apiPath, '--relative-path', 'ThroneForge.API.dll')).Output 'assembly-identity'
    $contractsIdentity = Get-OutputValue (Invoke-PluginTool @('inspect', '--assembly-path', $contractsPath, '--relative-path', 'ThroneForge.Contracts.dll')).Output 'assembly-identity'

    $templateRoot = Join-Path $script:repositoryRoot 'templates\synthetic-plugin-smoke'
    Copy-Item -LiteralPath (Join-Path $templateRoot 'ThroneForgeSyntheticPlugin.cs') -Destination $sourceRoot
    $projectText = Get-Content -LiteralPath (Join-Path $templateRoot 'PluginProject.csproj.template') -Raw
    $sourceText = Get-Content -LiteralPath (Join-Path $sourceRoot 'ThroneForgeSyntheticPlugin.cs') -Raw
    $projectText = $projectText.Replace('__TARGET_FRAMEWORK__', $targetFramework).Replace('__BEPINEX_CORE__', $bepInExCore).Replace('__UNITY_ENGINE__', $unityEngine).Replace('__UNITY_CORE_MODULE__', $unityCore).Replace('__THRONEFORGE_API__', $apiPath).Replace('__THRONEFORGE_CONTRACTS__', $contractsPath)
    $sourceText = $sourceText.Replace('__THRONEFORGE_API_IDENTITY__', $apiIdentity).Replace('__THRONEFORGE_CONTRACTS_IDENTITY__', $contractsIdentity)
    Write-Utf8NoBom (Join-Path $sourceRoot 'ThroneForge.M1.SyntheticSmoke.csproj') $projectText
    Write-Utf8NoBom (Join-Path $sourceRoot 'ThroneForgeSyntheticPlugin.cs') $sourceText

    $projectPath = Join-Path $sourceRoot 'ThroneForge.M1.SyntheticSmoke.csproj'
    Invoke-DotnetOperation @('restore', $projectPath) | Out-Null
    Invoke-DotnetOperation @('build', $projectPath, '-c', 'Release', '--no-restore') | Out-Null
    $pluginOutput = Join-Path $sourceRoot "bin\Release\$targetFramework\ThroneForge.M1.SyntheticSmoke.dll"
    if (-not (Test-Path -LiteralPath $pluginOutput -PathType Leaf)) { Throw-Sanitized 'The synthetic plugin build did not produce its primary assembly.' }

    Copy-Item -LiteralPath $pluginOutput -Destination (Join-Path $packageRoot 'ThroneForge.M1.SyntheticSmoke.dll')
    Copy-Item -LiteralPath $apiPath -Destination (Join-Path $packageRoot 'ThroneForge.API.dll')
    Copy-Item -LiteralPath $contractsPath -Destination (Join-Path $packageRoot 'ThroneForge.Contracts.dll')
    $manifestPath = Join-Path $buildRoot 'package-manifest.json'
    $packageOutput = Invoke-PluginTool @('package', '--package-root', $packageRoot, '--manifest-path', $manifestPath, '--target-framework', $targetFramework)
    $packageDigest = Get-OutputValue $packageOutput.Output 'package-sha256'
    return [pscustomobject]@{ BuildRoot = $buildRoot; PackageRoot = $packageRoot; ManifestPath = $manifestPath; TargetFramework = $targetFramework; ApiIdentity = $apiIdentity; ContractsIdentity = $contractsIdentity; PackageDigest = $packageDigest }
}

Assert-AbsolutePath $GamePath 'Game path'
Assert-AbsolutePath $ExperimentRoot 'Experiment root'
Assert-AbsolutePath $BepInExArchive 'BepInEx archive'
$script:gameRoot = Get-NormalizedPath $GamePath
$script:experimentRoot = Get-NormalizedPath $ExperimentRoot
$script:archivePath = Get-NormalizedPath $BepInExArchive
$script:expectedFingerprint = $ExpectedFingerprint.ToLowerInvariant()
$script:expectedArchiveDigest = $ExpectedBepInExDigest.ToLowerInvariant()
if ($script:expectedFingerprint -notmatch '^[0-9a-f]{64}$' -or $script:expectedArchiveDigest -notmatch '^[0-9a-f]{64}$') { Throw-Sanitized 'Expected fingerprints and digests must be lowercase-normalized SHA-256 values.' }
if ($script:expectedFingerprint -ne '1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d') { Throw-Sanitized 'The supplied fingerprint is not the fixed Task 6 evidence fingerprint.' }
if ($script:expectedArchiveDigest -ne '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4') { Throw-Sanitized 'The supplied archive digest is not the fixed Task 6 evidence digest.' }
if (-not (Test-Path -LiteralPath $script:gameRoot -PathType Container)) { Throw-Sanitized 'The explicit game path must identify an existing directory.' }
if (Test-SameOrDescendant $repositoryRoot $script:experimentRoot -or Test-SameOrDescendant $script:gameRoot $script:experimentRoot) { Throw-Sanitized 'The experiment root must be outside the repository and original game installation.' }
if (Test-Path -LiteralPath $script:experimentRoot -PathType Leaf) { Throw-Sanitized 'The experiment root must not be a file.' }
Assert-Archive

$cleanGameRoot = Join-Path $script:experimentRoot 'clean-game'
$script:cleanGameRoot = $cleanGameRoot
$evidenceRoot = Join-Path $script:experimentRoot 'evidence'
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null

if ($Mode -eq 'Cleanup') {
    if (-not (Test-SameOrDescendant $script:experimentRoot (Get-NormalizedPath $script:experimentRoot))) { Throw-Sanitized 'Cleanup target validation failed.' }
    if (Test-Path -LiteralPath $script:experimentRoot) { Remove-Item -LiteralPath $script:experimentRoot -Recurse -Force }
    Write-Output 'Cleanup completed inside the explicit experiment root.'
    exit 0
}

$preRuntime = Invoke-DotnetOperation @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.Discovery'), '-c', 'Release', '--no-build', '--', 'runtime-compatibility', '--game-path', $script:gameRoot, '--fingerprint', $script:expectedFingerprint, '--output-root', (Join-Path $evidenceRoot 'original-pre-runtime'), '--overwrite')
$selectedExecutableRelative = Get-OutputValue $preRuntime.Output 'Selected executable'
if ($selectedExecutableRelative -eq 'unknown') { Throw-Sanitized 'The original installation did not provide an unambiguous executable for the experiment.' }
$preOriginalManifest = Get-OutputValue (Invoke-PluginTool @('manifest', '--root', $script:gameRoot)).Output 'manifest-identity'

if ($Mode -eq 'Plan' -or $WhatIf) {
    Invoke-Loader Plan | Out-Null
    Write-Output 'Plan succeeded; no game files were copied or modified.'
    exit 0
}

if ($Mode -eq 'Rollback') {
    Invoke-PluginTool @('remove', '--clean-game', $cleanGameRoot) -AllowFailure | Out-Null
    Invoke-Loader Rollback | Out-Null
    Write-Output 'Explicit rollback completed.'
    exit 0
}

if ($Mode -eq 'Full' -and (Test-Path -LiteralPath $cleanGameRoot)) { Throw-Sanitized 'Full mode requires a fresh experiment root; an existing clean-game directory is not reused.' }

$loaderApplied = $false
$pluginDeployed = $false
$manualClosureRequired = $false
$result = 'Failed'
$failureSummary = 'The private smoke test did not complete.'
$package = $null
$nonce = New-Nonce
try {
    Invoke-Loader Prepare | Out-Null
    $baselineIdentity = Get-OutputValue (Invoke-PluginTool @('manifest', '--root', $cleanGameRoot)).Output 'manifest-identity'
    $baselineLaunch = Invoke-PluginTool @('launch', '--clean-game', $cleanGameRoot, '--experiment-root', $script:experimentRoot, '--executable', (Join-Path $cleanGameRoot $selectedExecutableRelative), '--nonce', $nonce) -AllowFailure
    if ($baselineLaunch.ExitCode -ne 0) { Throw-Sanitized 'The copied baseline launch was inconclusive or failed.' }
    Invoke-Loader Install | Out-Null
    $loaderApplied = $true
    $package = Invoke-PrivatePluginBuild
    $admission = Invoke-PluginTool @('admit', '--manifest-path', $package.ManifestPath, '--expected-fingerprint', $script:expectedFingerprint, '--adapter-id', 'throneforge.adapter', '--adapter-version', '1.0.0')
    if ((Get-OutputValue $admission.Output 'admission') -ne 'Approved') { Throw-Sanitized 'The exact package/game admission did not approve immediately before deployment.' }
    Invoke-PluginTool @('deploy', '--package-root', $package.PackageRoot, '--clean-game', $cleanGameRoot, '--manifest-path', $package.ManifestPath) | Out-Null
    $pluginDeployed = $true
    $launch = Invoke-PluginTool @('launch', '--clean-game', $cleanGameRoot, '--experiment-root', $script:experimentRoot, '--executable', (Join-Path $cleanGameRoot $selectedExecutableRelative), '--nonce', $nonce) -AllowFailure
    if ($launch.Output -match 'manual-closure-required=True') { $manualClosureRequired = $true; Throw-Sanitized 'The copied process requires manual closure before rollback.' }
    if ($launch.ExitCode -ne 0) { Throw-Sanitized 'The loader-enabled synthetic plugin launch was inconclusive or failed.' }
    $knownLogs = @((Join-Path $cleanGameRoot 'BepInEx\LogOutput.log'), (Join-Path $cleanGameRoot 'BepInEx\LogOutput.txt')) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    if ($knownLogs.Count -ne 1) { Throw-Sanitized 'The loader did not produce exactly one recognized BepInEx log file.' }
    $logVerification = Invoke-PluginTool @('verify-log', '--log-path', $knownLogs[0], '--nonce', $nonce, '--api-identity', $package.ApiIdentity, '--contracts-identity', $package.ContractsIdentity)
    if ($logVerification.ExitCode -ne 0) { Throw-Sanitized 'The BepInEx log did not prove the exact synthetic plugin bootstrap criteria.' }
    $pluginDeployed = $true
    $failureSummary = 'The disposable BepInEx profile loaded exactly one approved synthetic plugin and emitted the expected marker.'
    $result = 'Passed'
}
catch {
    $failureSummary = $_.Exception.Message -replace 'C:\\[^\r\n ]+', '<redacted-path>'
    if ($manualClosureRequired) { $result = 'Inconclusive' }
}
finally {
    if (-not $manualClosureRequired) {
        if ($pluginDeployed) { Invoke-PluginTool @('remove', '--clean-game', $cleanGameRoot) -AllowFailure | Out-Null }
        if ($loaderApplied) { Invoke-Loader Rollback -AllowFailure | Out-Null }
    }
    else {
        $marker = Join-Path $script:experimentRoot 'evidence\recovery-marker.json'
        Write-Utf8NoBom $marker '{"status":"ManualClosureRequired","markerPersisted":true,"nextOperation":"Rollback"}'
    }
}

$postOriginalManifest = Get-OutputValue (Invoke-PluginTool @('manifest', '--root', $script:gameRoot)).Output 'manifest-identity'
$postRuntime = Invoke-DotnetOperation @('run', '--project', (Join-Path $repositoryRoot 'src\ThroneForge.Discovery'), '-c', 'Release', '--no-build', '--', 'runtime-compatibility', '--game-path', $script:gameRoot, '--fingerprint', $script:expectedFingerprint, '--output-root', (Join-Path $evidenceRoot 'original-post-runtime'), '--overwrite') -AllowFailure
$originalUnchanged = $preOriginalManifest -eq $postOriginalManifest
$rollbackVerified = -not $manualClosureRequired -and $originalUnchanged -and $postRuntime.ExitCode -eq 0
if ($result -eq 'Passed' -and -not $rollbackVerified) { $result = 'Failed'; $failureSummary = 'The original installation post-verification or rollback verification failed.' }
if ($manualClosureRequired) { $rollbackVerified = $false }

$reportPath = Join-Path $repositoryRoot "docs\discovery\$($script:expectedFingerprint)-synthetic-plugin-smoke-test.md"
$reportLines = @(
    '# Thronefall Synthetic Plugin Smoke-Test Report', '',
    "- Base game fingerprint: $($script:expectedFingerprint)",
    '- Task version: M1 Task 6',
    "- Test timestamp UTC: $([DateTimeOffset]::UtcNow.ToString('O'))",
    "- Overall result: $result",
    "- Original complete manifest unchanged: $originalUnchanged",
    "- Original runtime post-verification passed: $($postRuntime.ExitCode -eq 0)",
    "- Disposable rollback verified: $rollbackVerified",
    "- Loader candidate: BepInEx 5 Unity Mono x64 $expectedLoaderVersion",
    "- Archive: $expectedArchiveName",
    "- Archive digest status: matched expected supplied SHA-256",
    "- Package digest: $($package.PackageDigest)",
    "- Package files: exactly 3 (synthetic plugin, ThroneForge.API, ThroneForge.Contracts)",
    "- Target framework evidence: $($package.TargetFramework)",
    '- Plugin marker: one nonce-bound readiness marker verified; nonce omitted from this report',
    '- BepInEx evidence: version, preloader, chainloader, one plugin, API/Contracts identities and zero fatal/errors were required',
    '- Explicit ThroneForge lifecycle calls: none; lifecycle marker would fail the result',
    "- Rollback/recovery state: $(if ($manualClosureRequired) { 'ManualClosureRequired; recovery marker persisted; no files were modified while process remained active.' } else { 'Rollback completed or was attempted before post-verification.' })",
    "- Failure or warning summary: $failureSummary",
    '- Privacy statement: no absolute paths, nonce, usernames, machine names, raw logs, binaries, archives, or private manifests are included.',
    '- Next permitted task: review this evidence and plan M1 Task 7; no further plugin or game functionality is claimed.'
)
Write-Utf8NoBom $reportPath ($reportLines -join [Environment]::NewLine)
Write-Output "Smoke-test result: $result"
Write-Output "Sanitized report: $([IO.Path]::GetFileName($reportPath))"
if ($result -eq 'Passed') { exit 0 }
exit 1
