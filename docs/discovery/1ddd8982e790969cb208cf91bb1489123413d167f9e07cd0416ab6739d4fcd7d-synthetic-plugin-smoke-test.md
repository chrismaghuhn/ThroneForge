# Thronefall Synthetic Plugin Smoke-Test Report

## Base game fingerprint

`1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`

Fingerprint algorithm: `throneforge-game-fingerprint-v1`

## Task and timestamp

- Task version: M1 Task 6
- Test timestamp UTC: 2026-08-05T08:42:46.7174448+00:00
- Backend: Mono
- Executable architecture: x64
- Unity evidence: 2022.3.62f2
- Target framework evidence: `netstandard2.1`

## Original installation verification

- Preflight fingerprint: matched the expected fingerprint.
- Preflight runtime readiness: `ReadyForReversibleTest`.
- Preflight loader indicators: absent.
- Postflight complete manifest: unchanged.
- Postflight fingerprint: matched the expected fingerprint.
- Postflight runtime/readiness verification: passed.
- Postflight loader indicators: absent.

The original installation was used as read-only input and was not used as the loader launch target.

## Disposable profile verification

- Copy destination: external disposable profile; absolute location intentionally omitted.
- Copied baseline fingerprint: matched the expected fingerprint.
- Copied baseline runtime readiness: `ReadyForReversibleTest`.
- Copied baseline loader indicators: absent before installation.
- Baseline launch: passed; the copied executable started and exited within the bounded observation window.

## Loader candidate and official release verification

- Candidate: BepInEx 5 Unity Mono x64 `5.4.23.5`.
- Official source: `BepInEx/BepInEx`, GitHub release tag `v5.4.23.5`.
- Asset: `BepInEx_win_x64_5.4.23.5.zip`.
- Asset ID: `352395699`.
- Asset size: `639118` bytes.
- Official vendor digest: `82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4`.
- Observed SHA-256: `82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4`.
- Archive digest status: matched.

## Secure extraction and transaction

- ZIP entry validation: passed; rooted paths, traversal, duplicate destinations, link entries, unsafe names, and excessive expansion were rejected or absent.
- Extraction target: external validated staging directory.
- Transaction: loader installation applied and verified in the disposable copy; the synthetic package was admitted and deployed only after exact package/game evidence matched.
- Package: exactly three files — synthetic plugin, `ThroneForge.API`, and `ThroneForge.Contracts`.
- Package SHA-256: `54ced96a142040cc96374bbb86d28f5a0e2735271102aff2cf4486a40d549020`.

## Loader-enabled launch and generated evidence

- Launch target: disposable copy only.
- Process observation: started, remained inside the experiment root, and exited gracefully within the bounded window.
- BepInEx version observed: `5.4.23.5`.
- Preloader: initialized.
- Chainloader: initialized.
- Plugins discovered during the synthetic-plugin launch: `1`.
- Synthetic readiness marker: exactly one nonce-bound marker matched; the nonce is omitted from this report.
- API/Contracts identities: matched the evidence-selected package assemblies.
- Explicit ThroneForge lifecycle calls: none; no lifecycle marker was observed.
- Warnings: `0`.
- Errors: `0`.
- Fatal errors: `0`.
- No file was added to the original installation.

## Rollback and post-verification

- Synthetic plugin removal: completed.
- Loader transaction rollback: completed and independently reported verified.
- Disposable post-rollback fingerprint: matched the expected fingerprint.
- Disposable complete baseline manifest: restored and matched.
- Original complete manifest: unchanged.
- Original runtime/readiness post-verification: passed.

## Overall result

`Passed`

The disposable BepInEx profile loaded exactly one approved source-generated synthetic plugin and emitted the expected marker. This result proves only the recorded synthetic bootstrap for this fingerprint. It does not prove a real Thronefall plugin, final plugin target-framework compatibility beyond the local evidence used for the build, Harmony compatibility, lifecycle bindings, game APIs, catalog extraction, or custom waves.

## Security and privacy statement

No absolute paths, usernames, machine names, nonce, raw logs, binaries, archives, complete private manifests, transaction state, recovery markers, or save-game information are included. Raw experiment evidence remains outside the repository. The original installation was not modified or launched.

## Next permitted task

Plan M1 Task 7 separately. Do not infer game-facing plugin compatibility or begin lifecycle, game API, catalog, Harmony, or custom-wave work from this report alone.
