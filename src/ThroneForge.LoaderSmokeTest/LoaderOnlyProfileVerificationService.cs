namespace ThroneForge.LoaderSmokeTest;

/// <summary>
/// Compares a loader-only profile after plugin removal. BepInEx's two documented
/// log paths may change contents, but their presence and all other profile facts
/// remain exact.
/// </summary>
public static class LoaderOnlyProfileVerificationService
{
    private static readonly HashSet<string> VolatileLogPaths = new(StringComparer.Ordinal)
    {
        "BepInEx/LogOutput.log",
        "BepInEx/LogOutput.txt"
    };

    public static ManifestVerificationResult Compare(CopyManifest expected, CopyManifest actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var expectedLogs = GetRecognizedLogPaths(expected);
        var actualLogs = GetRecognizedLogPaths(actual);
        if (!expectedLogs.SetEquals(actualLogs))
        {
            return InstallationCopyService.CompareManifests(expected, actual);
        }

        return InstallationCopyService.CompareManifests(
            NormalizeVolatileLogs(expected),
            NormalizeVolatileLogs(actual));
    }

    private static HashSet<string> GetRecognizedLogPaths(CopyManifest manifest)
        => manifest.Files
            .Where(file => VolatileLogPaths.Contains(file.RelativePath))
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

    private static CopyManifest NormalizeVolatileLogs(CopyManifest manifest)
        => manifest with
        {
            Files = manifest.Files
                .Select(file => VolatileLogPaths.Contains(file.RelativePath)
                    ? file with { Size = 0, Sha256 = new string('0', 64) }
                    : file)
                .ToArray()
        };
}
