# Changelog

All notable ThroneForge changes are documented here.

## Unreleased

- Started M1 Task 5 hardening from reviewed head `10897f7b03d45f1e470b5930a9dc1341939cde6f` on `agent/m1-plugin-load-smoke-test-hardening`: strict metadata-only single-assembly closure, shared API/Contracts resolution, module-initializer/native/sidecar rejection, public closed contract-shape validation, exact-byte capture, and bounded collectible unload observation. No private Thronefall/BepInEx experiment was run.
- Started M1 Task 5 on `agent/m1-plugin-load-smoke-test` from merged `main@5f4b4dd`: a repository-only synthetic plugin-load probe that re-runs the artifact/game-bound admission gate immediately before loading a test fixture. No Thronefall plugin is loaded or invoked.
- Added the synthetic `ThroneForge.PluginLoadTest` probe, collectible-context loading, bounded exact-byte hashing, sanitized load results, source-only single/duplicate fixtures, and 14 fail-closed regression tests. Local SDK `10.0.110` validation passed; exact SDK `10.0.100` formatting remains hosted-only on this workstation.
- Final Task-5 hosted run `30880625516` passed on Windows and Ubuntu with SDK `10.0.100`, 12 TRX files, and 226 tests per runner. The TRX completeness check now counts only `IsTestProject=true` projects so source-only fixtures do not create false failures; no overwrite warning occurred.
- Completed and merged M1 Task 4 trust-evidence hardening in PR #4 at `5f4b4dd`; hosted run `30878236039` passed on Windows and Ubuntu with SDK `10.0.100`, 11 TRX files, and 212 tests per runner.
- Completed M1 Task 4 trust-evidence hardening on `agent/m1-plugin-runtime-boundary-hardening`: canonical mod identity/version rules, artifact- and fingerprint-bound integrity/approval/adapter evidence, deterministic admission bindings, and fail-closed reason-code precedence. Implementation head `382fe877f5685bb71d77ee6b5cf04afc8a57a7bf` passed hosted run `30878049044` on Windows and Ubuntu with 11 TRX files and 212 tests per runner using SDK `10.0.100`; no plugin was loaded.
- Completed the bounded M1 Task 4 plugin/runtime boundary slice: portable code-mod identity/integrity/approval contracts, `IThroneForgeMod` lifecycle boundary, deterministic pre-load admission gate, ADR-0006, and architecture tests. Final documentation head `5d7c69a` passed hosted run `30868670093` on Windows and Ubuntu with SDK `10.0.100`, 11 TRX files, and 175 tests per runner; no plugin was loaded.
- Started M1 Task 4 on `agent/m1-plugin-runtime-boundary` from merged main commit `06554d845a9fe46132c1a19ec0c2f18b8722acf2`. The task is limited to portable full-trust code-mod contracts and a deterministic pre-load admission boundary; it does not load a plugin or claim game/runtime compatibility.
- Implemented the final Task 3 transaction-state correction: atomic versioned loader state, persisted-entry and backup containment validation, complete applied-profile verification, staged `Verify` bootstrap-evidence requirements, fail-closed stale/failed transaction handling, and consistent no-transaction report wording. Hosted run `30866207996` validated the implementation on Windows and Ubuntu with SDK `10.0.100`, 11 TRX files, and 165 tests per runner; the private loader experiment was not rerun.
- Added the final Task 3 hardening review corrections: evidence-derived original post-check claims, saved-baseline requirements for all staged modes, and explicit recovery-marker persistence status in reports and CLI output. Hosted run `30864308531` validated the implementation on Windows and Ubuntu with 11 TRX files and 154 tests per runner; the private loader experiment was not rerun.
- Fixed CI test-result preservation so every test project gets its own TRX artifact and the workflow verifies aggregate completeness before upload.
- Completed and hardened M1 discovery task 1 with a local-only, metadata-based Thronefall installation inspector, protected output boundaries, deterministic executable selection, and a sanitized fingerprint report; hosted verification passed on Windows and Ubuntu.
- Added M1 task 2's bounded runtime-compatibility inspection: metadata-only managed-runtime profiling, conservative target-framework recommendations, bounded Unity-version evidence, loader-indicator inventory, and a provisional official BepInEx candidate matrix.
- Completed M1 task 2 hardening: shared fingerprint-v1 verification, separate candidate/readiness assessment, evidence-specific TFM confidence, and distinct conflict/missing/limitation/warning reporting. Hosted Windows/Linux verification passed in run `30852340193` with 10 TRX files and 99 tests per runner.
- M1 task 2 was merged by PR #2 at `d3f1bb4fde9f77efbb84349f440385cc89002c86`; main run `30853440786` passed with 10 TRX files and 99 tests per runner. Started M1 task 3 as a local-only reversible BepInEx bootstrap smoke test; no loader result or plugin is claimed yet.
- Completed the historical M1 task 3 bootstrap experiment for fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`: official BepInEx `5.4.23.5` initialized its preloader and chainloader with zero custom plugins, and the disposable copy's compatibility fingerprint was restored. Complete historical manifests were not retained, so the report does not claim complete-manifest equality. Pre-hardening hosted run `30857962381` passed on Windows and Ubuntu with SDK `10.0.100`, 11 TRX files, and 130 tests per runner. Plugin, Harmony, lifecycle, and game API compatibility are not claimed.
- Started M1 task 3 hardening: complete original/disposable manifest verification, fresh-profile and manifest-backed resume gates, post-apply rollback guarding, recovery-state handling, and repository-derived report-path validation. The historical private result remains explicitly limited because complete manifests were not retained; no private experiment was rerun.
- Hosted hardening validation run `30860012681` passed on Windows and Ubuntu for implementation commit `1643cdb4e26f3e5d0890b7b203df8904ec77795c`: SDK `10.0.100`, 11 TRX files, 146 tests per runner, 0 failures/errors/skips, and no overwrite warnings.

### Added

- M0 repository bootstrap and architecture skeleton.
- Architecture boundary tests for project and assembly references.
- M0 hardening branch with exact SDK pinning, repository dependency scans, CI TRX artifacts, and public project documentation.

### Not yet implemented

- M1 Task 5 synthetic plugin-load probing and its closure hardening are repository-only. No Thronefall plugin loading, loader integration, game API binding, lifecycle integration, or custom-wave functionality is implemented.
- Runtime integration, loaders, game API bindings, and custom-wave support remain unimplemented.
