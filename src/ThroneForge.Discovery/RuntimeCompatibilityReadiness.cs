namespace ThroneForge.Discovery;

public static class RuntimeCompatibilityReadiness
{
    public static SmokeTestReadinessAssessment Assess(
        ManagedRuntimeProfile profile,
        ExecutableArchitecture architecture,
        TargetFrameworkAssessment targetFramework,
        string unityVersion,
        IReadOnlyList<LoaderIndicatorEvidence> loaderIndicators)
    {
        ArgumentNullException.ThrowIfNull(targetFramework);
        ArgumentNullException.ThrowIfNull(unityVersion);
        ArgumentNullException.ThrowIfNull(loaderIndicators);

        var blockingIndicators = loaderIndicators
            .Where(item => item.Status != LoaderIndicatorStatus.Absent)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Name} ({item.Status})")
            .ToArray();
        if (blockingIndicators.Length > 0)
        {
            return new SmokeTestReadinessAssessment(
                SmokeTestReadiness.BlockedByExistingLoaderIndicators,
                blockingIndicators,
                "Remove or isolate the existing loader/bootstrap indicators in a separate clean profile, then inspect again.",
                "The installation contains non-absent loader or bootstrap indicators.");
        }

        if (profile == ManagedRuntimeProfile.Conflicting
            || targetFramework.Recommendation == TargetFrameworkRecommendation.Conflicting
            || string.Equals(unityVersion, "Conflicting", StringComparison.OrdinalIgnoreCase))
        {
            return new SmokeTestReadinessAssessment(
                SmokeTestReadiness.BlockedByConflictingCompatibilityEvidence,
                [],
                "Resolve the conflicting local compatibility evidence before attempting a smoke test.",
                "The local compatibility evidence is internally conflicting.");
        }

        if (profile != ManagedRuntimeProfile.Mono
            || architecture != ExecutableArchitecture.X64
            || targetFramework.Confidence == TargetFrameworkConfidence.None
            || targetFramework.Recommendation is TargetFrameworkRecommendation.Unknown
                or TargetFrameworkRecommendation.FrameworkCompatibleButExactTfmUnresolved)
        {
            return new SmokeTestReadinessAssessment(
                SmokeTestReadiness.Unsupported,
                [],
                "Collect sufficient Mono, x64, Unity, and framework evidence before attempting a smoke test.",
                "The current compatibility evidence does not support a reversible clean-profile test.");
        }

        return new SmokeTestReadinessAssessment(
            SmokeTestReadiness.ReadyForReversibleTest,
            [],
            "No automatic changes are required; use a separately backed-up clean profile for the later experiment.",
            "The local profile has sufficient non-conflicting evidence and no detected loader indicators.");
    }
}
