# M1 Plugin Load Smoke-Test Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a repository-only, fingerprint- and artifact-bound probe that loads a synthetic `IThroneForgeMod` assembly without invoking it or claiming game compatibility.

**Architecture:** Add an external `ThroneForge.PluginLoadTest` project that hashes one explicit assembly artifact, rebuilds verified integrity evidence, re-evaluates the existing admission gate immediately before a collectible `AssemblyLoadContext` load, and returns sanitized evidence. Add a source-only fixture library and focused tests; keep the API, contracts, runtime, adapters, discovery, and loader-smoke projects free of new game-facing dependencies.

**Tech Stack:** .NET `10.0.100` repository SDK, C#, `System.Runtime.Loader`, `System.Security.Cryptography`, xUnit, existing Contracts/API/Runtime projects, no new NuGet package.

---

### Task 1: Record the merged baseline and task boundary

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/adr/ADR-0005-data-only-and-full-trust-code-mods.md`
- Modify: `docs/adr/ADR-0006-full-trust-code-mod-boundary.md`
- Create: `docs/superpowers/specs/2026-08-04-m1-plugin-load-smoke-test-design.md`
- Create: `docs/superpowers/plans/2026-08-04-m1-plugin-load-smoke-test.md`

- [ ] **Step 1: Mark Task 4 complete on merged `main`.**

Replace stale “in progress” wording with `main@5f4b4dd0714d0cffaf9f3267b6f0651ecf6e043e`, record PR #4 merge, hosted run `30878236039`, 11 TRX files and 212 tests per runner, and state that no plugin has been loaded.

- [ ] **Step 2: Define the next task as repository-only.**

Add a new `M1 Task 5` section stating that the probe loads only the synthetic fixture, re-runs the gate immediately before loading, never invokes it, and does not run the private game experiment.

- [ ] **Step 3: Review the docs for contradictions.**

Run:

```powershell
rg -n -i "in progress|hardening branch|Task 4 remains|plugin load|plugin is loaded|M1 Task 5" PLAN.md STATUS.md README.md CHANGELOG.md docs/adr
```

Expected: Task 4 is complete on `main`; Task 5 is started only on the new branch; no document claims a functioning game plugin.

- [ ] **Step 4: Commit the design and task-boundary documentation.**

```powershell
git add PLAN.md STATUS.md README.md CHANGELOG.md docs/adr/ADR-0005-data-only-and-full-trust-code-mods.md docs/adr/ADR-0006-full-trust-code-mod-boundary.md docs/superpowers/specs/2026-08-04-m1-plugin-load-smoke-test-design.md docs/superpowers/plans/2026-08-04-m1-plugin-load-smoke-test.md
git commit -m "docs: start M1 plugin load probe"
```

### Task 2: Add the external probe project and synthetic fixture

**Files:**
- Create: `src/ThroneForge.PluginLoadTest/ThroneForge.PluginLoadTest.csproj`
- Create: `src/ThroneForge.PluginLoadTest/PluginLoadModels.cs`
- Create: `src/ThroneForge.PluginLoadTest/PluginLoadProbe.cs`
- Create: `tests/ThroneForge.PluginLoadFixture/ThroneForge.PluginLoadFixture.csproj`
- Create: `tests/ThroneForge.PluginLoadFixture/SyntheticThroneForgeMod.cs`
- Create: `tests/ThroneForge.PluginLoadTest.Tests/ThroneForge.PluginLoadTest.Tests.csproj`
- Modify: `ThroneForge.slnx`
- Modify: `Directory.Build.props` only if an existing project convention requires an explicit marker

- [ ] **Step 1: Write the failing project-discovery test.**

Add a skeleton test asserting that `PluginLoadProbe` can be constructed with a fixture assembly path and returns a `Loaded` result containing the fixture assembly identity. Run the focused test project and confirm it fails because the production project/types are absent.

- [ ] **Step 2: Define the minimal immutable result model.**

Use these exact public shapes in `PluginLoadModels.cs`:

```csharp
public enum PluginLoadStatus { Rejected, Loaded, Failed }
public sealed record PluginLoadRequest(
    string ArtifactPath,
    CodeModDescriptor Descriptor,
    GameFingerprint GameFingerprint,
    CodeModApprovalRecord Approval,
    AdapterCompatibilityEvidence CompatibilityEvidence);
public sealed record PluginLoadResult(
    PluginLoadStatus Status,
    string ReasonCode,
    string Message,
    CodeModAdmissionBinding? Binding,
    string? AssemblyName,
    IReadOnlyList<string> ImplementedContractTypes);
