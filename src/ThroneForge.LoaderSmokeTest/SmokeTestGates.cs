namespace ThroneForge.LoaderSmokeTest;

public static class SmokeTestGates
{
    public static void RequireBaselineSuccess(LaunchObservationResult baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (!baseline.Started || !baseline.StableInitialized || baseline.RequiresManualClosure)
        {
            throw new SmokeTestException("BaselineLaunchInconclusive: installation is not eligible for loader installation.");
        }
    }

    public static void RequireOriginalUnchanged(string expectedFingerprint, string actualFingerprint)
    {
        if (!string.Equals(expectedFingerprint, actualFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The original installation changed during the experiment.");
        }
    }
}
