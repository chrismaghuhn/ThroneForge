# M1 Task 4 Plugin/Runtime Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish and test the portable boundary between explicitly trusted code mods and the ThroneForge runtime without loading assemblies or claiming game-runtime compatibility.

**Architecture:** `ThroneForge.API` will expose only the portable mod lifecycle and capability contracts required at this stage. `ThroneForge.Contracts` will carry the small immutable identity, package-integrity, and approval records shared by the API and runtime. `ThroneForge.Runtime` will implement an admission gate that decides whether a code mod may proceed to a future loader, but it will not load code, resolve assemblies, invoke a plugin, or reference the game adapter.

**Tech Stack:** C# on pinned .NET 10 external tooling, nullable reference types, xUnit, existing architecture tests, no new production package dependencies.

---

## Scope guard

- The task is limited to the plugin/runtime boundary design and executable admission decision.
- No BepInEx, Harmony/HarmonyX, Unity, Assembly-CSharp, GameAssembly, or copied game reference may be added.
- No assembly loading, reflection-based plugin discovery, lifecycle event binding, catalog export, custom-wave code, or game launch is part of this task.
- The gate is deliberately not a loader. `Approved` means only that a later loader may continue after the caller has satisfied this gate's explicit conditions.

### Task 1: Record the boundary decision and mark M1 Task 4 started

**Files:**
- Create: `docs/adr/ADR-0006-full-trust-code-mod-boundary.md`
- Create: `docs/superpowers/plans/2026-08-04-m1-plugin-runtime-boundary.md`
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [x] Update the living milestone state to `M1 Task 4 in progress` from `main@06554d845a9fe46132c1a19ec0c2f18b8722acf2` on `agent/m1-plugin-runtime-boundary`.
- [x] Record that the only accepted behavior is portable contract definition and pre-load admission; plugin TFM compatibility, assembly loading, Harmony compatibility, lifecycle bindings, and game APIs remain unverified.
- [x] Record the explicit trust rule: a code mod requires integrity verification and an explicit user approval before any future loader is allowed to activate it; the gate does not claim OS-level sandboxing.

### Task 2: Write the failing portable contract tests

**Files:**
- Modify: `tests/ThroneForge.Contracts.Tests/ProjectSkeletonTests.cs`
- Create: `tests/ThroneForge.Contracts.Tests/CodeModBoundaryContractTests.cs`
- Modify: `tests/ThroneForge.Runtime.Tests/ThroneForge.Runtime.Tests.csproj`
- Create: `tests/ThroneForge.Runtime.Tests/CodeModAdmissionGateTests.cs`

- [x] Add tests showing that a code-mod descriptor preserves the normalized package hash and exposes no filesystem path.
- [x] Add tests for gate outcomes: unverified integrity is rejected; unsupported/unknown adapter compatibility is rejected; missing approval returns `RequiresExplicitApproval`; verified integrity plus explicit approval returns `Approved`.
- [x] Add tests proving the decision contains a stable reason code and does not load or execute any assembly.
- [x] Run the focused contract/runtime tests and observe the expected compile/test failures before adding production implementation.

### Task 3: Implement the immutable boundary records and public mod lifecycle contract

**Files:**
- Create: `src/ThroneForge.Contracts/CodeModBoundaryContracts.cs`
- Create: `src/ThroneForge.API/CodeModApi.cs`

- [x] Implement `ModIdentity`, `CodeModDescriptor`, and `CodeModActivationRequest` as immutable records with invariant, lowercase SHA-256 normalization and no absolute-path fields.
- [x] Implement `IThroneForgeMod`, `IModContext`, and `ICapabilityService` using only portable contracts and `CancellationToken`/`ValueTask`.
- [x] Keep the API free of Unity, BepInEx, Harmony, adapter, loader, reflection, and game-internal names.
- [x] Re-run the focused tests and the contract/API build.

### Task 4: Implement the runtime admission gate without a loader

**Files:**
- Create: `src/ThroneForge.Runtime/CodeModAdmissionGate.cs`
- Modify: `src/ThroneForge.Runtime/ThroneForge.Runtime.csproj` only if the existing contract reference is insufficient (no new package)

- [x] Implement `CodeModAdmissionStatus`, `CodeModAdmissionDecision`, and `CodeModAdmissionGate.Evaluate` with deterministic precedence: malformed request, unverified integrity, unsupported compatibility, missing approval, then approved.
- [x] Use existing `AdapterCompatibility` values only; do not add a game-specific compatibility assumption.
- [x] Return stable portable reason codes and remediation text without paths, stack traces, or secrets.
- [x] Do not create an assembly load context, call `Assembly.Load*`, use reflection, launch a process, or invoke a mod.

