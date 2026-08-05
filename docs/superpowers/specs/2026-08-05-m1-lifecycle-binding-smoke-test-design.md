# M1 Task 7: public Unity lifecycle binding and smoke-test design

## Scope

Task 7 verifies one public Unity lifecycle source for the documented fingerprint:

`BepInEx Awake -> initialize one synthetic mod -> UnityEngine.Application.quitting -> shutdown the same mod`.

The binding is identified as `unity-application-quitting-v1`. No Thronefall-defined type, private member, gameplay state, Harmony hook, catalog, save data, or custom wave is inspected.

## Trust and safety

The implementation reuses Task-6 ownership, loader transaction, package capture, admission, deployment, launch, removal, rollback, and complete original/disposable post-verification services. The Task-7 package contains exactly the lifecycle plugin, `ThroneForge.API.dll`, and `ThroneForge.Contracts.dll`. Hosted CI remains source-only; one private run is permitted only after hosted synthetic CI passes and uses a fresh external disposable profile.

## Metadata contract

Before a private run, the exact local `UnityEngine.CoreModule.dll` is inspected with `PEReader` and `MetadataReader`. The validator requires a public static `UnityEngine.Application.quitting` event with handler type `System.Action` and public static add/remove accessors. The Unity assembly is never loaded by the external validator and no Unity binary is committed.

## Lifecycle contract

The experiment-only host uses explicit states `Created`, `Initializing`, `Initialized`, `ShutdownRequested`, `ShutdownCompleted`, and `Faulted`. Initialization and shutdown are synchronous-only and exactly-once. `OnDestroy` is cleanup fallback only and cannot satisfy the pass criteria. Markers are nonce-bound and must occur exactly once in sequence 1, 2, 3 for initialization, `Application.quitting`, and shutdown completion.

## Evidence and limits

The report records package and admission digests, metadata evidence, marker counts/sequence, actual runtime API and Contracts identities, loader errors, removal, rollback, disposable restoration, and original pre/post verification. It describes the result as a public Unity `Application.quitting` binding observed while Thronefall was running, not as a verified Thronefall-internal lifecycle method. Async lifecycle scheduling and arbitrary third-party plugin safety remain unverified.
