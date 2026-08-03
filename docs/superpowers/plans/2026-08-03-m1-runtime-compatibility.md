# M1 Runtime Compatibility Investigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing local-only discovery tool with bounded, metadata-only managed-runtime and loader-compatibility evidence for the documented game fingerprint, without installing or executing a loader.

**Architecture:** Reuse `DiscoveryPathValidator`, the existing reparse-safe relative-path rules, bounded stream reads, and atomic report writer. Add focused runtime-compatibility models and inspectors: PE metadata for managed assemblies, bounded Unity-version evidence, a fixed-name loader-indicator inventory, conservative target-framework classification, and a deterministic sanitized report. The CLI gains a `runtime-compatibility` command while the existing `inspect` command and fingerprint v1 remain unchanged.

**Tech Stack:** C# on the pinned `net10.0` external-tool target; `System.Reflection.Metadata`/`System.Reflection.PortableExecutable`; `FileVersionInfo`; xUnit synthetic fixtures; no BepInEx, Harmony, Unity, game assemblies, or third-party runtime packages.

## Global Constraints

- Inspect only an explicitly supplied absolute game path; never scan the computer or Steam library.
- Never modify the game installation, launch the game, follow reparse points, load assemblies, decompile code, inspect methods, or publish private game types.
- Keep the base fingerprint `1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d` and algorithm `throneforge-game-fingerprint-v1` unchanged.
- Report only relative paths, bounded compatibility metadata, official release identifiers, and sanitized conclusions.
- Keep all game-facing target framework and loader decisions provisional until a later clean-profile smoke test.
- Preserve the explicit architecture/dependency allowlist and add no forbidden project dependency.

---

### Task 1: Establish the Task-2 plan and red test surface

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Create: `docs/superpowers/plans/2026-08-03-m1-runtime-compatibility.md`
- Create: `tests/ThroneForge.Discovery.Tests/RuntimeCompatibilityEngineTests.cs`
- Create: `tests/ThroneForge.Discovery.Tests/RuntimeCompatibilityTestFixture.cs`

**Interfaces:**
- Tests consume the planned `RuntimeCompatibilityEngine.Inspect(RuntimeCompatibilityRequest)` API.
- The request carries `GamePath`, `BaseFingerprint`, `OutputRoot`, `OverwriteExisting`, and a fixed UTC timestamp for deterministic tests.
- The result exposes managed assembly evidence, framework recommendation, Unity evidence, loader indicators, report path, and report Markdown.

- [x] Update the living plan and status to say M1 Task 2 is in progress after merge commit `e0c46a1`, with no new compatibility conclusion yet.
- [x] Add synthetic tests for the required managed-runtime, Unity, loader-indicator, output-boundary, determinism, sanitization, and metadata-loading prohibitions.
- [ ] Run the focused test project and record the expected compile/test failure caused by the not-yet-created runtime-compatibility API.

### Task 2: Implement bounded managed-assembly metadata inspection

**Files:**
- Create: `src/ThroneForge.Discovery/ManagedAssemblyInspector.cs`
- Modify: `src/ThroneForge.Discovery/DiscoveryModels.cs`
- Modify: `ThroneForge.slnx` only if a project file change is required (no new project is expected)

**Interfaces:**
- `ManagedAssemblyInspector.TryInspect(string fullPath, string relativePath, out ManagedAssemblyEvidence evidence)`.
- `ManagedAssemblyEvidence` contains only relative path, managed-metadata presence, assembly name/version, target-framework attribute, and selected framework references.

- [ ] Read each candidate file through a bounded stream and `PEReader`; return a sanitized failure record for malformed or oversized candidates.
- [ ] Decode only the assembly identity, `TargetFrameworkAttribute`, and selected framework references (`mscorlib`, `netstandard`, `System`, `System.Core`, `System.Runtime`).
- [ ] Never call `Assembly.Load`, `Assembly.LoadFrom`, reflection loading, decompilation, or method-body APIs.
- [ ] Classify netstandard/`mscorlib` evidence conservatively into the specified recommendation values and detect contradictory evidence.

