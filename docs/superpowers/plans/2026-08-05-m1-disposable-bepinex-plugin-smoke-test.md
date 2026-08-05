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
7. Commit only the sanitized report and final documentation; run final local/hosted validation and stop before M1 Task 7.

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
