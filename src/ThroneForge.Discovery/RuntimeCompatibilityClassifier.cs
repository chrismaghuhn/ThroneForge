using System.Globalization;
using System.Text.RegularExpressions;

namespace ThroneForge.Discovery;

public static class RuntimeCompatibilityClassifier
{
    private static readonly Regex UnityVersionPattern = new(
        @"^(?<major>\d+)\.(?<minor>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TargetFrameworkRecommendation Recommend(
        IReadOnlyList<ManagedAssemblyEvidence> managedAssemblies,
        string unityVersion)
        => Assess(managedAssemblies, unityVersion).Recommendation;

    public static TargetFrameworkAssessment Assess(
        IReadOnlyList<ManagedAssemblyEvidence> managedAssemblies,
        string unityVersion)
    {
        ArgumentNullException.ThrowIfNull(managedAssemblies);
        ArgumentNullException.ThrowIfNull(unityVersion);

        var netstandardAssemblies = managedAssemblies
            .Where(item => string.Equals(item.AssemblyName, "netstandard", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var mscorlibAssemblies = managedAssemblies
            .Where(item => string.Equals(item.AssemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (netstandardAssemblies.Length > 0)
        {
            if (mscorlibAssemblies.Any(item => item.AssemblyVersion is not null && item.AssemblyVersion < new Version(4, 0)))
            {
                return Assessment(
                    TargetFrameworkRecommendation.Conflicting,
                    TargetFrameworkConfidence.None,
                    "netstandard and legacy mscorlib evidence conflict.");
            }

            var directRecommendations = netstandardAssemblies
                .Select(item => DirectNetstandardRecommendation(item.TargetFramework))
                .Where(item => item is not null)
                .Cast<TargetFrameworkRecommendation>()
                .Distinct()
                .ToArray();
            if (directRecommendations.Length > 1)
            {
                return Assessment(
                    TargetFrameworkRecommendation.Conflicting,
                    TargetFrameworkConfidence.None,
                    "TargetFrameworkAttribute values disagree across netstandard assemblies.");
            }

            if (directRecommendations.Length == 1)
            {
                return Assessment(
                    directRecommendations[0],
                    TargetFrameworkConfidence.High,
                    "A readable TargetFrameworkAttribute supports the exact recommendation.");
            }

            var inferredRecommendation = RecommendNetstandardFromUnityVersion(unityVersion);
            return inferredRecommendation is TargetFrameworkRecommendation.Netstandard20Candidate
                or TargetFrameworkRecommendation.Netstandard21Candidate
                ? Assessment(
                    inferredRecommendation,
                    TargetFrameworkConfidence.Medium,
                    $"netstandard compatibility surface plus Unity {UnityIdentity(unityVersion)} evidence")
                : Assessment(
                    inferredRecommendation,
                    TargetFrameworkConfidence.None,
                    "netstandard compatibility surface is present, but the Unity version does not resolve the exact TFM.");
        }

        if (mscorlibAssemblies.Length == 0)
        {
            return Assessment(
                TargetFrameworkRecommendation.Unknown,
                TargetFrameworkConfidence.None,
                "No netstandard.dll or mscorlib.dll compatibility evidence was found.");
        }

        var mscorlibVersions = mscorlibAssemblies
            .Select(item => item.AssemblyVersion)
            .Where(item => item is not null)
            .Cast<Version>()
            .Distinct()
            .ToArray();
        if (mscorlibVersions.Length == 0)
        {
            return Assessment(
                TargetFrameworkRecommendation.Unknown,
                TargetFrameworkConfidence.None,
                "mscorlib metadata did not expose an assembly version.");
        }

        if (mscorlibVersions.Any(item => item >= new Version(4, 0))
            && mscorlibVersions.Any(item => item < new Version(4, 0)))
        {
            return Assessment(
                TargetFrameworkRecommendation.Conflicting,
                TargetFrameworkConfidence.None,
                "mscorlib versions contain both modern and legacy evidence.");
        }

        return mscorlibVersions[0] >= new Version(4, 0)
            ? Assessment(
                TargetFrameworkRecommendation.Net46Candidate,
                TargetFrameworkConfidence.Medium,
                "mscorlib version 4.0.0.0 or newer supports a net46 candidate.")
            : Assessment(
                TargetFrameworkRecommendation.Net35FallbackCandidate,
                TargetFrameworkConfidence.Low,
                "Only legacy mscorlib evidence was found; net35 is a conservative fallback candidate.");
    }

    public static string RecommendedCandidate(
        ManagedRuntimeProfile profile,
        ExecutableArchitecture architecture,
        TargetFrameworkAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return profile == ManagedRuntimeProfile.Mono
            && architecture == ExecutableArchitecture.X64
            && assessment.Recommendation is not TargetFrameworkRecommendation.Conflicting
            and not TargetFrameworkRecommendation.Unknown
            ? "BepInEx 5 Unity Mono x64 5.4.23.5"
            : "No loader candidate recommended";
    }

    private static TargetFrameworkAssessment Assessment(
        TargetFrameworkRecommendation recommendation,
        TargetFrameworkConfidence confidence,
        string basis)
        => new(recommendation, confidence, basis);

    private static TargetFrameworkRecommendation? DirectNetstandardRecommendation(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return null;
        }

        if (targetFramework.Contains("netstandard2.1", StringComparison.OrdinalIgnoreCase)
            || targetFramework.Contains(".NETStandard,Version=v2.1", StringComparison.OrdinalIgnoreCase))
        {
            return TargetFrameworkRecommendation.Netstandard21Candidate;
        }

        if (targetFramework.Contains("netstandard2.0", StringComparison.OrdinalIgnoreCase)
            || targetFramework.Contains(".NETStandard,Version=v2.0", StringComparison.OrdinalIgnoreCase))
        {
            return TargetFrameworkRecommendation.Netstandard20Candidate;
        }

        return null;
    }

    private static TargetFrameworkRecommendation RecommendNetstandardFromUnityVersion(string unityVersion)
    {
        var match = UnityVersionPattern.Match(unityVersion);
        if (!match.Success
            || !int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return TargetFrameworkRecommendation.FrameworkCompatibleButExactTfmUnresolved;
        }

        return major > 2021 || major == 2021 && minor >= 2
            ? TargetFrameworkRecommendation.Netstandard21Candidate
            : TargetFrameworkRecommendation.Netstandard20Candidate;
    }

    private static string UnityIdentity(string unityVersion)
    {
        var match = Regex.Match(
            unityVersion,
            @"^(?<major>\d{4})\.(?<minor>\d+)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Value : "unknown Unity version";
    }
}
