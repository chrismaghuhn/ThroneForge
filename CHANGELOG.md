# Changelog

All notable ThroneForge changes are documented here.

## Unreleased

- Fixed CI test-result preservation so every test project gets its own TRX artifact and the workflow verifies aggregate completeness before upload.
- Completed and hardened M1 discovery task 1 with a local-only, metadata-based Thronefall installation inspector, protected output boundaries, deterministic executable selection, and a sanitized fingerprint report; hosted verification passed on Windows and Ubuntu.
- Added M1 task 2's bounded runtime-compatibility inspection: metadata-only managed-runtime profiling, conservative target-framework recommendations, bounded Unity-version evidence, loader-indicator inventory, and a provisional official BepInEx candidate matrix.
- Completed M1 task 2 hardening: shared fingerprint-v1 verification, separate candidate/readiness assessment, evidence-specific TFM confidence, and distinct conflict/missing/limitation/warning reporting. Hosted Windows/Linux verification passed in run `30852340193` with 10 TRX files and 99 tests per runner.
- M1 task 2 was merged by PR #2 at `d3f1bb4fde9f77efbb84349f440385cc89002c86`; main run `30853440786` passed with 10 TRX files and 99 tests per runner. Started M1 task 3 as a local-only reversible BepInEx bootstrap smoke test; no loader result or plugin is claimed yet.
- Completed the private M1 task 3 bootstrap experiment for fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`: official BepInEx `5.4.23.5` initialized its preloader and chainloader with zero custom plugins, then the disposable copy was rolled back. Hosted synthetic CI verification remains pending; plugin, Harmony, lifecycle, and game API compatibility are not claimed.

### Added

- M0 repository bootstrap and architecture skeleton.
- Architecture boundary tests for project and assembly references.
- M0 hardening branch with exact SDK pinning, repository dependency scans, CI TRX artifacts, and public project documentation.

### Not yet implemented

- The M1 task 3 disposable-profile loader bootstrap experiment, private evidence review, and hosted synthetic validation remain incomplete.
- Runtime integration, loaders, game API bindings, and custom-wave support remain unimplemented.
