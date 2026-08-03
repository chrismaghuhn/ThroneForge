namespace ThroneForge.Discovery;

internal static class LoaderIndicatorInspector
{
    private static readonly (string Name, bool IsDirectory, LoaderIndicatorStatus PresentStatus)[] Indicators =
    [
        ("winhttp.dll", false, LoaderIndicatorStatus.Ambiguous),
        ("version.dll", false, LoaderIndicatorStatus.Ambiguous),
        ("doorstop_config.ini", false, LoaderIndicatorStatus.PotentialConflict),
        ("doorstop_config.ini.bak", false, LoaderIndicatorStatus.Ambiguous),
        ("BepInEx/", true, LoaderIndicatorStatus.PotentialConflict),
        ("MelonLoader/", true, LoaderIndicatorStatus.PotentialConflict),
        ("Mods/", true, LoaderIndicatorStatus.Ambiguous),
        ("Plugins/", true, LoaderIndicatorStatus.Ambiguous)
    ];

    public static IReadOnlyList<LoaderIndicatorEvidence> Inspect(DirectoryInfo gameRoot)
    {
        ArgumentNullException.ThrowIfNull(gameRoot);
        var result = new List<LoaderIndicatorEvidence>(Indicators.Length);
        foreach (var indicator in Indicators)
        {
            var relativePath = indicator.Name.TrimEnd('/');
            var present = false;
            var rejected = false;
            try
            {
                present = indicator.IsDirectory
                    ? DiscoveryPathValidator.TryResolveReadDirectory(gameRoot, relativePath, out _)
                    : DiscoveryPathValidator.TryResolveReadFile(gameRoot, relativePath, out _);
            }
            catch (DiscoveryException)
            {
                rejected = true;
            }

            var status = rejected
                ? LoaderIndicatorStatus.Ambiguous
                : present
                    ? indicator.PresentStatus
                    : LoaderIndicatorStatus.Absent;
            var explanation = status switch
            {
                LoaderIndicatorStatus.Absent => "No item with this exact relative name was detected.",
                LoaderIndicatorStatus.Ambiguous => "The indicator is present or could not be safely classified; its filename alone does not identify a loader.",
                LoaderIndicatorStatus.PotentialConflict => "The indicator may affect bootstrap or mod loading and requires a later clean-profile check.",
                _ => "The indicator is present."
            };
            result.Add(new LoaderIndicatorEvidence(indicator.Name, relativePath, status, explanation));
        }

        return result;
    }
}
