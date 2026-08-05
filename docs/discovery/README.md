# Local Thronefall discovery

The discovery tool records conservative, sanitized evidence from an explicitly supplied, legally obtained Thronefall installation. It is an external inspection tool, not a loader or mod runtime.

## Invocation

From the repository root, run:

```powershell
dotnet run --project src/ThroneForge.Discovery -- inspect `
  --game-path "C:\Path\To\Thronefall" `
  --output-root "docs/discovery"
```

`--game-path` must be an absolute directory path. The optional `--overwrite` flag is required to replace a report with the same fingerprint. Without it, an existing report is left unchanged and the command fails. The tool prints only the generated file name, classification, architecture, and fingerprint; it does not echo the supplied installation path.

The output root is validated before any directory is created. It must be outside the inspected game root, must not be the game root or a descendant, and must not be an existing symbolic link/reparse point or pass through one of its parents. Similarly prefixed sibling paths are allowed.

For the second bounded investigation, run:

```powershell
dotnet run --project src/ThroneForge.Discovery -- runtime-compatibility `
  --game-path "C:\Path\To\Thronefall" `
  --fingerprint "<fingerprint-from-the-discovery-report>" `
  --output-root "docs/discovery"
```

This command writes `<fingerprint>-runtime-compatibility.md`. It recomputes fingerprint v1 from the current installation before inspecting runtime compatibility and fails without creating output when the supplied fingerprint does not match. `--overwrite` is required for an existing report. The shared snapshot is read-only and does not create or replace the Task 1 report.

## Evidence and safety boundaries

The tool reads directory and file metadata below the supplied root, selected PE headers, selected small compatibility files, and selected file contents for SHA-256 hashing. It never launches the game, modifies the installation, loads an assembly, executes code, follows symbolic links or reparse points, scans the computer or Steam library, copies binaries, decompiles source, or writes outside the requested output directory.

Reports contain relative paths only. They do not contain the absolute installation path, usernames, machine names, filesystem timestamps, environment variables, arbitrary directory listings, or copied binary contents.

## Runtime-compatibility evidence

The `runtime-compatibility` command reads only selected framework assemblies and layout indicators beneath the explicit game root. Managed assemblies are inspected with `PEReader` and `MetadataReader`; they are never loaded, executed, decompiled, or searched for game methods or private types. The report records assembly identity, assembly version, the `TargetFrameworkAttribute` when safely decodable, and a small allowlist of framework references.

The managed-runtime classification requires multiple compatible layout signals. `Mono` and `IL2CPP` are evidence classifications, not proof of loader compatibility. `Conflicting` means signals for both backends are present, and `Unknown` means the bounded evidence is insufficient.

Target-framework output is a recommendation only:

- `netstandard2.0 candidate` is supported by direct metadata or by `netstandard.dll` plus Unity 2021.1 or older evidence.
- `netstandard2.1 candidate` is supported by direct metadata or by `netstandard.dll` plus Unity 2021.2 or newer evidence.
- `Framework-compatible but exact TFM unresolved` is used when `netstandard.dll` exists but Unity-version evidence cannot distinguish 2.0 from 2.1.
- `net46 candidate` and `net35 fallback candidate` are conservative `mscorlib`-based recommendations.
- `Conflicting` and `Unknown` prevent a confident target selection.

Unity-version evidence is bounded to `UnityVersion.txt`, the beginning of `globalgamemanagers`, and version-resource metadata from the selected executable and `UnityPlayer.dll`. Equivalent Unity version-resource build-number formats are normalized before conflict checking; raw relative evidence sources remain listed in the report.

The loader inventory reports only exact-name indicators such as `BepInEx/`, `MelonLoader/`, Doorstop configuration files, `winhttp.dll`, `version.dll`, `Mods/`, and `Plugins/`. A DLL filename is never treated as proof of a loader. No indicator is executed, changed, deleted, or identified beyond its safe bounded classification. Reports keep `Conflict`, `Missing`, `Limitation`, and `Warning` evidence categories separate; a bounded prefix limitation is not treated as missing evidence when the version was found in that prefix.

As of 2026-08-03, the official BepInEx release metadata records BepInEx 5.4.23.5 as the stable LTS line and BepInEx 6.0.0-pre.2 as a pre-release. For Mono Unity plus x64 evidence, BepInEx 5 is the provisional candidate for a later clean-profile smoke test. Candidate selection is separate from readiness: any non-absent loader/bootstrap indicator blocks `ReadyForReversibleTest`, and conflicts or insufficient evidence produce a blocked/unsupported readiness result. This is not a loader selection or compatibility claim; it remains provisional until the reversible smoke test succeeds. Sources: [BepInEx releases](https://github.com/BepInEx/BepInEx/releases), [BepInEx plugin target-framework guidance](https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/2_plugin_start.html).

## Backend classification

The names below are layout indicators and are not proof of game internals:

- `Mono` requires at least two compatible Mono signals, such as a Unity `*_Data/Managed` directory, `Assembly-CSharp.dll` beneath it, or a local `mono`/`MonoBleedingEdge` runtime directory.
- `IL2CPP` requires at least two compatible IL2CPP signals, such as `GameAssembly.dll`, a Unity `*_Data/il2cpp_data` directory, and `global-metadata.dat` beneath that directory.
- `Ambiguous` means signals from both backends were detected.
- `Unknown` means the evidence is absent or fewer than two compatible signals support one backend.

All detected signals and missing/conflicting explanations are written to the report.

## Main executable selection

Top-level `*_Data` directories provide the first executable-name signal. With exactly one such directory, the matching `<base>.exe` is preferred. The tool then tries an executable matching the installation directory name, then selects a single remaining valid PE executable. Multiple remaining PE executables are treated as ambiguous and produce architecture `Unknown`; no alphabetical fallback is used.

## Fingerprint v1

The fingerprint is a lowercase SHA-256 digest of UTF-8, LF-separated, invariant-culture lines using the version marker `throneforge-game-fingerprint-v1`, backend classification, executable architecture, verified Unity-version evidence, and a sorted set of selected compatibility files. Each selected file contributes its normalized relative path, byte size, and lowercase SHA-256. Absolute paths, usernames, machine identifiers, local timestamps, and environment values are excluded. The entire installation is never hashed indiscriminately; selected file reads are limited to 64 MiB each.

## Limitations and next steps

Unity version is reported only when stable local evidence is readable. PE architecture supports x86, x64, and Arm64; malformed or unsupported headers produce `Unknown`. A classification does not establish loader compatibility, lifecycle behavior, game API bindings, or a target framework. Those questions require later, evidence-backed discovery tasks and must not be inferred from this report.

Selected compatibility files are opened once for length validation and SHA-256 hashing, with a 64 MiB per-file limit. Path, permission, and filesystem failures are returned as sanitized discovery errors without absolute paths or stack traces in normal CLI output.

## M1 task 3: reversible loader bootstrap smoke test

The loader smoke-test harness is local-only and accepts an explicit original game path, an explicit external experiment root, the expected fingerprint, and optionally the exact official archive:

```powershell
./tools/loader-smoke-test/Invoke-ThroneForgeLoaderSmokeTest.ps1 `
  -Mode Full `
  -GamePath "C:\Path\To\Thronefall" `
  -ExperimentRoot "D:\ThroneForgeExperiments\<fingerprint>" `
  -ExpectedFingerprint "<fingerprint>" `
  -AllowDownload
```

