# M1 Task 5 Hardening Plan

## Objective

Harden the repository-only synthetic plugin-load probe without running a private Thronefall or BepInEx experiment. The probe must bind the complete single-assembly load closure, reject module initializers before loading, accept exactly one public closed `IThroneForgeMod` implementation, and require bounded collectible-context unload observation.

## Implementation slices

- [x] Add source-only fixtures and failing tests for helper/native dependencies, module initializers, invalid contract shapes, exact-byte capture, and unload retention.
- [x] Add metadata-only PE/metadata preflight for assembly identity, assembly references, module initializers, native imports, trusted platform references, and the shared API/Contracts identities.
- [x] Replace resolver-based loading with a strict collectible context that shares only the exact API/Contracts identities, defers trusted platform references, and rejects all other managed or unmanaged dependencies.
- [x] Strengthen contract inspection to require one public, top-level, non-abstract, closed class implementing the shared `IThroneForgeMod` contract.
- [x] Refactor loading around one bounded byte capture and report closure evidence, exact-byte admission, sanitized outcomes, and bounded unload status.
- [x] Update all focused tests, architecture boundaries, design/ADR documentation, and current-head validation evidence.

## Acceptance gates

- [x] Focused PluginLoadTest tests pass with no compiled fixture artifacts tracked.
- [x] Architecture tests continue to pass and no forbidden game/loader dependency is introduced.
- [ ] Full restore, format, Release build, and test validation pass locally where the pinned SDK is available; hosted CI verifies exact SDK `10.0.100` on Windows and Ubuntu.
- [x] No private Thronefall/BepInEx experiment is run and no game, loader, archive, raw log, or private path is committed.

## Explicit limits

This task validates only one synthetic managed assembly plus shared API/Contracts and trusted platform assemblies. It does not design multi-file package integrity, load a Thronefall plugin, select a plugin TFM, claim BepInEx compatibility, or invoke plugin lifecycle methods.
