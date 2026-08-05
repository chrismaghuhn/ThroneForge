# M1 Task 7 execution plan

1. Create the branch from merged `main` and record the Task-7 scope in the repository plan and status.
2. Add red repository-only tests for Unity lifecycle metadata, context/capabilities, lifecycle state transitions, marker validation, bounded log stability, and package evidence.
3. Implement metadata-only lifecycle validation, the synchronous exactly-once lifecycle host, structured marker/log verification, and the source-only lifecycle plugin template.
4. Extend the existing Task-6 CLI/harness integration without weakening package, admission, ownership, transaction, rollback, or sanitization gates.
5. Run local format/build/test/hygiene validation, push the implementation, and wait for exact-SDK Windows/Linux CI.
6. Only after green hosted synthetic CI, run one fresh private disposable-profile experiment, manually inspect the sanitized report, and commit no private raw evidence.
7. Run final hosted validation on the report head and document remaining uncertainty; do not begin Task 8.

## Task-7 correction pass

1. Persist a sanitized, versioned lifecycle experiment stage and stable failure category before and after each loader/package/lifecycle operation.
2. Share one source-only lifecycle host between repository tests and the generated private plugin, including correct synchronous `ValueTask` handling.
3. Parse bounded BepInEx log envelopes strictly, validate marker encounter order, and require exactly one recognized log file.
4. Make Unity metadata inspection require the exact `UnityEngine.CoreModule` assembly and one public top-level `UnityEngine.Application` contract.
5. Track plugin removal, loader-only restoration, loader rollback, disposable restoration, and original post-checks independently.
6. Run local and hosted synthetic validation, then perform the single permitted fresh corrective private experiment and record its sanitized result without claiming unsupported game internals.

## Repository-only orchestration correction

The current correction does not permit another private run. It replaces human CLI parsing with `throneforge-runtime-compatibility-evidence-v1`, verifies Task-3 loader state through shared C# services and the saved disposable baseline, represents package admission and deployment as one `AdmitAndDeploy` stage with internal phase categories, and moves stage progression/result modeling into `LifecycleExperimentOrchestrator`. `LogStability`, `PluginRemoval`, `LoaderRollback`, `DisposablePostcheck` and `OriginalPostcheck` own their respective evidence. A separate private attempt may be considered only after this correction receives a fresh hosted green run and code review.

Validation of the correction passed on head `dbbcc3a` in hosted run `31012843103`: Windows and Ubuntu each used SDK `10.0.100`, uploaded 13 TRX files and reported 327 tests with zero failures, errors, skips or not-executed tests. The private experiment was not rerun.

## Correction outcome

Correction head `bdd871fa263d5b97fc4a20160ee110d32f406c99` passed hosted run `31008620029` with 13 TRX files and 313 tests per runner on SDK `10.0.100`. The single permitted corrective private run stopped at `OriginalPreflight` with stable category `original-preflight-failed`: the discovery CLI emitted `Selected executable: ...`, while the harness expected an equals-delimited value. No loader transaction, package admission, deployment, or lifecycle evidence was produced. No further private run is permitted for this correction.

## Final orchestration ownership correction

The current repository-only slice makes `LifecycleExperimentOrchestrator` the single stage owner. `ILifecycleExperimentOperations` returns typed evidence for every external boundary; the result cannot be `Passed` when required evidence is missing. The versioned stage record preserves the first primary failed stage/category and stores cleanup failures separately. Cleanup and both postchecks execute in the orchestrator, while `LifecycleExperimentProductionOperations` uses the existing Task-3/Task-6 services for runtime evidence, loader state, package capture/admission/deployment, removal, rollback, and profile verification.

The PowerShell entry point only validates explicit inputs, locates `dotnet`, invokes `run-lifecycle-experiment`, and forwards its sanitized output/exit code. `LifecycleExperimentReportWriter` derives the fingerprint-specific repository report path and renders only `LifecycleExperimentResult`. No private experiment is authorized in this slice. Final repository-only validation passed on head `1207546c1f7885eb4cb43fa70fa70c62051a7145` in hosted run `31016602789`: Windows and Ubuntu each uploaded 13 TRX files representing 333 tests with zero failure, error, skip, not-executed or aborted results under SDK `10.0.100`.

## Production-path hardening correction

The current correction keeps the private lifecycle experiment disabled. The production adapter now creates the versioned Task-6 ownership state before any disposable preparation, builds the source-only lifecycle package inside the owned experiment root, preserves loader/plugin/process side-effect evidence even when a stage fails, and exposes typed recovery plus an explicit C# rollback operation. The loader-only manifest is captured after loader launch and verification, immediately before deployment. Package, manifest, and Unity metadata paths are validated against the owned experiment and fingerprint-bound profile; expected API/Contracts identities come from captured package metadata. Cleanup and original/disposable postchecks remain C#-owned and independent.

The current cleanup correction keeps the reusable workflow hardened. One shared loader-only profile comparator is used by normal plugin removal and manual recovery; it permits only changed content in the same recognized BepInEx log path and retains exact complete-baseline verification after loader rollback. The PowerShell wrapper now offers `-Mode Rollback` for the supported C# rollback operation, derives the repository HEAD commit, and passes the explicitly supplied executable-relative path so C# can bind it to discovered evidence. Implementation head `2139d41812b5da09c620a1685a217de1caa3510e` passed hosted run `31025440280`; final sanitized report head `e0bfd3958d4f2add190877b436737f7a0946b6e9` passed final hosted run `31028231111` on both runners with 13 TRX files and 350 tests per runner under SDK `10.0.100`. The one authorized final private run failed once at `LoaderLaunch` with `loader-launch-failed` before package admission or lifecycle evidence; no retry is authorized.

The repository-only integration coverage verifies ownership creation, safe refusal for an unowned/non-empty root, report redaction, primary-failure persistence, manual-closure cleanup suppression, exact path boundaries, and the real CLI-to-production-orchestrator wiring. No private game, BepInEx, or lifecycle run is authorized in this correction; the previous two private failures remain historical evidence.