`-AllowDownload` is required for the harness to query the official GitHub release. It accepts only `BepInEx/BepInEx`, tag `v5.4.23.5`, and asset `BepInEx_win_x64_5.4.23.5.zip`. An explicit local archive is accepted only when its filename matches and its observed SHA-256 is recorded. `Plan`, `Prepare`, `Baseline`, `Install`, `Launch`, `Verify`, `Rollback`, `Full`, explicit manifest-backed `Resume`, and the separate `Cleanup` mode are available; the experiment root must remain outside the repository and original installation.

The harness first recomputes fingerprint v1, captures a complete original manifest, and verifies runtime readiness. A fresh `Full` run rejects any existing `clean-game` directory. The explicit `Resume` mode accepts an existing copy only with a schema- and task-versioned baseline manifest, complete manifest equality, current readiness, and absent loader indicators. Fresh preparation copies the complete installation without following reparse points and requires the copied fingerprint to match. It launches only the copied executable for a bounded baseline observation, securely validates archive paths and sizes before extraction, applies the loader transactionally, and rolls back the copy after evidence collection. After rollback it compares the complete disposable manifest and repeats the original fingerprint, runtime/readiness, loader-indicator, and complete-manifest checks. It never launches or writes to the original installation, never adds a plugin, and never executes Harmony or game-internal code.

The committed report destination is derived internally as `<repository-root>/docs/discovery/<fingerprint>-loader-smoke-test.md`; the CLI does not accept an arbitrary report path. A manual-closure state writes only a local recovery marker under the external experiment root and reports the redacted `Rollback` command needed after graceful closure. No automatic cleanup is performed while a copied process is active.

The bootstrap result is classified as `Passed`, `PassedWithWarnings`, `Failed`, or `Inconclusive`. Preloader/chainloader log evidence can satisfy the initialization-evidence requirement when a separate configuration file is not generated. A pass proves only loader bootstrap for the exact fingerprint; it does not prove plugin target-framework compatibility, Harmony compatibility, lifecycle bindings, game APIs, catalog extraction, or custom waves. Raw logs, archives, manifests, and copied files remain in the external experiment root and are never committed.

