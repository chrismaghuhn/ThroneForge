namespace ThroneForge.Discovery;

public sealed record InstallationDiscoverySnapshot(
    BackendClassification Backend,
    ExecutableArchitecture ExecutableArchitecture,
    string UnityVersion,
    string? SelectedExecutableRelativePath,
    IReadOnlyList<EvidenceItem> DetectedEvidence,
    IReadOnlyList<string> MissingOrConflictingEvidence,
    IReadOnlyList<SelectedFileEvidence> SelectedFiles,
    string Fingerprint);

public static class InstallationFingerprintService
{
    public static InstallationDiscoverySnapshot Capture(string gamePath)
    {
        var gameRoot = DiscoveryPathValidator.ValidateGameRoot(gamePath);
        return Capture(gameRoot);
    }

    internal static InstallationDiscoverySnapshot Capture(DirectoryInfo gameRoot)
    {
        ArgumentNullException.ThrowIfNull(gameRoot);
        return DiscoveryEngine.CaptureSnapshot(gameRoot);
    }
}
