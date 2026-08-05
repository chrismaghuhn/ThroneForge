# M1 Task 5 Hardening Plan

## Objective

Harden the repository-only synthetic plugin-load probe without running a private Thronefall or BepInEx experiment. The probe must bind the complete single-assembly load closure, require a pure managed IL-only image, reject module initializers before loading, accept exactly one public closed `IThroneForgeMod` implementation, verify the actual collectible load context, and require bounded collectible-context unload observation.

## Implementation slices

- [x] Add source-only fixtures and failing tests for helper/native dependencies, module initializers, invalid contract shapes, exact-byte capture, and unload retention.
- [x] Add metadata-only PE/metadata preflight for assembly identity, assembly references, module initializers, native imports, trusted platform references, and the shared API/Contracts identities.
- [x] Replace resolver-based loading with a strict collectible context that shares only the exact API/Contracts identities, defers trusted platform references, and rejects all other managed or unmanaged dependencies.
- [x] Strengthen contract inspection to require one public, top-level, non-abstract, closed class implementing the shared `IThroneForgeMod` contract.
- [x] Refactor loading around one bounded byte capture and report closure evidence, exact-byte admission, sanitized outcomes, and bounded unload status.
- [x] Update all focused tests, architecture boundaries, design/ADR documentation, and current-head validation evidence.
- [x] Add temporary PE mutation tests for missing `ILOnly`, present `NativeEntryPoint`, and missing CLR headers.
- [x] Record CLR-header, IL-only, native-entry-point, P/Invoke, and actual-context evidence without paths or runtime object identities.
- [x] Verify `AssemblyLoadContext.GetLoadContext(assembly)` is the expected collectible context and fail closed for simulated mismatches.

## Acceptance gates

- [x] Focused PluginLoadTest tests pass with no compiled fixture artifacts tracked.
- [x] Architecture tests continue to pass and no forbidden game/loader dependency is introduced.
- [x] Full restore, format, Release build, and test validation pass in hosted CI with exact SDK `10.0.100` on Windows and Ubuntu; local compile/test validation passes with SDK `10.0.110`, while exact local formatting remains unavailable because `10.0.100` is not installed.
- [x] No private Thronefall/BepInEx experiment is run and no game, loader, archive, raw log, or private path is committed.

## Explicit limits

This task validates only one synthetic managed assembly plus shared API/Contracts and trusted platform assemblies. It does not design multi-file package integrity, load a Thronefall plugin, select a plugin TFM, claim BepInEx compatibility, or invoke plugin lifecycle methods.

## Previous validation before the final native-image correction

- Head: `bb32de1dd211ddd5174f8d8f6e6490164da970db`
- Hosted run: [30895225156](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30895225156)
- Windows and Ubuntu: SDK `10.0.100`, 12 TRX files, 239 tests, 0 failures/errors/skips each.
- The artifacts were independently parsed and no `Overwriting results file` warning occurred.

## Final correction validation

- Pre-fix head: `1649336681fd76cb1f66623d151179015c812fcb`
- Pre-fix hosted run: [30895740658](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30895740658)
- Final head: `a8595e7b56b80ab338e5d148c1ba1f13455598d4`
- Hosted run: [30978672030](https://github.com/chrismaghuhn/ThroneForge/actions/runs/30978672030)
- Windows and Ubuntu: SDK `10.0.100`, 12 TRX files, 244 tests, 0 failures/errors/skips each.
- The artifacts were independently parsed and no `Overwriting results file` warning occurred.