### Task 3: Add bounded Unity-version and loader-indicator evidence

**Files:**
- Create: `src/ThroneForge.Discovery/UnityVersionEvidenceReader.cs`
- Create: `src/ThroneForge.Discovery/LoaderIndicatorInspector.cs`
- Modify: `src/ThroneForge.Discovery/DiscoveryPathValidator.cs` only to expose shared safe-relative candidate checks if required
- Modify: `src/ThroneForge.Discovery/DiscoveryModels.cs`

**Interfaces:**
- `UnityVersionEvidenceReader.Read(...)` returns every source observation and an aggregate `Unknown`, single-value, or `Conflicting` result.
- `LoaderIndicatorInspector.Inspect(...)` returns one status per fixed indicator name without arbitrary directory listing.

- [ ] Read `UnityVersion.txt` and the beginning of `globalgamemanagers` under strict byte limits.
- [ ] Read only PE version-resource metadata from the selected executable and `UnityPlayer.dll` using framework APIs.
- [ ] Reject or omit reparse-point candidates and keep all reads inside the supplied root.
- [ ] Classify loader indicators as `Absent`, `Present`, `Ambiguous`, or `Potential conflict`; never infer loader identity from a filename alone.

### Task 4: Compose the runtime-compatibility command and deterministic report

**Files:**
- Create: `src/ThroneForge.Discovery/RuntimeCompatibilityEngine.cs`
- Modify: `src/ThroneForge.Discovery/DiscoveryReportWriter.cs`
- Modify: `src/ThroneForge.Discovery/Program.cs`
- Modify: `src/ThroneForge.Discovery/DiscoveryModels.cs`

**Interfaces:**
- CLI syntax: `runtime-compatibility --game-path <absolute-path> --fingerprint <sha256> --output-root <path> [--overwrite]`.
- Report path: `<output-root>/<fingerprint>-runtime-compatibility.md`.

- [ ] Reuse game/output validation and atomic writer behavior from Task 1 discovery.
- [ ] Inspect only the documented candidate files that exist, using normalized relative paths.
- [ ] Add the official BepInEx 5 stable/LTS and BepInEx 6 pre-release matrix from verified official source metadata; keep the recommendation provisional.
- [ ] Ensure report output never contains the supplied absolute path, username, machine name, arbitrary listings, binary contents, private game types, or full reference graphs.
- [ ] Make repeated runs deterministic for a fixed timestamp and unchanged synthetic fixture; preserve collision protection.

### Task 5: Documentation and bounded private report

**Files:**
- Modify: `docs/discovery/README.md`
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/adr/ADR-0002-target-framework-split-pending-m1-discovery.md` only after local evidence supports a provisional recommendation
- Create: `docs/discovery/<fingerprint>-runtime-compatibility.md` only after synthetic tests pass and manual sanitization succeeds

- [ ] Document invocation, read/write boundaries, metadata-only behavior, classification rules, fingerprint binding, candidate matrix, limitations, and the later smoke-test boundary.
- [ ] Run the private command only after synthetic tests pass, with the explicit local installation path kept out of tracked content and the final response redacted.
- [ ] Manually inspect the report for absolute paths, usernames, machine data, arbitrary listings, copied content, and unsupported claims before staging it.
- [ ] Keep M1 incomplete and make M1 Task 3 the next task: reversible clean-profile loader smoke test.

### Task 6: Validation and handoff

**Files:**
- Modify: `STATUS.md` with exact local and hosted evidence after validation

- [ ] Run `dotnet --version`, locked restore, format verification, Release build, full tests, architecture tests, contracts tests, and hygiene scans.
- [ ] Inspect hosted Windows and Ubuntu test-result artifacts and confirm complete TRX coverage.
- [ ] Confirm no loader/proprietary binaries, copied game directories, absolute private paths, or forbidden dependencies are tracked.
- [ ] Do not install or execute BepInEx and do not begin M1 Task 3.
