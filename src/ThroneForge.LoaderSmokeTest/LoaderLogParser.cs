using System.Text.RegularExpressions;

namespace ThroneForge.LoaderSmokeTest;

public static class LoaderLogParser
{
    private static readonly Regex VersionPattern = new(
        @"BepInEx\s+(?<version>\d+\.\d+\.\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PluginCountPattern = new(
        @"(?<count>\d+)\s+plugins?\s+(?:to\s+load|loaded|discovered)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static LoaderLogSummary Parse(string logText)
    {
        ArgumentNullException.ThrowIfNull(logText);
        string? version = null;
        var configurationGenerated = false;
        var preloader = false;
        var chainloader = false;
        var pluginCount = 0;
        var warnings = 0;
        var errors = 0;
        var fatal = 0;
        var categories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in logText.Split('\n'))
        {
            var normalized = line.Trim();
            var versionMatch = VersionPattern.Match(normalized);
            if (versionMatch.Success)
            {
                version ??= versionMatch.Groups["version"].Value;
            }

            if (ContainsAny(normalized, "configuration file generated", "config file generated", "configuration created"))
            {
                configurationGenerated = true;
            }

            if (ContainsAny(normalized, "preloader finished", "preloader initialized", "preloader startup complete"))
            {
                preloader = true;
            }

            if (ContainsAny(normalized, "chainloader initialized", "chainloader ready", "chainloader startup complete"))
            {
                chainloader = true;
            }

            var pluginMatch = PluginCountPattern.Match(normalized);
            if (pluginMatch.Success && int.TryParse(pluginMatch.Groups["count"].Value, out var parsedCount))
            {
                pluginCount = parsedCount;
            }

            if (normalized.Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                warnings++;
                categories.Add("loader-warning");
            }

            if (normalized.Contains("fatal", StringComparison.OrdinalIgnoreCase))
            {
                fatal++;
                errors++;
                categories.Add("fatal-loader-error");
            }
            else if (normalized.Contains("error", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("exception", StringComparison.OrdinalIgnoreCase))
            {
                errors++;
                categories.Add("loader-error");
            }
        }

        return new LoaderLogSummary(
            version,
            configurationGenerated,
            preloader,
            chainloader,
            pluginCount,
            warnings,
            errors,
            fatal,
            categories.Order(StringComparer.Ordinal).ToArray(),
            version is not null && preloader && chainloader && fatal == 0);
    }

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}

public static class SmokeTestOutcomeClassifier
{
    public static SmokeTestOutcome Classify(
        bool baselineSucceeded,
        bool loaderLaunchSucceeded,
        LoaderLogSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!baselineSucceeded || !loaderLaunchSucceeded || summary.FatalErrorCount > 0)
        {
            return SmokeTestOutcome.Failed;
        }

        if (!summary.StableInitialized)
        {
            return SmokeTestOutcome.Inconclusive;
        }

        return summary.WarningCount > 0 || summary.ErrorCount > 0
            ? SmokeTestOutcome.PassedWithWarnings
            : SmokeTestOutcome.Passed;
    }
}
