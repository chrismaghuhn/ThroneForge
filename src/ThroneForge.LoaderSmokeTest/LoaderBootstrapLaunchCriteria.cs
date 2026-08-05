namespace ThroneForge.LoaderSmokeTest;

public static class LoaderBootstrapLaunchCriteria
{
    public static bool IsObserved(
        LaunchObservationResult launch,
        LoaderLogSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return launch.Started
            && launch.Exited
            && launch.ExecutableWasInsideExperiment
            && !launch.RequiresManualClosure
            && string.Equals(summary.BepInExVersion, "5.4.23.5", StringComparison.Ordinal)
            && summary.PreloaderInitialized
            && summary.ChainloaderInitialized
            && summary.PluginsDiscovered == 0
            && summary.FatalErrorCount == 0;
    }
}
