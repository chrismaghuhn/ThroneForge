using System.Text;
using System.Text.Json;

namespace ThroneForge.LoaderSmokeTest;

public static class LoaderTransactionStateService
{
    public const string SchemaVersion = "throneforge-loader-transaction-v1";
    public const string TaskVersion = "m1-loader-smoke-test-v3";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void SaveAtomic(string path, LoaderTransactionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.Equals(state.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(state.TaskVersion, TaskVersion, StringComparison.Ordinal))
        {
            throw new SmokeTestException("The loader transaction state has an unsupported schema or task version.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(fullPath)
                ?? throw new SmokeTestException("The loader transaction state has no parent directory.");
            Directory.CreateDirectory(parent);
            var temporary = Path.Combine(parent, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
                File.Move(temporary, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The loader transaction state could not be written safely.", exception);
        }
    }

    public static LoaderTransactionState LoadAndValidate(
        string path,
        SmokeTestRoots roots,
        string expectedFingerprint,
        CopyManifest baselineManifest,
        IEnumerable<LoaderTransactionStatus> allowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(baselineManifest);
        ArgumentNullException.ThrowIfNull(allowedStatuses);
        LoaderTransactionState state;
        try
        {
            state = JsonSerializer.Deserialize<LoaderTransactionState>(File.ReadAllText(path))
                ?? throw new SmokeTestException("The loader transaction state is empty or invalid.");
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            throw new SmokeTestException("The loader transaction state is missing or malformed.", exception);
        }

        ValidateStateMetadata(state, expectedFingerprint, baselineManifest, allowedStatuses);
        LoaderTransactionService.ValidatePersistedEntries(roots, state.Entries);
        ValidateManifest(roots, state.ExpectedAppliedManifest);
        ValidateGeneratedEvidence(roots, state.GeneratedEvidenceFiles, state.GeneratedEvidenceDirectories ?? []);
        return state;
    }

    public static void VerifyAppliedProfile(SmokeTestRoots roots, LoaderTransactionState state)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(state);
        LoaderTransactionService.ValidatePersistedEntries(roots, state.Entries);
        ValidateManifest(roots, state.ExpectedAppliedManifest);
        ValidateGeneratedEvidence(roots, state.GeneratedEvidenceFiles, state.GeneratedEvidenceDirectories ?? []);

        var expected = MergeGeneratedEvidence(state.ExpectedAppliedManifest, state.GeneratedEvidenceFiles, state.GeneratedEvidenceDirectories ?? []);
        var actual = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        if (!InstallationCopyService.CompareManifests(expected, actual).Matches)
        {
            throw new SmokeTestException("The disposable profile does not match the persisted loader transaction state.");
        }

        foreach (var entry in state.Entries)
        {
            var destination = Path.Combine(roots.CleanGameRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            switch (entry.Change)
            {
                case TransactionChangeKind.NewFile:
                case TransactionChangeKind.Overwrite:
                    if (!File.Exists(destination)
                        || !string.Equals(FileManifestHasher.HashFile(roots.CleanGameRoot, destination).Sha256, entry.ReplacementSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SmokeTestException("A persisted loader replacement file is missing or has an unexpected hash.");
                    }

                    break;
                case TransactionChangeKind.Unchanged:
                    if (!File.Exists(destination)
                        || !string.Equals(FileManifestHasher.HashFile(roots.CleanGameRoot, destination).Sha256, entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SmokeTestException("A persisted unchanged loader file is missing or has an unexpected hash.");
                    }

                    break;
                case TransactionChangeKind.CreatedDirectory:
                    if (!Directory.Exists(destination))
                    {
                        throw new SmokeTestException("A persisted loader directory is missing.");
                    }

                    break;
                default:
                    throw new SmokeTestException("The persisted loader transaction contains an unsupported change kind.");
            }
        }
    }

    public static IReadOnlyList<FileManifestEntry> CaptureGeneratedEvidence(
        CopyManifest appliedManifest,
        CopyManifest currentManifest,
        out IReadOnlyList<string> generatedDirectories)
    {
        var comparison = InstallationCopyService.CompareManifests(appliedManifest, currentManifest);
        if (comparison.RemovedFiles.Count > 0 || comparison.ChangedFiles.Count > 0)
        {
            throw new SmokeTestException("The loader launch changed a file outside the allowed generated evidence set.");
        }

        foreach (var added in comparison.AddedFiles)
        {
            if (!IsAllowedGeneratedPath(added.RelativePath))
            {
                throw new SmokeTestException("The loader launch created an unapproved generated file.");
            }
        }

        var directories = comparison.UnexpectedDirectories
            .Where(IsAllowedGeneratedPath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (comparison.UnexpectedDirectories.Any(path => !IsAllowedGeneratedPath(path)))
        {
            throw new SmokeTestException("The loader launch created an unapproved generated directory.");
        }

        generatedDirectories = directories;
        return comparison.AddedFiles
            .Select(item => item.Actual!)
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateStateMetadata(
        LoaderTransactionState state,
        string expectedFingerprint,
        CopyManifest baselineManifest,
        IEnumerable<LoaderTransactionStatus> allowedStatuses)
    {
        if (state.ExpectedAppliedManifest is null
            || state.Entries is null
            || state.GeneratedEvidenceFiles is null)
        {
            throw new SmokeTestException("The loader transaction state is missing required collections.");
        }

        if (state.Entries.Count == 0
            || !state.Entries.Any(entry => entry.Change is TransactionChangeKind.NewFile or TransactionChangeKind.Overwrite))
        {
            throw new SmokeTestException("The loader transaction state does not describe an applied loader payload.");
        }

        if (!string.Equals(state.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(state.TaskVersion, TaskVersion, StringComparison.Ordinal)
            || !string.Equals(state.ExpectedFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The loader transaction state does not match the expected schema, task, or fingerprint.");
        }

        if (!allowedStatuses.Contains(state.Status))
        {
            throw new SmokeTestException($"The loader transaction state '{state.Status}' is not valid for this staged operation.");
        }

        if (string.IsNullOrWhiteSpace(state.BaselineManifestIdentity)
            || !string.Equals(state.BaselineManifestIdentity, InstallationCopyService.ComputeManifestIdentity(baselineManifest), StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The loader transaction state is tied to a different disposable baseline.");
        }

        if (string.IsNullOrWhiteSpace(state.ArchiveName)
            || Path.GetFileName(state.ArchiveName) != state.ArchiveName
            || !string.Equals(state.ArchiveName, "BepInEx_win_x64_5.4.23.5.zip", StringComparison.OrdinalIgnoreCase)
            || !IsSha256(state.ObservedArchiveSha256))
        {
            throw new SmokeTestException("The loader transaction state has invalid archive identity metadata.");
        }

        if (state.Status == LoaderTransactionStatus.LaunchObserved
            && state.LaunchEvidence is null)
        {
            throw new SmokeTestException("The launch-observed loader transaction is missing bounded bootstrap evidence.");
        }

        if (state.LaunchEvidence is not null
            && (state.LaunchEvidence.PluginsDiscovered < 0
                || state.LaunchEvidence.WarningCount < 0
                || state.LaunchEvidence.ErrorCount < 0
                || state.LaunchEvidence.FatalErrorCount < 0))
        {
            throw new SmokeTestException("The loader transaction contains invalid bootstrap counters.");
        }
    }

    private static void ValidateManifest(SmokeTestRoots roots, CopyManifest manifest)
    {
        if (manifest is null || manifest.Files is null)
        {
            throw new SmokeTestException("The persisted loader manifest is missing its file collection.");
        }

        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (file is null)
            {
                throw new SmokeTestException("The persisted loader manifest contains a null file entry.");
            }

            ValidateRelativePath(roots, file.RelativePath, roots.CleanGameRoot);
            if (!files.Add(file.RelativePath))
            {
                throw new SmokeTestException("The persisted loader manifest contains duplicate file paths.");
            }

            if (!IsSha256(file.Sha256) || file.Size < 0)
            {
                throw new SmokeTestException("The persisted loader manifest contains an invalid file entry.");
            }
        }

        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in manifest.Directories ?? [])
        {
            ValidateRelativePath(roots, directory, roots.CleanGameRoot);
            if (!directories.Add(directory))
            {
                throw new SmokeTestException("The persisted loader manifest contains duplicate directory paths.");
            }
        }

        if (files.Overlaps(directories))
        {
            throw new SmokeTestException("The persisted loader manifest uses one path as both a file and a directory.");
        }
    }

    private static void ValidateGeneratedEvidence(
        SmokeTestRoots roots,
        IReadOnlyList<FileManifestEntry> files,
        IReadOnlyList<string> directories)
    {
        var filesSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (file is null)
            {
                throw new SmokeTestException("The persisted generated-evidence manifest contains a null file entry.");
            }

            ValidateRelativePath(roots, file.RelativePath, roots.CleanGameRoot);
            if (!filesSeen.Add(file.RelativePath))
            {
                throw new SmokeTestException("The persisted generated-evidence manifest contains duplicate file paths.");
            }

            if (!IsAllowedGeneratedPath(file.RelativePath) || !IsSha256(file.Sha256) || file.Size < 0)
            {
                throw new SmokeTestException("The persisted generated-evidence manifest contains an invalid entry.");
            }
        }

        var directoriesSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            ValidateRelativePath(roots, directory, roots.CleanGameRoot);
            if (!directoriesSeen.Add(directory))
            {
                throw new SmokeTestException("The persisted generated-evidence manifest contains duplicate directories.");
            }

            if (!IsAllowedGeneratedPath(directory))
            {
                throw new SmokeTestException("The persisted generated-evidence manifest contains an unapproved directory.");
            }
        }

        if (filesSeen.Overlaps(directoriesSeen))
        {
            throw new SmokeTestException("The persisted generated-evidence manifest uses one path as both a file and a directory.");
        }
    }

    private static CopyManifest MergeGeneratedEvidence(
        CopyManifest applied,
        IReadOnlyList<FileManifestEntry> files,
        IReadOnlyList<string> directories)
    {
        var mergedFiles = applied.Files.Concat(files).ToArray();
        if (mergedFiles.GroupBy(item => item.RelativePath, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new SmokeTestException("The persisted loader state contains duplicate generated file paths.");
        }

        var mergedDirectories = (applied.Directories ?? []).Concat(directories).ToArray();
        if (mergedDirectories.GroupBy(item => item, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new SmokeTestException("The persisted loader state contains duplicate generated directory paths.");
        }

        return new CopyManifest(mergedFiles, mergedDirectories);
    }

    private static void ValidateRelativePath(SmokeTestRoots roots, string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains('\0', StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal)
            || path.StartsWith('/')
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new SmokeTestException("The persisted loader transaction contains an unsafe relative path.");
        }

        SmokeTestPathValidator.EnsureWithin(
            root,
            Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsAllowedGeneratedPath(string path)
        => string.Equals(path, "BepInEx/LogOutput.log", StringComparison.Ordinal)
            || string.Equals(path, "BepInEx/LogOutput.txt", StringComparison.Ordinal)
            || path.StartsWith("BepInEx/config/", StringComparison.Ordinal)
            || path.StartsWith("BepInEx/cache/", StringComparison.Ordinal)
            || string.Equals(path, "BepInEx/config", StringComparison.Ordinal)
            || string.Equals(path, "BepInEx/cache", StringComparison.Ordinal)
            || string.Equals(path, "BepInEx/plugins", StringComparison.Ordinal)
            || string.Equals(path, "BepInEx/patchers", StringComparison.Ordinal);

    private static bool IsSha256(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length == 64
            && value.All(Uri.IsHexDigit);
}
