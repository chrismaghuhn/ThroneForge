# Changelog

All notable ThroneForge changes are documented here.

## Unreleased

- Fixed CI test-result preservation so every test project gets its own TRX artifact and the workflow verifies aggregate completeness before upload.
- Completed and hardened M1 discovery task 1 with a local-only, metadata-based Thronefall installation inspector, protected output boundaries, deterministic executable selection, and a sanitized fingerprint report; hosted verification passed on Windows and Ubuntu.
- Added M1 task 2's bounded runtime-compatibility inspection: metadata-only managed-runtime profiling, conservative target-framework recommendations, bounded Unity-version evidence, loader-indicator inventory, and a provisional official BepInEx candidate matrix.
- Started M1 task 2 hardening: shared fingerprint-v1 verification, separate candidate/readiness assessment, evidence-specific TFM confidence, and distinct conflict/missing/limitation/warning reporting. Hosted verification for this hardening branch is pending.

### Added

- M0 repository bootstrap and architecture skeleton.
- Architecture boundary tests for project and assembly references.
- M0 hardening branch with exact SDK pinning, repository dependency scans, CI TRX artifacts, and public project documentation.

### Not yet implemented

- Further private runtime-compatibility evidence and all game-facing runtime integration remain incomplete.
- Runtime integration, loaders, game API bindings, and custom-wave support remain unimplemented.