```

The result may contain only normalized assembly/type identities and sanitized categories; it must not contain paths, loaded objects, stack traces, or personal data.

- [ ] **Step 3: Implement the bounded artifact hash and gate sequence.**

`PluginLoadProbe.Load` must open `ArtifactPath` once, reject missing/non-regular/oversized files, calculate SHA-256, compare it with `Descriptor.PackageSha256`, construct verified `CodeModIntegrityEvidence` with method `sha256-file`, and call `CodeModAdmissionGate.Evaluate` immediately before `AssemblyLoadContext.LoadFromAssemblyPath`. Any non-Approved decision returns without calling the load context.

- [ ] **Step 4: Implement collectible loading without invocation.**

Load the exact artifact into a private collectible `AssemblyLoadContext`, inspect only assembly name and types assignable to `IThroneForgeMod`, require exactly one contract implementation, record its full type name, then unload the context. Do not call constructors or interface methods. Convert loader exceptions into `Failed` with a stable reason code and no raw path.

- [ ] **Step 5: Make the fixture minimal and game-free.**

The fixture must implement `IThroneForgeMod` using only the existing API types. It must not reference Contracts, Runtime, Discovery, LoaderSmokeTest, BepInEx, Harmony, Unity, adapter, or game assemblies. Do not commit its build output.

- [ ] **Step 6: Add projects to the solution and deliberate architecture allowlists.**

Add source and test projects to `ThroneForge.slnx`; update the explicit project-reference allowlist only for the new intended edges. Keep the external probe outside the concrete game-facing adapter and keep the fixture test-only.

- [ ] **Step 7: Run the focused test until green.**

```powershell
dotnet test tests/ThroneForge.PluginLoadTest.Tests -c Release
```

Expected: all focused tests pass with zero failures; no new package restore is required.

### Task 3: Add fail-closed regression coverage

**Files:**
- Modify: `tests/ThroneForge.PluginLoadTest.Tests/PluginLoadProbeTests.cs`
- Modify: `tests/ThroneForge.ArchitectureTests/*` only where the existing scanner requires explicit new project entries

- [ ] **Step 1: Add artifact-binding tests.**

Cover matching hash success, changed artifact rejection, wrong package hash rejection, uppercase digest normalization, and no load on integrity failure.

- [ ] **Step 2: Add approval and compatibility-binding tests.**

Cover missing approval, approval for another identity/hash/fingerprint, denied approval, compatibility fingerprint mismatch, warning/unknown compatibility, and preservation of the exact admission binding digest in a successful result.

- [ ] **Step 3: Add assembly-shape tests.**

Cover missing contract implementation, duplicate contract implementations, malformed/non-assembly input, exact assembly identity capture, and no constructor/lifecycle invocation. Use fixture source variants or temporary fixture files; never use a game or loader binary.

- [ ] **Step 4: Add unload and sanitization tests.**

Verify collectible-context unload after success, deterministic result fields, absence of absolute artifact paths and stack traces, and stable failure reason codes.

- [ ] **Step 5: Run the focused test suite and architecture tests.**

```powershell
dotnet test tests/ThroneForge.PluginLoadTest.Tests -c Release
dotnet test tests/ThroneForge.ArchitectureTests -c Release
```

Expected: all new tests and all architecture tests pass; no project contains forbidden game/loader dependencies.

### Task 4: Validate repository-wide and hand off the private experiment

**Files:**
- Modify: `PLAN.md`
- Modify: `STATUS.md`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/adr/ADR-0006-full-trust-code-mod-boundary.md`

- [ ] **Step 1: Run canonical validation with the repository SDK.**

```powershell
dotnet --version
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
git status --short
```

Expected hosted toolchain: SDK `10.0.100`, all tests green, no tracked binaries/private paths. If local SDK `10.0.100` is unavailable, record local compile/test with `10.0.110` separately and do not claim exact local format verification.

- [ ] **Step 2: Run hygiene checks.**

```powershell
git ls-files | Select-String -Pattern '\.(dll|exe|so|dylib|pdb|zip|log)$'
git ls-files | Select-String -Pattern '(^|/)(Thronefall|BepInEx|experiment|clean-game)(/|$)'
git grep -n -I -E 'C:\\Users\\|/home/|/Users/' -- ':!docs/THRONEFORGE_SPEC.md' ':!docs/superpowers/plans/*'
```

Expected: no tracked plugin/game/loader binaries, archives, raw logs, experiment state, or private paths.

- [ ] **Step 3: Update handoff documentation.**

State that Task 5 repository probe is complete only for the synthetic fixture; no plugin was loaded in Thronefall, no loader was installed, and the next permitted experiment is an explicitly approved disposable-profile test.

- [ ] **Step 4: Commit implementation and documentation.**

```powershell
git add src/ThroneForge.PluginLoadTest tests/ThroneForge.PluginLoadFixture tests/ThroneForge.PluginLoadTest.Tests tests/ThroneForge.ArchitectureTests ThroneForge.slnx PLAN.md STATUS.md README.md CHANGELOG.md docs/adr/ADR-0006-full-trust-code-mod-boundary.md
git commit -m "feat: add bound synthetic plugin load probe"
```

- [ ] **Step 5: Push and wait for hosted CI before requesting a PR.**

```powershell
git push -u origin agent/m1-plugin-load-smoke-test
```

Record the hosted Windows/Linux run, SDK, TRX counts, and tests. Do not run the private experiment in CI and do not claim real game plugin compatibility.
