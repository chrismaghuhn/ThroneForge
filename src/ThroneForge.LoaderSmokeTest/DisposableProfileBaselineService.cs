using System.Text.Json;

namespace ThroneForge.LoaderSmokeTest;

public static class DisposableProfileBaselineService
{
    public const string SchemaVersion = "throneforge-disposable-profile-baseline-v1";
    public const string TaskVersion = "m1-loader-smoke-test-v2";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void RequireFreshProfile(SmokeTestRoots roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (Directory.Exists(roots.CleanGameRoot) || File.Exists(roots.CleanGameRoot))
        {
            throw new SmokeTestException("The disposable clean-game directory already exists; choose a new experiment root or use explicit Resume mode with a valid baseline manifest.");
        }
    }

    public static void Save(string path, DisposableProfileBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (!string.Equals(baseline.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(baseline.TaskVersion, TaskVersion, StringComparison.Ordinal))
        {
            throw new SmokeTestException("The disposable profile baseline has an unsupported schema or task version.");
        }

        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path))
                ?? throw new SmokeTestException("The disposable profile baseline has no parent directory.");
            Directory.CreateDirectory(parent);
            File.WriteAllText(path, JsonSerializer.Serialize(baseline, JsonOptions));
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The disposable profile baseline could not be written safely.", exception);
        }
    }

    public static DisposableProfileBaseline LoadAndValidateResume(
        string path,
        string expectedFingerprint,
        CopyManifest originalManifest,
        CopyManifest currentManifest,
        ThroneForge.Discovery.SmokeTestReadiness readiness,
        IReadOnlyList<ThroneForge.Discovery.LoaderIndicatorEvidence> indicators)
    {
        ArgumentNullException.ThrowIfNull(originalManifest);
        ArgumentNullException.ThrowIfNull(currentManifest);
        ArgumentNullException.ThrowIfNull(indicators);
        DisposableProfileBaseline baseline;
        try
        {
            baseline = JsonSerializer.Deserialize<DisposableProfileBaseline>(File.ReadAllText(path))
                ?? throw new SmokeTestException("The disposable profile baseline is empty or invalid.");
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            throw new SmokeTestException("The disposable profile baseline is missing or malformed.", exception);
        }

        if (!string.Equals(baseline.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(baseline.TaskVersion, TaskVersion, StringComparison.Ordinal)
            || !string.Equals(baseline.ExpectedOriginalFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The disposable profile baseline does not match the expected task schema or original fingerprint.");
        }

        if (!InstallationCopyService.CompareManifests(baseline.OriginalManifest, originalManifest).Matches
            || !InstallationCopyService.CompareManifests(baseline.DisposableManifest, currentManifest).Matches)
        {
            throw new SmokeTestException("The disposable profile baseline no longer matches the original and current complete manifests.");
        }

        if (readiness != ThroneForge.Discovery.SmokeTestReadiness.ReadyForReversibleTest)
        {
            throw new SmokeTestException("The disposable profile is not ready for a clean-profile resume.");
        }

        if (indicators.Any(item => item.Status != ThroneForge.Discovery.LoaderIndicatorStatus.Absent))
        {
            throw new SmokeTestException("The disposable profile contains loader indicators; resume is blocked.");
        }

        return baseline;
    }
}
