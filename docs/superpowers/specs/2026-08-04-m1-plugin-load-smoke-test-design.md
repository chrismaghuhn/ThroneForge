# M1 Plugin Load Smoke-Test Design

## Goal

Prove a narrowly bounded full-trust code-mod loading path after Task 4: one synthetic primary assembly can be admitted for one exact artifact/game binding and loaded into a collectible .NET `AssemblyLoadContext`, without claiming BepInEx, Unity, Harmony, Thronefall lifecycle, or game-API compatibility.

## Scope and non-goals

This slice is repository-only and uses a tracked source fixture, never a copied game or loader binary. It does not inspect a game installation, install BepInEx, launch Thronefall, invoke plugin lifecycle methods, inspect private game types, resolve game dependencies, or choose a final plugin TFM. The existing Task 3 private evidence remains separate and is not rerun.

The probe loads only a synthetic primary assembly that implements the existing portable `IThroneForgeMod` contract. It verifies the exact file hash, performs metadata-only closure preflight, and re-runs `CodeModAdmissionGate` immediately before loading. The closure allows only the current default-context API and Contracts identities plus trusted platform assemblies; sidecars and native imports are rejected. Module initializers are rejected before load for this synthetic slice. An admission or preflight failure must result in no assembly load.

## Architecture

`ThroneForge.PluginLoadTest` is an external-tool project. It references only the portable API, contracts, and runtime admission gate; it has no Unity, BepInEx, Harmony, game-assembly, adapter, discovery, or loader dependency. `PluginLoadProbe` owns the sequence: bounded byte capture, metadata-only closure/module/native preflight, bound evidence construction, admission evaluation, strict contract-shape inspection after load, and collectible-context disposal. It does not use `AssemblyDependencyResolver`; the context shares only exact API/Contracts identities, defers trusted platform references, rejects other managed references, and rejects unmanaged resolution.

`ThroneForge.PluginLoadTest.Tests` uses a separate synthetic fixture project implementing `IThroneForgeMod`. Tests exercise the real probe against its built assembly and temporary copies. The fixture is test input, not a distributed plugin package and not a claim about the eventual `.tforge` format.

## Trust and data flow

1. The caller supplies a canonical descriptor, exact game fingerprint, adapter evidence, and approval record.
2. The probe reads the exact assembly artifact once with a bounded stream, hashes those exact bytes, and requires the descriptor hash to match.
3. The probe creates verified integrity evidence for that same identity/hash and records the single-assembly closure evidence.
4. Metadata-only preflight rejects module initializers, native imports, and non-platform/non-shared references before any load.
5. Immediately before `AssemblyLoadContext.LoadFromStream` of those already-captured bytes, it evaluates the existing admission gate.
6. Only `Approved` proceeds. The returned binding digest is retained in the result.
7. The collectible context loads the exact artifact, verifies exactly one public, top-level, non-abstract, closed class implements `IThroneForgeMod`, records only assembly/type identity, requests unload, and requires bounded unload observation. No plugin constructor or ThroneForge lifecycle method is explicitly invoked.

## Failure behavior

Missing/invalid paths, hash mismatch, missing or stale approval, unsupported compatibility, binding mismatches, module initializers, native imports, and unapproved managed references fail closed before any load. Load failures return a sanitized failure category without raw paths or stack traces in normal result data. The service never mutates the artifact, repository, game installation, or loader profile. Assembly loading remains full-trust; the probe is not a sandbox. Its wording is limited to “No plugin constructor or ThroneForge lifecycle method was explicitly invoked.”

## Testing and limits

Tests cover successful bound loading, exact-byte capture, missing approval, changed artifact, changed game fingerprint, adapter mismatch, unsupported/unknown compatibility, malformed paths, shared API/Contracts resolution, helper/native/module-initializer rejection, invalid public contract shapes, missing plugin contract, duplicate plugin contract types, unloadability including retained-reference failure, deterministic binding preservation, sanitization, and architecture boundaries. The tests do not prove a real game load, BepInEx plugin discovery, plugin target-framework compatibility, Harmony compatibility, or lifecycle behavior.

## Explicit integrity limit

Task 5 hashes one primary assembly. It does not claim package-closure integrity for a multi-file plugin package. The strict single-assembly policy rejects arbitrary sidecars and native dependencies. A future multi-file package requires a separate manifest and approval design.

## Next experiment boundary

Only after this slice is merged and reviewed may a separately approved private experiment build the synthetic plugin for a disposable BepInEx profile. That experiment must use an explicit local game path, recompute the fingerprint/readiness, place no plugin in the original installation, and report loader/plugin initialization separately from this repository-only probe.
