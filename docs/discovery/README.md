# Local Thronefall discovery

The discovery tool records conservative, sanitized evidence from an explicitly supplied, legally obtained Thronefall installation. It is an external inspection tool, not a loader or mod runtime.

## Invocation

From the repository root, run:

```powershell
dotnet run --project src/ThroneForge.Discovery -- inspect `
  --game-path "C:\Path\To\Thronefall" `
  --output-root "docs/discovery"
```

`--game-path` must be an absolute directory path. The optional `--overwrite` flag is required to replace a report with the same fingerprint. Without it, an existing report is left unchanged and the command fails. The tool prints only the generated file name, classification, architecture, and fingerprint; it does not echo the supplied installation path.

## Evidence and safety boundaries

The tool reads directory and file metadata below the supplied root, selected PE headers, selected small compatibility files, and selected file contents for SHA-256 hashing. It never launches the game, modifies the installation, loads an assembly, executes code, follows symbolic links or reparse points, scans the computer or Steam library, copies binaries, decompiles source, or writes outside the requested output directory.

Reports contain relative paths only. They do not contain the absolute installation path, usernames, machine names, filesystem timestamps, environment variables, arbitrary directory listings, or copied binary contents.

## Backend classification

The names below are layout indicators and are not proof of game internals:

- `Mono` requires at least two compatible Mono signals, such as a Unity `*_Data/Managed` directory, `Assembly-CSharp.dll` beneath it, or a local `mono`/`MonoBleedingEdge` runtime directory.
- `IL2CPP` requires at least two compatible IL2CPP signals, such as `GameAssembly.dll`, a Unity `*_Data/il2cpp_data` directory, and `global-metadata.dat` beneath that directory.
- `Ambiguous` means signals from both backends were detected.
- `Unknown` means the evidence is absent or fewer than two compatible signals support one backend.

All detected signals and missing/conflicting explanations are written to the report.

## Fingerprint v1

The fingerprint is a lowercase SHA-256 digest of UTF-8, LF-separated, invariant-culture lines using the version marker `throneforge-game-fingerprint-v1`, backend classification, executable architecture, verified Unity-version evidence, and a sorted set of selected compatibility files. Each selected file contributes its normalized relative path, byte size, and lowercase SHA-256. Absolute paths, usernames, machine identifiers, local timestamps, and environment values are excluded. The entire installation is never hashed indiscriminately; selected file reads are limited to 64 MiB each.

## Limitations and next steps

Unity version is reported only when stable local evidence is readable. PE architecture supports x86, x64, and Arm64; malformed or unsupported headers produce `Unknown`. A classification does not establish loader compatibility, lifecycle behavior, game API bindings, or a target framework. Those questions require later, evidence-backed discovery tasks and must not be inferred from this report.
