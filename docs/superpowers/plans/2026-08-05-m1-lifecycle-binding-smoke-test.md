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
