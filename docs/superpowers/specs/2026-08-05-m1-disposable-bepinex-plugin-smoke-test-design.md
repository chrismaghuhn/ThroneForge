# M1 Task 6: Disposable BepInEx synthetic-plugin smoke-test design

## Scope

Task 6 is one private, reversible experiment for the already documented game fingerprint. It uses only official BepInEx 5.4.23.5, a complete disposable copy, and one source-generated synthetic plugin. It does not test Thronefall APIs, game methods, Harmony, lifecycle bindings, catalog extraction, or custom waves.

The original installation is read-only input. The repository contains only the source template, metadata/package/deployment validators, synthetic tests, harness, and a sanitized fingerprint-specific report.

## Evidence binding

The fixed input is fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d`, Mono, x64, Unity `2022.3.62f2`, and the official archive digest `82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4`. TFM selection is performed from bounded local metadata and is inconclusive when it cannot distinguish netstandard 2.0 from 2.1.

The private package contains exactly three managed files: the synthetic plugin, the evidence-selected `ThroneForge.API`, and the evidence-selected `ThroneForge.Contracts`. A versioned canonical manifest records relative path, size, SHA-256, assembly identity, and selected TFM. Its digest becomes `CodeModDescriptor.PackageSha256`; integrity, approval, game fingerprint, and adapter evidence are evaluated immediately before deployment.

## Execution and rollback

The harness calls the existing loader-smoke copy, archive, transaction, launch, manifest, readiness, and rollback services. It refuses an existing full-mode disposable copy, never follows reparse points, and never writes under the original game root. It deploys only below `clean-game/BepInEx/plugins/dev.throneforge.m1.synthetic-smoke/` after the loader transaction is verified and the process is closed.

The bounded plugin launch receives only a cryptographically random nonce through `THRONEFORGE_SMOKE_NONCE`. The marker parser requires exactly one GUID/version/nonce/API/Contracts marker, one discovered plugin, BepInEx 5.4.23.5, preloader and chainloader evidence, no fatal/error evidence, and no lifecycle marker. A manual-closure state persists a sanitized external recovery marker and refuses file mutation until explicit rollback.

## Trust boundary

This is a full-trust plugin experiment, not a sandbox. The synthetic source template contains no game assembly, Harmony, network, process, file, config, reflection, or game-lifecycle code. `IThroneForgeMod.InitializeAsync` and `ShutdownAsync` throw a sanitized synthetic marker if ever called; the smoke test does not call them. No result generalizes to a real Thronefall plugin.
