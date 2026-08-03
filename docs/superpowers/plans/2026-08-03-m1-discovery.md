# M1 Discovery Task 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local-only, portable discovery tool that classifies a legally obtained Thronefall installation conservatively, fingerprints selected local evidence, and writes a sanitized Markdown report without runtime integration.

**Architecture:** `ThroneForge.Discovery` is an external `net10.0` executable with a small public inspection API and no project references. It walks only the explicit root while skipping reparse points, reads bounded PE/file evidence without loading or executing assemblies, and writes one atomic report under the caller-selected output root. Synthetic tests own all fixtures; no proprietary file is required for CI.

**Tech Stack:** C#, .NET 10, framework APIs (`SHA256`, `PEReader`/`MetadataReader` for architecture tests, `FileStream`, `System.Text.Json` only if needed), xUnit, solution-level architecture tests.

---

### Task 1: Establish the M1 branch and architecture test boundary

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `tests/ThroneForge.ArchitectureTests/ArchitectureBoundaryTests.cs`
- Modify: `ThroneForge.slnx`
- Create: `src/ThroneForge.Discovery/ThroneForge.Discovery.csproj`
- Create: `src/ThroneForge.Discovery/Program.cs`
- Create: `tests/ThroneForge.Discovery.Tests/ThroneForge.Discovery.Tests.csproj`

- [x] Confirm `main` is clean at `37c50febfe0ab32231000c194d7f2f853463148c` and create `agent/m1-discovery`.
- [x] Add `ThroneForge.Discovery` to the source-project allowlist with an empty project-reference allowlist; add its test project to the solution.
- [x] Replace `Assembly.LoadFrom` in the compiled-assembly architecture test with `PEReader` plus `MetadataReader`, reading only assembly-reference names from metadata.
- [x] Keep the future target-framework follow-up comment and do not add a package solely for metadata inspection.

### Task 2: Write the failing discovery tests

**Files:**
- Create: `tests/ThroneForge.Discovery.Tests/DiscoveryTestFixture.cs`
- Create: `tests/ThroneForge.Discovery.Tests/DiscoveryEngineTests.cs`
- Create: `tests/ThroneForge.Discovery.Tests/PeArchitectureReaderTests.cs`

- [x] Generate temporary layouts with only small text files and minimal PE headers containing `MZ`, `PE\0\0`, and a selected COFF machine value.
- [x] Assert Mono requires compatible evidence, IL2CPP requires compatible evidence, conflicting evidence is `Ambiguous`, and insufficient evidence is `Unknown`.
- [x] Assert x86, x64, Arm64, malformed, missing, relative, file, and invalid roots are handled without executing or loading binaries.
- [x] Assert reparse-point directories are not traversed, repeated equivalent fixtures produce the same fingerprint, selected metadata changes the fingerprint, report text excludes absolute paths/usernames, writes are atomic, and an existing report is not overwritten without `--overwrite`.
- [x] Run the focused test project with the available SDK `10.0.110`; exact `10.0.100` validation remains a hosted-CI requirement because the repository pin rejects the locally installed feature band.

### Task 3: Implement bounded evidence collection and PE parsing

**Files:**
- Create: `src/ThroneForge.Discovery/DiscoveryModels.cs`
- Create: `src/ThroneForge.Discovery/PeArchitectureReader.cs`
- Create: `src/ThroneForge.Discovery/DiscoveryEngine.cs`

- [x] Validate a non-empty rooted game path that exists as a directory; reject relative paths, missing paths, file paths, and root reparse points with actionable non-sensitive exceptions.
- [x] Enumerate only beneath the supplied root, skip reparse-point entries, normalize report paths to forward-slash relative paths, and never serialize the root path.
- [x] Detect only documented layout indicators: `_Data/Managed`, `mono`/`MonoBleedingEdge`, `Assembly-CSharp.dll`, `GameAssembly.dll`, `_Data/il2cpp_data`, and `global-metadata.dat`.
- [x] Return `Mono` or `IL2CPP` only with at least two compatible signals; return `Ambiguous` for strong conflicting signals and `Unknown` for insufficient evidence. Preserve every detected signal and missing/conflict explanation in the model.
- [x] Parse PE headers from bounded reads, map `0x014c` to x86, `0x8664` to x64, and `0xAA64` to Arm64, and return `Unknown` for malformed/unsupported files.
- [x] Extract Unity-version evidence only from bounded local version files; otherwise return `Unknown`.

### Task 4: Implement fingerprinting and atomic Markdown reporting

**Files:**
- Modify: `src/ThroneForge.Discovery/DiscoveryEngine.cs`
- Create: `src/ThroneForge.Discovery/DiscoveryReportWriter.cs`
- Modify: `tests/ThroneForge.Discovery.Tests/DiscoveryEngineTests.cs`

- [x] Define fingerprint input version `throneforge-game-fingerprint-v1` and serialize invariant UTF-8 LF lines containing backend, architecture, Unity evidence, and ordinal-sorted selected relative file IDs, sizes, and SHA-256 values.
- [x] Hash only a documented small selected set and reject or report files above the read limit; never hash the installation indiscriminately.
- [x] Render all required report sections: fingerprint, tool/algorithm versions, UTC timestamp, backend, architecture, Unity evidence, evidence, missing/conflict evidence, relative files, size/hash values, conclusions, assumptions, next investigation, and privacy statement.
- [x] Write via a same-directory temporary file followed by atomic move; clean the temporary file on failure.
- [x] Require `overwrite=true` for an existing report path and leave the existing report byte-for-byte unchanged when overwrite is not enabled.

### Task 5: Add the command-line surface and discovery documentation

**Files:**
- Modify: `src/ThroneForge.Discovery/Program.cs`
- Create: `docs/discovery/README.md`
- Modify: `CHANGELOG.md`
- Modify: `STATUS.md`

- [x] Support `inspect`, mandatory `--game-path`, optional `--output-root` defaulting to `docs/discovery`, and `--overwrite`; keep exit codes limited to success, invalid arguments, and I/O/discovery failure.
- [x] Print only relative report information and sanitized diagnostics; never echo the absolute game path.
- [x] Document what the tool reads and never modifies, backend classification rules, PE/Unity evidence limits, fingerprint v1, report collision behavior, privacy guarantees, and the difference between synthetic and private verification.
- [x] Update status with actual local/hosted validation only; the private report records `Mono`, `X64`, and `Unity version: Unknown` as local evidence, and hosted run `30839919115` passed on both runners.

### Task 6: Validate, optionally inspect the explicitly supplied private installation, and hand off

**Files:**
- Modify: `STATUS.md`
- Modify: `docs/adr/ADR-0002-target-framework-split-pending-m1-discovery.md` only if the local evidence justifies a decision
- Create: `docs/discovery/<fingerprint>.md` only after manual sanitization

- [x] Run the required restore, format, Release build, full test, architecture, contracts, and tracked-file hygiene checks; local compile/tests passed with SDK `10.0.110`, while exact pinned-toolchain formatting/build/test evidence passed in hosted CI run `30839919115`.
- [x] Run the discovery command against the explicit local game path after synthetic tests passed, inspect the report, and retain only sanitized Markdown.
- [x] Push `agent/m1-discovery`, verify both hosted matrix jobs and artifacts, and record exact run IDs, SDK, and test results.
- [ ] Stop before BepInEx/Harmony selection, loader smoke tests, lifecycle discovery, catalog export, wave bridge, or custom-wave implementation.