## M1 task 6: disposable synthetic-plugin smoke test

The Task-6 harness is local-only and requires every path and evidence value explicitly:

```powershell
./tools/plugin-smoke-test/Invoke-ThroneForgeSyntheticPluginSmokeTest.ps1 `
  -Mode Full `
  -GamePath "C:\Path\To\Thronefall" `
  -ExperimentRoot "D:\ThroneForgeExperiments\<fingerprint>" `
  -BepInExArchive "D:\ThroneForgeExperiments\downloads\BepInEx_win_x64_5.4.23.5.zip" `
  -ExpectedFingerprint "<fingerprint>" `
  -ExpectedBepInExDigest "<official-or-user-supplied-digest>"
```

`Full` requires a new experiment root; `Plan` performs only read-only preflight, `Rollback` is the explicit recovery operation, and `Cleanup` is separate and limited to the supplied experiment root. The harness never scans for a game, Steam library, archive, or experiment directory.

Before private use, the tool recomputes fingerprint v1 and runtime readiness, verifies the exact official archive digest, builds the source-only template from metadata-selected `netstandard2.0` or `netstandard2.1` evidence, and creates a three-file package manifest. The admission gate binds package identity/hash, approval, adapter evidence, and the fixed game fingerprint immediately before deployment.

The disposable plugin is deployed only below `clean-game/BepInEx/plugins/dev.throneforge.m1.synthetic-smoke/`. The launch uses only the random nonce environment variable. Success requires BepInEx `5.4.23.5`, preloader, chainloader, exactly one plugin, one matching sanitized marker, matching API/Contracts identities, no lifecycle marker, no fatal/error evidence, graceful close, plugin removal, loader rollback, complete manifest restoration, and unchanged original-installation post-checks.

No nonce, absolute path, raw log, binary, archive, complete private manifest, or recovery marker is committed. A pass proves only this synthetic plugin bootstrap for the exact documented fingerprint. It does not prove a real Thronefall plugin, final plugin TFM compatibility beyond the local evidence used for the build, Harmony compatibility, lifecycle bindings, game APIs, catalog extraction, or custom waves.

The private Task-6 run passed for the documented fingerprint. The sanitized result is [the fingerprint-specific report](1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-synthetic-plugin-smoke-test.md). Raw logs, the disposable copy, package files, transaction state, and rollback evidence remain outside the repository.

## M1 task 7: public Unity lifecycle binding smoke test

Task 7 is limited to the public Unity event `UnityEngine.Application.quitting`, identified as `unity-application-quitting-v1`. The source metadata is inspected with `PEReader`/`MetadataReader` before a private run and must prove a public static `System.Action` event with public static add/remove accessors. The external validator never loads `UnityEngine.CoreModule.dll` and no Unity binary is committed.

The source-only lifecycle package contains exactly `ThroneForge.M1.LifecycleSmoke.dll`, `ThroneForge.API.dll`, and `ThroneForge.Contracts.dll`. It reuses the Task-6 ownership, exact-byte package capture, admission, transactional deployment, bounded launch, removal, rollback, and original/disposable post-verification workflow. Its synthetic context exposes only the documented mod identity and reports every capability unavailable.

The lifecycle host is synchronous-only and exactly-once: BepInEx `Awake` initializes one synthetic mod, `Application.quitting` shuts down that same instance, and `OnDestroy` is cleanup fallback only. A private pass requires one nonce-bound marker for initialization, one direct quitting-event marker, and one shutdown marker in sequence `1,2,3`, plus actual runtime API/Contracts identities and zero fatal/error evidence. Missing or unstable logs are bounded failures; a process requiring manual closure is inconclusive and never modified while active.

Invocation is local-only and must happen only after repository tests and hosted synthetic Windows/Linux CI pass:

```powershell
./tools/plugin-smoke-test/Invoke-ThroneForgeLifecycleBindingSmokeTest.ps1 `
  -Mode Full `
  -GamePath "C:\Path\To\Thronefall" `
  -ExperimentRoot "D:\ThroneForgeExperiments\<fingerprint>" `
  -BepInExArchive "D:\ThroneForgeExperiments\downloads\BepInEx_win_x64_5.4.23.5.zip" `
  -ExpectedFingerprint "<fingerprint>" `
  -ExpectedBepInExDigest "<official-digest>"
```

The committed output, if the one permitted private run passes or is inconclusive, is the sanitized fingerprint-specific report `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d-lifecycle-binding.md`. It contains no nonce, absolute path, raw log, binary, private manifest, machine identity, or process identity. The binding is described as a public Unity lifecycle binding observed while Thronefall was running, not as a verified Thronefall-internal method. No game API, Harmony, gameplay state, catalog, save, wave, async lifecycle, or cross-version compatibility is claimed.
