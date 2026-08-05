# ADR-0006: Full-trust code-mod admission boundary

- Status: Accepted for M1 Task 4; M1 Task 5 final native-image/load-context correction complete
- Date: 2026-08-04

## Context

ThroneForge distinguishes data-only content from executable code mods. A code mod will eventually run in the game process and cannot be securely sandboxed by this SDK. The loader smoke test proved only that the selected BepInEx bootstrap initialized for one fingerprint; it did not prove that a ThroneForge plugin can load, that its target framework is compatible, or that any game-facing API works.

The runtime therefore needs a small, portable decision boundary before a future loader is allowed to activate code. The boundary must be usable by tests and future runtime code without importing BepInEx, Unity, Harmony, private game assemblies, or reflection names into stable projects. A repository-only synthetic probe can test the boundary and collectible .NET loading without claiming that the game loader or a real plugin works.

## Decision

The Task-7 lifecycle slice binds BepInEx `Awake` and public `UnityEngine.Application.quitting` to one synthetic mod instance only. It does not expand the admission boundary into a game API, Harmony, catalog, or custom-wave boundary. The result remains fingerprint-specific and provisional until the private evidence and post-checks are recorded.

Task 4 defines three separate concerns:

1. `ThroneForge.Contracts` carries immutable code-mod identity, package-integrity, approval, compatibility, activation-request, and admission-binding data. Mod IDs and versions are canonicalized with bounded portable rules. Integrity, approval, and compatibility evidence are bound to the same exact mod identity, package SHA-256, and game fingerprint; records never contain installation paths or executable objects.
2. `ThroneForge.API` exposes the portable `IThroneForgeMod` lifecycle contract and a minimal capability context. It exposes no game objects and does not define unverified lifecycle events.
3. `ThroneForge.Runtime` evaluates a `CodeModActivationRequest` through a deterministic, fail-closed admission gate. The gate validates every cross-record binding, requires verified package integrity, fingerprint-matched supported adapter evidence, and exact-package-and-game-build approval before returning `Approved`. Every sufficiently evidenced decision carries a canonical artifact binding and deterministic versioned UTF-8/SHA-256 digest. The gate never loads, reflects over, invokes, or unloads an assembly.

`Approved` means only that a later loader may continue to its own loading and compatibility checks. It is not a claim that a plugin can load in Thronefall, that a target framework has been selected, that Harmony/HarmonyX is compatible, or that lifecycle/game APIs are available.

## Consequences

- The trust decision is explicit, artifact-bound, fingerprint-bound, and testable before any future code-mod activation.
- The stable API and runtime remain free of game-specific dependencies and can be tested without a game installation.
- The records are trusted runtime inputs rather than cryptographic signatures, and package integrity/approval are not an OS-level security sandbox.
- A future loader must compare the decision binding immediately before loading, preserve the package hash and approval decision, and add separate evidence for assembly loading and game compatibility.
- M1 Task 5 may load only a source-controlled synthetic primary fixture into a collectible test context. Metadata-only preflight requires a CLR header with `CorFlags.ILOnly`, rejects native entry points, arbitrary sidecars, native imports, and module initializers, and requires exactly one public top-level closed implementation. After `LoadFromStream`, the probe verifies that the assembly belongs to the expected collectible context. The admission gate is re-run immediately before loading the captured bytes. No plugin constructor or ThroneForge lifecycle method is explicitly invoked; assembly loading remains full-trust. The probe must not access a game installation.
- The Task-5 primary-file hash does not establish multi-file package-closure integrity. A future multi-file package needs a separate manifest and approval design.
- Plugin target framework remains unverified at the binary level; assembly loading, Harmony compatibility, lifecycle bindings, and game APIs remain unverified until a later bounded task and clean-profile evidence exist.

## Task-6 disposable experiment

The private synthetic-plugin experiment reuses this admission boundary immediately before deployment. Its package digest is a versioned canonical digest of exactly three metadata-inspected managed files; approval, integrity, adapter evidence, and the fixed game fingerprint are bound to that digest. This is evidence binding, not a signature or sandbox. Task 6 passed for the documented fingerprint in an external disposable profile, loaded exactly one source-generated synthetic plugin, and invoked no ThroneForge lifecycle method. It did not load a Thronefall plugin or establish game compatibility.