### Task 5: Harden architecture tests and document the execution boundary

**Files:**
- Modify: `tests/ThroneForge.ArchitectureTests/ArchitectureBoundaryTests.cs`
- Modify: `tests/ThroneForge.ArchitectureTests/DependencyDeclarationScanner.cs` only if a new boundary assertion needs a shared helper
- Modify: `docs/adr/ADR-0005-data-only-and-full-trust-code-mods.md`
- Modify: `docs/adr/ADR-0002-target-framework-split-pending-m1-discovery.md` only to preserve the explicit unverified TFM statement

- [x] Add assertions that the API and runtime assemblies remain portable and that the runtime admission gate has no concrete adapter or loader dependency.
- [x] Add a regression test that the gate's source/project declarations contain no forbidden game/runtime dependency tokens.
- [x] Update the trust-model ADR with the implemented admission boundary while keeping actual code loading out of scope.

### Task 6: Validate and close this bounded Task 4 slice

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] Run `dotnet --version`, locked restore, format verification, Release build, full tests, architecture tests, and contracts tests.
- [ ] Confirm no tracked game/loader binaries, private paths, downloaded archives, raw logs, or new forbidden dependency declarations exist.
- [ ] Record exact local results and leave M1 incomplete with the next task limited to the next evidence-backed runtime boundary investigation; do not claim a functioning plugin.

## Task-4 hardening: bind trust evidence to the exact artifact and game build

This follow-up starts from reviewed head `18f7b1135b6aaba04290198f355e6e9ac6a97b5d` on `agent/m1-plugin-runtime-boundary-hardening`. It closes the review findings in the portable boundary without loading a plugin or adding loader/game dependencies.

### Hardening steps

- [ ] Rewrite contract tests first for canonical mod identity/version rules, structured integrity evidence, structured approval, fingerprint-bound adapter evidence, decision bindings, and deterministic binding digests.
- [ ] Implement a shared SHA-256 value type and canonical encoding rules; keep records immutable and free of paths, streams, assemblies, executable objects, and personal data.
- [ ] Redesign `CodeModActivationRequest` to carry descriptor, game fingerprint, integrity evidence, optional approval, and compatibility evidence instead of independent booleans/enums.
- [ ] Implement deterministic gate precedence: malformed request; descriptor/hash mismatch; missing or failed integrity; missing/unsupported compatibility; fingerprint mismatch; denied/mismatched approval; missing approval; approved.
- [ ] Include the exact artifact/game/adapter binding and digest in decisions wherever sufficient evidence exists; centralize stable reason codes.
- [ ] Update ADR-0005, ADR-0006, `PLAN.md`, `STATUS.md`, `README.md`, and `CHANGELOG.md` to state that the records are trusted runtime inputs, not signatures or an OS sandbox, and that binary plugin-TFM compatibility remains unverified.
- [ ] Run the repository validation, tracked-file hygiene checks, hosted Windows/Linux matrix, and TRX artifact inspection. Do not begin plugin loading or lifecycle integration.

### Hardening acceptance

- [ ] Canonically equivalent IDs compare equal and unsafe identities/versions fail construction.
- [ ] Integrity, approval, compatibility, request, and decision records reject or fail closed on every cross-record mismatch.
- [ ] Binding digest changes when identity, package hash, game fingerprint, adapter ID, or adapter version changes and remains deterministic otherwise.
- [ ] Unknown compatibility values and warnings never reach `Approved`; missing approval returns `RequiresExplicitApproval`, while denied or stale approval returns `Rejected`.
- [ ] Hosted exact-SDK validation passes with no forbidden dependencies, no plugin load, and no proprietary or private-path artifacts.

## Self-review against the specification

- Section 5.6 is preserved: the repository will not claim to sandbox arbitrary .NET code.
- Section 8.3 is advanced only by portable interfaces; no private game object crosses the boundary.
- Section 9.3 is enforced at the admission point by requiring integrity verification and explicit approval.
- Section 16.1 is represented by the lifecycle contract, but no event or game payload is invented.
- Section 18.1 is not implemented: the gate is not a startup phase or loader.
- Section 23.4 is partially prepared: approval and integrity precede future loading, while actual assembly isolation/loading remains a later task.
- Section 25 M1 remains incomplete because no lifecycle hook, catalog source, or custom-wave behavior is claimed.
