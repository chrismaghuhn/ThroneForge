# M1 Plugin Load Smoke-Test Design

## Goal

Prove the smallest safe code-mod loading path after Task 4: a synthetic plugin assembly can be admitted for one exact artifact/game binding and loaded into a collectible .NET `AssemblyLoadContext`, without claiming BepInEx, Unity, Harmony, Thronefall lifecycle, or game-API compatibility.

## Scope and non-goals

This slice is repository-only and uses a tracked source fixture, never a copied game or loader binary. It does not inspect a game installation, install BepInEx, launch Thronefall, invoke plugin lifecycle methods, inspect private game types, resolve game dependencies, or choose a final plugin TFM. The existing Task 3 private evidence remains separate and is not rerun.

The probe loads only a synthetic assembly that implements the existing portable `IThroneForgeMod` contract. It verifies the exact file hash and re-runs `CodeModAdmissionGate` immediately before loading. An admission failure must result in no assembly load.

## Architecture

`ThroneForge.PluginLoadTest` is an external-tool project. It references only the portable API, contracts, and runtime admission gate; it has no Unity, BepInEx, Harmony, game-assembly, adapter, discovery, or loader dependency. `PluginLoadProbe` owns the sequence: bounded artifact hashing, bound evidence construction, admission evaluation, metadata-only identity check after load, and collectible-context disposal. The probe does not invoke the loaded plugin.

`ThroneForge.PluginLoadTest.Tests` uses a separate synthetic fixture project implementing `IThroneForgeMod`. Tests exercise the real probe against its built assembly and temporary copies. The fixture is test input, not a distributed plugin package and not a claim about the eventual `.tforge` format.

## Trust and data flow

1. The caller supplies a canonical descriptor, exact game fingerprint, adapter evidence, and approval record.
2. The probe reads the exact assembly artifact once with a bounded stream, hashes those exact bytes, and requires the descriptor hash to match.
3. The probe creates verified integrity evidence for that same identity/hash.
4. Immediately before `AssemblyLoadContext.LoadFromStream` of those already-hashed bytes, it evaluates the existing admission gate.
5. Only `Approved` proceeds. The returned binding digest is retained in the result.
6. The collectible context loads the exact artifact, verifies that exactly one public type implements `IThroneForgeMod`, records only assembly/type identity, and is unloaded. No constructor or lifecycle method is called.

## Failure behavior

Missing/invalid paths, hash mismatch, missing or stale approval, unsupported compatibility, and binding mismatches fail closed before any load. Load failures return a sanitized failure category without raw paths or stack traces in normal result data. The service never mutates the artifact, repository, game installation, or loader profile.

## Testing and limits

Tests cover successful bound loading, missing approval, changed artifact, changed game fingerprint, adapter mismatch, unsupported compatibility, malformed paths, missing plugin contract, duplicate plugin contract types, unloadability, deterministic binding preservation, and architecture boundaries. The tests do not prove a real game load, BepInEx plugin discovery, plugin target-framework compatibility, Harmony compatibility, or lifecycle behavior.

## Next experiment boundary

Only after this slice is merged and reviewed may a separately approved private experiment build the synthetic plugin for a disposable BepInEx profile. That experiment must use an explicit local game path, recompute the fingerprint/readiness, place no plugin in the original installation, and report loader/plugin initialization separately from this repository-only probe.
