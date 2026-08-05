# Thronefall Synthetic Plugin Smoke-Test Report

- Base game fingerprint: `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`
- Task version: M1 Task 6
- Loader candidate: official BepInEx 5 Unity Mono x64 `5.4.23.5`
- Archive: `BepInEx_win_x64_5.4.23.5.zip`
- Archive digest status: matched the expected official SHA-256
- Privacy statement: no absolute paths, nonce, usernames, machine names, raw logs, binaries, archives, or private manifests are included.

## Historical failed attempt

- Result: `Failed`
- Failure category: `baseline-state-path-mismatch`
- Cause: the Task-6 deployment service looked for the legacy `baseline.json`, while Task 3 persisted `baseline-copy-manifest.json`.
- Deployment effect: context derivation stopped before deployment; no plugin files were written.
- Recovery: loader rollback, disposable restoration, original complete-manifest equality, and original runtime post-verification passed.
- Evidence limit: this attempt did not establish actual runtime API/Contracts resolution.

## Corrected private result

- Result: `Passed`
- Profile: one fresh external disposable experiment root; the original installation was not modified.
- Package capture: all three package files were captured, metadata-validated, hashed, admitted, and deployed from the same captured bytes.
- Package digest: `0193d57e79c3c61057bc3a296f4529d0fda32ba43c34e2d82dc2ca59a67f42a9`
- Admission binding digest: `1fd37cd4e3eebf82a80a3f3ba30017272d99d46f95da7483cff1f086fba47b0f`
- Package shape: exactly three files — synthetic plugin, `ThroneForge.API`, and `ThroneForge.Contracts`.
- Loader transaction binding: validated against the canonical disposable baseline manifest `baseline-copy-manifest.json`.
- BepInEx evidence: version `5.4.23.5`, preloader initialized, chainloader initialized, zero errors and zero fatal errors.
- Plugin evidence: exactly one expected synthetic plugin was discovered.
- Nonce marker: exactly one matching readiness marker.
- Runtime API identity: `ThroneForge.API, Version=1.0.0.0`.
- Runtime Contracts identity: `ThroneForge.Contracts, Version=1.0.0.0`.
- Lifecycle evidence: no lifecycle marker; no explicit ThroneForge lifecycle call was made.
- Plugin removal: verified.
- Loader rollback: verified.
- Disposable profile: complete baseline restoration verified.
- Original installation: complete pre/post manifest equality and runtime post-verification passed; the original remained free of detected loader indicators.

## Interpretation and limits

This result verifies the disposable synthetic-plugin bootstrap path for the documented fingerprint and the selected BepInEx release. It does not verify Thronefall game APIs, Harmony compatibility, lifecycle bindings, catalog extraction, custom waves, or a production plugin target framework.

Assembly loading remains full-trust. The synthetic plugin was not used to inspect game methods or invoke game behavior. M1 Task 7 remains unstarted.
