# M1 Task 7: public Unity lifecycle binding and smoke-test design

## Scope

Task 7 verifies one public Unity lifecycle source for the documented fingerprint:

`BepInEx Awake -> initialize one synthetic mod -> UnityEngine.Application.quitting -> shutdown the same mod`.

The binding is identified as `unity-application-quitting-v1`. No Thronefall-defined type, private member, gameplay state, Harmony hook, catalog, save data, or custom wave is inspected.

## Trust and safety

The implementation reuses Task-6 ownership, loader transaction, package capture, admission, deployment, launch, removal, rollback, and complete original/disposable post-verification services. The Task-7 package contains exactly the lifecycle plugin, `ThroneForge.API.dll`, and `ThroneForge.Contracts.dll`. Hosted CI remains source-only; one private run is permitted only after hosted synthetic CI passes and uses a fresh external disposable profile.

The repository correction is fail-closed: machine-readable runtime evidence is a strict versioned JSON contract; loader state is read through the canonical Task-3/Task-6 services; package capture/admission/deployment is reported as one bounded `AdmitAndDeploy` stage; and the final result carries independent plugin-removal, loader-rollback, disposable-restoration and original-postcheck facts. No private experiment is part of this correction.

## Metadata contract

Before a private run, the exact local `UnityEngine.CoreModule.dll` is inspected with `PEReader` and `MetadataReader`. The validator requires a public static `UnityEngine.Application.quitting` event with handler type `System.Action` and public static add/remove accessors. The Unity assembly is never loaded by the external validator and no Unity binary is committed.

## Lifecycle contract

The experiment-only host uses explicit states `Created`, `Initializing`, `Initialized`, `ShutdownRequested`, `ShutdownCompleted`, and `Faulted`. Initialization and shutdown are synchronous-only and exactly-once. `OnDestroy` is cleanup fallback only and cannot satisfy the pass criteria. Markers are nonce-bound and must occur exactly once in sequence 1, 2, 3 for initialization, `Application.quitting`, and shutdown completion.

## Evidence and limits

The report records package and admission digests, metadata evidence, marker counts/sequence, actual runtime API and Contracts identities, loader errors, removal, rollback, disposable restoration, and original pre/post verification. It describes the result as a public Unity `Application.quitting` binding observed while Thronefall was running, not as a verified Thronefall-internal lifecycle method. Async lifecycle scheduling and arbitrary third-party plugin safety remain unverified.

The correction adds a versioned external stage record that persists only the experiment ID, expected fingerprint, current/last-completed stage, stable result category, and bounded digests/status values. BepInEx log lines are accepted only through the raw-marker or bounded logger-prefix envelope; marker keys are exact and encounter order must be `1,2,3`. The same source-only lifecycle host is linked into repository tests and the generated plugin, so synchronous faults and exactly-once state transitions are tested against the deployed implementation. Removal, loader-only restoration, loader rollback, disposable restoration, and original postchecks are independent evidence fields.

## Orchestration ownership

`LifecycleExperimentOrchestrator` is the single owner of the Task-7 experiment state machine. Its `ILifecycleExperimentOperations` dependency exposes typed evidence for each external boundary rather than arbitrary success flags. The orchestrator persists stage transitions, preserves the first primary failure, runs applicable cleanup and postchecks, and assembles the final result only from measured evidence. `LifecycleExperimentProductionOperations` adapts the existing Task-3 and Task-6 services; it does not reimplement their path, manifest, transaction, admission, or rollback rules.

The public `run-lifecycle-experiment` CLI operation constructs the production operations, orchestrator, and C# report writer. The PowerShell script is only an explicit-input and `dotnet` invocation wrapper: it does not interpret loader state, advance stages, classify package phases, perform cleanup, or construct Markdown. This repository-only correction does not authorize another private run; a private attempt requires a separate review after fresh hosted synthetic validation.
