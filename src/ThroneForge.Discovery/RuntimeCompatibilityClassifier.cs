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
                return TargetFrameworkRecommendation.Conflicting;
            }

            var directRecommendations = netstandardAssemblies
                .Select(item => DirectNetstandardRecommendation(item.TargetFramework))
                .Where(item => item is not null)
                .Cast<TargetFrameworkRecommendation>()
                .Distinct()
                .ToArray();
            if (directRecommendations.Length > 1)
            {
                return TargetFrameworkRecommendation.Conflicting;
            }

            if (directRecommendations.Length == 1)
            {
                return directRecommendations[0];
            }

            return RecommendNetstandardFromUnityVersion(unityVersion);
        }

        if (mscorlibAssemblies.Length == 0)
        {
            return TargetFrameworkRecommendation.Unknown;
        }

        var mscorlibVersions = mscorlibAssemblies
            .Select(item => item.AssemblyVersion)
            .Where(item => item is not null)
            .Cast<Version>()
            .Distinct()
            .ToArray();
        if (mscorlibVersions.Length == 0)
        {
            return TargetFrameworkRecommendation.Unknown;
        }

        if (mscorlibVersions.Any(item => item >= new Version(4, 0))
            && mscorlibVersions.Any(item => item < new Version(4, 0)))
        {
            return TargetFrameworkRecommendation.Conflicting;
        }

        return mscorlibVersions[0] >= new Version(4, 0)
            ? TargetFrameworkRecommendation.Net46Candidate
            : TargetFrameworkRecommendation.Net35FallbackCandidate;
    }

    public static string Confidence(TargetFrameworkRecommendation recommendation)
        => recommendation switch
        {
            TargetFrameworkRecommendation.Netstandard21Candidate
                or TargetFrameworkRecommendation.Netstandard20Candidate => "High when direct metadata is present; medium when inferred from Unity version.",
            TargetFrameworkRecommendation.Net46Candidate
                or TargetFrameworkRecommendation.Net35FallbackCandidate => "Medium; based on mscorlib version without a loader smoke test.",
            TargetFrameworkRecommendation.Net472FallbackCandidate => "Low; fallback only and not selected as the primary target.",
            TargetFrameworkRecommendation.FrameworkCompatibleButExactTfmUnresolved => "Low; compatibility surface is present but exact TFM evidence is missing.",
            TargetFrameworkRecommendation.Conflicting => "None; conflicting evidence must be resolved before target selection.",
            _ => "None; insufficient evidence."
        };

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
}
