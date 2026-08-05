# M1 Task 6: Disposable BepInEx synthetic-plugin smoke test

## Goal

Run one private, reversible BepInEx 5.4.23.5 smoke test in a disposable copy for the documented fingerprint. The repository implementation remains game-free and uses source-only synthetic fixtures in hosted CI.

## Tasks

1. Establish the external-tool project, architecture allowlist, CLI contract, and fail-closed experiment-root validation.
2. Add metadata-only TFM evidence, package manifest/digest, exact admission binding, and the source-only synthetic plugin template.
3. Reuse the existing loader-smoke services for baseline, transaction, launch, manifest, rollback, and original-installation verification.
4. Add nonce-bound marker parsing, deployment verification, sanitized reporting, and explicit `Passed`/`Failed`/`Inconclusive` state handling.
5. Add synthetic tests for all safety and evidence boundaries, then run local validation and hosted CI before any private run.
6. After CI is green, perform exactly one private run against the explicit local installation and inspect the sanitized report manually.
7. [x] Commit only the sanitized report and final documentation; run final local/hosted validation and stop before M1 Task 7.

## Task-6 hardening follow-up

The historical private run remains limited evidence because its template emitted build-time API/Contracts identity values and its deployment path did not yet own the experiment or recapture package bytes as one operation. Before Task 6 can be closed:

- [x] Require an atomically written, fingerprint-bound ownership record for Full, rollback, recovery, and cleanup.
- [x] Derive deployment readiness from the original/disposable roots, loader transaction state, complete loader-profile manifest, process state, and empty plugin root.
- [x] Capture the exact three package files once, validate metadata and target-framework evidence, compare the current package against its saved manifest, rerun admission, and deploy the captured bytes.
- [x] Remove partial plugin files/directories on every deployment failure and verify the complete pre-deployment manifest.
- [x] Enforce exact package shape, managed IL/no-native/no-PInvoke/no-module-initializer rules, expected BepInEx metadata, and one public plugin implementation.
- [x] Emit runtime API/Contracts identities from actual loaded assemblies and compare them in the log parser.
- [x] Add metadata-only public API/Contracts net10.0/netstandard2.1 parity checks and structured recovery-marker parsing.
- [x] Run corrected current-head hosted CI, inspect every TRX artifact, then perform exactly one fresh private rerun and update the report with actual runtime identity and admit-and-deploy evidence.
- [x] Diagnose the deployment-state mismatch: the Task-6 service used legacy `baseline.json` while Task 3 writes `baseline-copy-manifest.json`; centralize both state paths and bind the transaction check to `baseline.DisposableManifest`.
- [x] Add a positive real-service context-derivation test plus negative canonical-path and state-mismatch tests; expose sanitized state-gate failure categories.
- [ ] Do not begin M1 Task 7.

## Planned repository surfaces

- `src/ThroneForge.PluginSmokeTest/`
- `tests/ThroneForge.PluginSmokeTest.Tests/`
- `templates/synthetic-plugin-smoke/`
- `tools/plugin-smoke-test/`
- `docs/discovery/<fingerprint>-synthetic-plugin-smoke-test.md`

## Fixed evidence

- Fingerprint: `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7`
- Backend: Mono; architecture: x64; Unity: `2022.3.62f2`
- Loader: official BepInEx Unity Mono x64 `5.4.23.5`
- Archive: `BepInEx_win_x64_5.4.23.5.zip`
- Expected archive SHA-256: `82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4`

## Safety gates

No private experiment starts until source-only tests, required local checks, and hosted Windows/Linux CI pass. The original installation is read-only, the experiment is external to the repository and game root, and no binary, raw log, nonce, absolute path, or private manifest is committed.

## Recorded private result

The historical private run remains limited `PassedWithWarnings` evidence because it emitted build-time API/Contracts identity values. The subsequent fresh attempt in external profile `final15` remains `Failed`: it recaptured and metadata-validated the exact three-file package but `admit-and-deploy` rejected disposable-profile state before plugin files were written. Rollback, disposable restoration, and original runtime/manifest post-checks passed. The corrected fresh run then passed with package digest `0193d57e79c3c61057bc3a296f4529d0fda32ba43c34e2d82dc2ca59a67f42a9`, admission binding digest `1fd37cd4e3eebf82a80a3f3ba30017272d99d46f95da7483cff1f086fba47b0f`, actual runtime API/Contracts identities, one plugin and nonce marker, successful removal and rollback, and complete original/disposable post-verification. M1 Task 7 remains unstarted.

Final pre-private hosted validation for correction head `bda540f9f4c97868d2469c0d5f48826269528e8f` passed in run `30998768487` on Windows and Ubuntu with SDK `10.0.100`; each runner uploaded 13 TRX files representing 279 tests with zero failures, errors, or skips. Both artifacts were downloaded and parsed independently, with no TRX overwrite warning.
