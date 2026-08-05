using System.Text;
using System.Text.Json;
using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public enum Task6ExperimentStatus
{
    Prepared,
    LoaderApplied,
    PluginDeployed,
    LaunchObserved,
    ManualClosureRequired,
    RolledBack,
    Completed,
    Failed
}

public sealed record Task6ExperimentState(
    string SchemaVersion,
    string TaskVersion,
    string ExpectedFingerprint,
    string ExperimentId,
    string RepositoryBaselineCommit,
    string CleanGameRelativePath,
    Task6ExperimentStatus Status,
    string? PackageSha256 = null,
    string? AdmissionBindingDigest = null,
    string? PluginRelativeRoot = null,
    string? LoaderTransactionStatus = null);

public sealed record Task6RecoveryState(
    string SchemaVersion,
    string TaskVersion,
    string ExpectedFingerprint,
    string ExperimentId,
    string? PackageSha256,
    string? AdmissionBindingDigest,
    string PluginRelativeRoot,
    string LoaderTransactionStatus,
    string Status);

public static class Task6ExperimentStateService
{
    public const string SchemaVersion = "throneforge-task6-experiment-v1";
    public const string TaskVersion = "m1-disposable-bepinex-plugin-smoke-test-v2";
    public const string CleanGameRelativePath = "clean-game";
    public const string StateRelativePath = "manifests/task6-experiment-state.json";
    public const string RecoveryRelativePath = "evidence/task6-recovery-state.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Task6ExperimentState CreatePrepared(
        string experimentRoot,
        string expectedFingerprint,
        string repositoryBaselineCommit)
    {
        ValidateFingerprint(expectedFingerprint);
        ValidateRepositoryCommit(repositoryBaselineCommit);
        var root = Path.GetFullPath(experimentRoot);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new PluginSmokeException("A new Task-6 experiment requires a nonexistent or empty experiment root.");
        }

        return new Task6ExperimentState(
            SchemaVersion,
            TaskVersion,
            expectedFingerprint.ToLowerInvariant(),
            Guid.NewGuid().ToString("N"),
            repositoryBaselineCommit,
            CleanGameRelativePath,
            Task6ExperimentStatus.Prepared);
    }

    public static string GetStatePath(string experimentRoot)
        => Path.GetFullPath(Path.Combine(experimentRoot, StateRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static string GetRecoveryPath(string experimentRoot)
        => Path.GetFullPath(Path.Combine(experimentRoot, RecoveryRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static void SaveAtomic(string experimentRoot, Task6ExperimentState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateState(state);
        var root = Path.GetFullPath(experimentRoot);
        var statePath = GetStatePath(root);
        SmokeTestPathValidator.EnsureWithin(root, statePath);
        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(root);
        var parent = Path.GetDirectoryName(statePath) ?? throw new PluginSmokeException("The Task-6 ownership record has no safe parent.");
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(statePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, statePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("The Task-6 ownership record could not be written safely.", exception);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static Task6ExperimentState LoadAndValidate(
        string experimentRoot,
        string expectedFingerprint,
        string? expectedRepositoryCommit = null)
    {
        ValidateFingerprint(expectedFingerprint);
        var root = Path.GetFullPath(experimentRoot);
        var statePath = GetStatePath(root);
        SmokeTestPathValidator.EnsureWithin(root, statePath);
        if (!File.Exists(statePath))
        {
            throw new PluginSmokeException("The explicit experiment root is not owned by a Task-6 ownership record.");
        }

        try
        {
            SmokeTestPathValidator.EnsureNoReparsePointsOnPath(root);
            var state = JsonSerializer.Deserialize<Task6ExperimentState>(File.ReadAllText(statePath))
                ?? throw new PluginSmokeException("The Task-6 ownership record is empty or malformed.");
            ValidateState(state);
            if (!state.ExpectedFingerprint.Equals(expectedFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new PluginSmokeException("The Task-6 ownership record is bound to a different game fingerprint.");
            }

            if (expectedRepositoryCommit is not null
                && !state.RepositoryBaselineCommit.Equals(expectedRepositoryCommit, StringComparison.Ordinal))
            {
                throw new PluginSmokeException("The Task-6 ownership record is bound to a different repository baseline.");
            }

            return state;
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            throw new PluginSmokeException("The Task-6 ownership record is missing or malformed.", exception);
        }
    }

    public static void SaveRecoveryAtomic(string experimentRoot, Task6RecoveryState recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ValidateFingerprint(recovery.ExpectedFingerprint);
        if (string.IsNullOrWhiteSpace(recovery.ExperimentId)
            || !recovery.ExperimentId.All(char.IsAsciiLetterOrDigit)
            || !string.Equals(recovery.PluginRelativeRoot, "BepInEx/plugins/dev.throneforge.m1.synthetic-smoke", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(recovery.LoaderTransactionStatus))
        {
            throw new PluginSmokeException("The Task-6 recovery record contains unsafe ownership data.");
        }

        var root = Path.GetFullPath(experimentRoot);
        var path = GetRecoveryPath(root);
        SmokeTestPathValidator.EnsureWithin(root, path);
        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(root);
        var parent = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(recovery, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("The Task-6 recovery record could not be persisted safely.", exception);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static void ClearRecovery(string experimentRoot)
    {
        var path = GetRecoveryPath(experimentRoot);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ValidateState(Task6ExperimentState state)
    {
        if (!string.Equals(state.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(state.TaskVersion, TaskVersion, StringComparison.Ordinal)
            || !string.Equals(state.CleanGameRelativePath, CleanGameRelativePath, StringComparison.Ordinal)
            || !Guid.TryParseExact(state.ExperimentId, "N", out _))
        {
            throw new PluginSmokeException("The Task-6 ownership record has an unsupported schema, task, path, or experiment identity.");
        }

        ValidateFingerprint(state.ExpectedFingerprint);
        ValidateRepositoryCommit(state.RepositoryBaselineCommit);
        if (state.PackageSha256 is not null && !IsSha256(state.PackageSha256)
            || state.AdmissionBindingDigest is not null && !IsSha256(state.AdmissionBindingDigest))
        {
            throw new PluginSmokeException("The Task-6 ownership record contains an invalid package or admission digest.");
        }

        if (state.PluginRelativeRoot is not null
            && !state.PluginRelativeRoot.Equals("BepInEx/plugins/dev.throneforge.m1.synthetic-smoke", StringComparison.Ordinal))
        {
            throw new PluginSmokeException("The Task-6 ownership record contains an unsafe plugin root.");
        }
    }

    private static void ValidateFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new PluginSmokeException("The Task-6 fingerprint must be a 64-character SHA-256 value.");
        }
    }

    private static void ValidateRepositoryCommit(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '/' or '\\' or ':'))
        {
            throw new PluginSmokeException("The Task-6 repository baseline identifier is invalid.");
        }
    }

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);
}
