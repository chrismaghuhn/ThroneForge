using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ThroneForge.Discovery;

public sealed class RuntimeCompatibilityEngine
{
    private static readonly Regex FingerprintPattern = new(
        "^[0-9a-f]{64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly IReadOnlyList<BepInExCandidate> OfficialCandidates =
    [
        new BepInExCandidate(
            "BepInEx 5 Unity Mono x64",
            "5.4.23.5",
            "Stable release",
            "Matches Mono evidence",
            "Matches X64 evidence when the selected executable is X64",
            "Framework depends on local mscorlib/netstandard evidence",
            "LTS",
            "Exact Unity version, plugin TFM, and clean-profile behavior remain unverified",
            "Leading candidate for a later clean-profile smoke test when Mono and X64 evidence agree",
            "Releases · BepInEx/BepInEx",
            "https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5",
            "2026-08-03"),
        new BepInExCandidate(
            "BepInEx 6 Unity Mono x64",
            "6.0.0-pre.2",
            "Pre-release",
            "Matches Mono evidence",
            "Matches X64 evidence when the selected executable is X64",
            "Exact plugin TFM is not selected by this task",
            "Bleeding edge/pre-release",
            "Pre-release status and local loader compatibility are unverified",
            "Comparison candidate only; not the default smoke-test choice",
            "Releases · BepInEx/BepInEx",
            "https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2",
            "2026-08-03"),
        new BepInExCandidate(
            "No loader / unsupported",
            "n/a",
            "Not a loader",
            "Used when backend evidence is insufficient or conflicting",
            "Not applicable",
            "Not applicable",
            "Safest fallback when evidence is insufficient",
            "Does not provide runtime injection",
            "Only conclusion permitted when evidence cannot support a loader candidate",
            "ThroneForge discovery policy",
            "docs/discovery/README.md",
            "2026-08-03")
    ];

    private readonly Func<string, string?> versionResourceReader;

    public RuntimeCompatibilityEngine(Func<string, string?>? versionResourceReader = null)
    {
        this.versionResourceReader = versionResourceReader ?? UnityVersionEvidenceReader.ReadFileVersionResource;
    }

    public RuntimeCompatibilityResult Inspect(RuntimeCompatibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateFingerprint(request.BaseFingerprint);

        var gameRoot = DiscoveryPathValidator.ValidateGameRoot(request.GamePath);
        var outputRoot = DiscoveryPathValidator.ValidateOutputRoot(gameRoot, request.OutputRoot);
        var snapshot = InstallationFingerprintService.Capture(gameRoot);
        var expectedFingerprint = request.BaseFingerprint.ToLowerInvariant();
        if (!string.Equals(snapshot.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            throw new DiscoveryException(
                "The inspected installation does not match the supplied base fingerprint. "
                + $"Expected: {expectedFingerprint} Actual: {snapshot.Fingerprint}");
        }

        var legacyExplanations = new List<string>();
        var dataDirectories = FindDataDirectories(gameRoot, legacyExplanations);
        var executableRelativePath = snapshot.SelectedExecutableRelativePath;
        var executableArchitecture = snapshot.ExecutableArchitecture;

        var runtimeLayoutEvidence = InspectRuntimeLayout(gameRoot, dataDirectories, legacyExplanations);
        var managedAssemblies = InspectManagedAssemblies(
            gameRoot,
            dataDirectories,
            legacyExplanations);
        var managedRuntimeProfile = ClassifyManagedRuntimeProfile(runtimeLayoutEvidence, legacyExplanations);
        var unityVersionEvidence = UnityVersionEvidenceReader.Read(
            gameRoot,
            dataDirectories,
            executableRelativePath,
            versionResourceReader);
        var evidenceIssues = legacyExplanations
            .Select(CategorizeIssue)
            .Concat(unityVersionEvidence.Issues)
            .DistinctBy(item => (item.Category, item.Message))
            .ToArray();

        var targetFrameworkAssessment = RuntimeCompatibilityClassifier.Assess(
            managedAssemblies.Where(item => item.HasManagedMetadata).ToArray(),
            unityVersionEvidence.Version);
        var loaderIndicators = LoaderIndicatorInspector.Inspect(gameRoot);
        var recommendedCandidate = RuntimeCompatibilityClassifier.RecommendedCandidate(
            managedRuntimeProfile,
            executableArchitecture,
            targetFrameworkAssessment);
        var readiness = RuntimeCompatibilityReadiness.Assess(
            managedRuntimeProfile,
            executableArchitecture,
            targetFrameworkAssessment,
            unityVersionEvidence.Version,
            loaderIndicators);
        var missingOrConflictingEvidence = evidenceIssues.Select(item => item.Message).ToArray();
        var reportMarkdown = BuildReport(
            request,
            managedRuntimeProfile,
            executableArchitecture,
            executableRelativePath,
            runtimeLayoutEvidence,
            managedAssemblies,
            targetFrameworkAssessment,
            unityVersionEvidence,
            loaderIndicators,
            recommendedCandidate,
            readiness,
            evidenceIssues);
        var reportPath = DiscoveryReportWriter.WriteFile(
            outputRoot,
            $"{request.BaseFingerprint}-runtime-compatibility.md",
            reportMarkdown,
            request.OverwriteExisting);

        return new RuntimeCompatibilityResult(
            request.BaseFingerprint,
            DiscoveryEngine.DiscoveryToolVersion,
            $"Base game fingerprint {request.BaseFingerprint}; backend and architecture evidence are installation-specific.",
            executableArchitecture,
            executableRelativePath,
            managedRuntimeProfile,
            runtimeLayoutEvidence,
            managedAssemblies,
            targetFrameworkAssessment.Recommendation,
            targetFrameworkAssessment,
            unityVersionEvidence.Version,
            unityVersionEvidence.Evidence,
            loaderIndicators,
            OfficialCandidates,
            recommendedCandidate,
            readiness,
            evidenceIssues,
            missingOrConflictingEvidence,
            reportPath,
            reportMarkdown);
    }

    private static void ValidateFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || !FingerprintPattern.IsMatch(fingerprint))
        {
            throw new DiscoveryException("--fingerprint must be a 64-character SHA-256 value.");
        }
    }

    private static string[] FindDataDirectories(
        DirectoryInfo gameRoot,
        List<string> explanations)
    {
        try
        {
            var directories = gameRoot.EnumerateDirectories()
                .Where(directory =>
                {
                    try
                    {
                        return (directory.Attributes & FileAttributes.ReparsePoint) == 0
                            && directory.Name.EndsWith("_Data", StringComparison.OrdinalIgnoreCase);
                    }
                    catch (IOException)
                    {
                        return false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return false;
                    }
                })
                .Select(directory => directory.Name)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (directories.Length == 0)
            {
                explanations.Add("No top-level Unity *_Data directory was found.");
            }
            else if (directories.Length > 1)
            {
                explanations.Add("Multiple top-level Unity *_Data directories were found.");
            }

            return directories;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DiscoveryException("The installation could not be enumerated safely.", exception);
        }
    }

    private static ManagedAssemblyEvidence[] InspectManagedAssemblies(
        DirectoryInfo gameRoot,
        IReadOnlyList<string> dataDirectories,
        List<string> explanations)
    {
        var frameworkFileNames = new[]
        {
            "Managed/mscorlib.dll",
            "Managed/netstandard.dll",
            "Managed/System.dll",
            "Managed/System.Core.dll",
            "Managed/UnityEngine.dll",
            "Managed/UnityEngine.CoreModule.dll"
        };
        var evidence = new List<ManagedAssemblyEvidence>();
        foreach (var dataDirectory in dataDirectories)
        {
            foreach (var frameworkFileName in frameworkFileNames)
            {
                var relativePath = $"{dataDirectory}/{frameworkFileName}";
                if (!DiscoveryPathValidator.TryResolveReadFile(gameRoot, relativePath, out var fullPath))
                {
                    continue;
                }

                var inspected = ManagedAssemblyInspector.TryInspect(fullPath, relativePath, out var assemblyEvidence);
                evidence.Add(assemblyEvidence);
                if (!inspected && assemblyEvidence.FailureReason is not null)
                {
                    explanations.Add($"Managed compatibility candidate '{relativePath}': {assemblyEvidence.FailureReason}");
                }
            }
        }

        return evidence
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static RuntimeLayoutEvidence[] InspectRuntimeLayout(
        DirectoryInfo gameRoot,
        string[] dataDirectories,
        List<string> explanations)
    {
        var candidates = new List<(string RelativePath, bool IsDirectory)>();
        foreach (var dataDirectory in dataDirectories)
        {
            candidates.Add(($"{dataDirectory}/Managed", true));
            candidates.Add(($"{dataDirectory}/Managed/Assembly-CSharp.dll", false));
            candidates.Add(($"{dataDirectory}/MonoBleedingEdge", true));
            candidates.Add(($"{dataDirectory}/mono", true));
            candidates.Add(($"{dataDirectory}/il2cpp_data", true));
            candidates.Add(($"{dataDirectory}/il2cpp_data/Metadata/global-metadata.dat", false));
        }

        candidates.Add(("MonoBleedingEdge", true));
        candidates.Add(("mono", true));
        candidates.Add(("mono-2.0-bdwgc.dll", false));
        candidates.Add(("MonoPosixHelper.dll", false));
        candidates.Add(("GameAssembly.dll", false));

        return candidates
            .Distinct()
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(item =>
            {
                try
                {
                    var present = item.IsDirectory
                        ? DiscoveryPathValidator.TryResolveReadDirectory(gameRoot, item.RelativePath, out _)
                        : DiscoveryPathValidator.TryResolveReadFile(gameRoot, item.RelativePath, out _);
                    return new RuntimeLayoutEvidence(
                        item.RelativePath,
                        item.IsDirectory,
                        present,
                        present ? "Detected as a local runtime-layout indicator." : "Not detected.");
                }
                catch (DiscoveryException)
                {
                    explanations.Add($"Runtime-layout indicator '{item.RelativePath}' could not be inspected safely.");
                    return new RuntimeLayoutEvidence(
                        item.RelativePath,
                        item.IsDirectory,
                        false,
                        "Present or inaccessible, but not safely inspectable.");
                }
            })
            .ToArray();
    }

    private static ManagedRuntimeProfile ClassifyManagedRuntimeProfile(
        RuntimeLayoutEvidence[] runtimeLayoutEvidence,
        List<string> explanations)
    {
        var monoSignals = runtimeLayoutEvidence.Count(item =>
            item.Present
            && (item.RelativePath.EndsWith("/Managed", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.EndsWith("/Managed/Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.EndsWith("/MonoBleedingEdge", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.Equals("MonoBleedingEdge", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.EndsWith("/mono", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.Equals("mono", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.Equals("mono-2.0-bdwgc.dll", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.Equals("MonoPosixHelper.dll", StringComparison.OrdinalIgnoreCase)));
        var il2CppSignals = runtimeLayoutEvidence.Count(item =>
            item.Present
            && (item.RelativePath.EndsWith("/il2cpp_data", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.EndsWith("/il2cpp_data/Metadata/global-metadata.dat", StringComparison.OrdinalIgnoreCase)
                || item.RelativePath.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase)));

        var profile = monoSignals > 0 && il2CppSignals > 0
            ? ManagedRuntimeProfile.Conflicting
            : monoSignals >= 2
                ? ManagedRuntimeProfile.Mono
                : il2CppSignals >= 2
                    ? ManagedRuntimeProfile.IL2CPP
                    : ManagedRuntimeProfile.Unknown;
        if (profile == ManagedRuntimeProfile.Unknown)
        {
            explanations.Add($"Insufficient managed-runtime evidence: Mono signals={monoSignals}, IL2CPP signals={il2CppSignals}.");
        }
        else if (profile == ManagedRuntimeProfile.Conflicting)
        {
            explanations.Add("Conflicting Mono and IL2CPP managed-runtime indicators were found.");
        }

        return profile;
    }

    private static string BuildReport(
        RuntimeCompatibilityRequest request,
        ManagedRuntimeProfile managedRuntimeProfile,
        ExecutableArchitecture executableArchitecture,
        string? selectedExecutableRelativePath,
        RuntimeLayoutEvidence[] runtimeLayoutEvidence,
        ManagedAssemblyEvidence[] managedAssemblies,
        TargetFrameworkAssessment targetFrameworkAssessment,
        UnityVersionScanResult unityVersionEvidence,
        IReadOnlyList<LoaderIndicatorEvidence> loaderIndicators,
        string recommendedCandidate,
        SmokeTestReadinessAssessment readiness,
        IReadOnlyList<DiscoveryIssue> evidenceIssues)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Thronefall Runtime Compatibility Report");
        builder.AppendLine();
        AppendValue(builder, "Base game fingerprint", request.BaseFingerprint);
        AppendValue(builder, "Inspection-tool version", DiscoveryEngine.DiscoveryToolVersion);
        AppendValue(builder, "Inspection timestamp UTC", (request.DiscoveryTimestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        AppendValue(builder, "Backend evidence reference", $"Base fingerprint {request.BaseFingerprint}; no backend assumption is generalized beyond that fingerprint.");
        builder.AppendLine("## Executable architecture");
        builder.AppendLine();
        builder.AppendLine(executableArchitecture.ToString());
        builder.Append("Selected executable: ").AppendLine(selectedExecutableRelativePath ?? "Unknown");
        builder.AppendLine();
        AppendValue(builder, "Managed-runtime profile", managedRuntimeProfile.ToString());
        builder.AppendLine("## Managed-runtime layout evidence");
        builder.AppendLine();
        foreach (var item in runtimeLayoutEvidence)
        {
            builder.Append("- ").Append(item.RelativePath).Append(" | ")
                .Append(item.IsDirectory ? "directory" : "file").Append(" | ")
                .Append(item.Present ? "Present" : "Absent").Append(" | ")
                .AppendLine(item.Description);
        }

        builder.AppendLine();
        builder.AppendLine("## Detected framework assemblies");
        builder.AppendLine();
        foreach (var assembly in managedAssemblies)
        {
            builder.Append("- ").Append(assembly.RelativePath)
                .Append(" | managed metadata: ").Append(assembly.HasManagedMetadata ? "yes" : "no")
                .Append(" | assembly: ").Append(assembly.AssemblyName ?? "Unknown")
                .Append(" | version: ").Append(assembly.AssemblyVersion?.ToString() ?? "Unknown")
                .Append(" | target framework: ").AppendLine(assembly.TargetFramework ?? "Unknown");
            foreach (var reference in assembly.SelectedFrameworkReferences)
            {
                builder.Append("  - framework reference: ").Append(reference.Name)
                    .Append(' ').AppendLine(reference.Version?.ToString() ?? "Unknown");
            }
        }

        if (managedAssemblies.Length == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        AppendValue(builder, "Target-framework recommendation", targetFrameworkAssessment.Recommendation.ToString());
        AppendValue(builder, "Target-framework confidence", targetFrameworkAssessment.Confidence.ToString());
        AppendValue(builder, "Target-framework evidence basis", targetFrameworkAssessment.Basis);
        AppendValue(builder, "Unity-version evidence", unityVersionEvidence.Version);
        builder.AppendLine("## Unity-version evidence sources");
        builder.AppendLine();
        foreach (var item in unityVersionEvidence.Evidence)
        {
            builder.Append("- ").Append(item.Source).Append(" | ").Append(item.RelativePath)
                .Append(" | ").AppendLine(item.Version);
        }

        if (unityVersionEvidence.Evidence.Count == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        builder.AppendLine("## Existing bootstrap and loader indicators");
        builder.AppendLine();
        foreach (var indicator in loaderIndicators.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- ").Append(indicator.Name).Append(" | ").Append(indicator.Status)
                .Append(" | ").AppendLine(indicator.Explanation);
        }

        builder.AppendLine();
        builder.AppendLine("## Official BepInEx candidate matrix");
        builder.AppendLine();
        builder.AppendLine("| Candidate | Version | Status | Backend | Architecture | Likely TFM | Stability | Suitability | Known uncertainty |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var candidate in OfficialCandidates)
        {
            builder.Append("| ").Append(candidate.Product).Append(" | ").Append(candidate.Version)
                .Append(" | ").Append(candidate.OfficialStatus).Append(" | ").Append(candidate.BackendMatch)
                .Append(" | ").Append(candidate.ArchitectureMatch).Append(" | ").Append(candidate.LikelyTargetFramework)
                .Append(" | ").Append(candidate.Stability).Append(" | ").Append(candidate.Suitability)
                .Append(" | ").Append(candidate.KnownUncertainty).AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("Official sources and retrieval dates:");
        foreach (var candidate in OfficialCandidates)
        {
            var sourceTitle = candidate.Product.StartsWith("BepInEx ", StringComparison.Ordinal)
                ? "Official BepInEx release notes"
                : candidate.SourceTitle;
            builder.Append("- ").Append(candidate.Product).Append(" | ")
                .Append(sourceTitle).Append(" | ").Append(candidate.SourceUrl)
                .Append(" | retrieved ").AppendLine(candidate.RetrievedDateUtc);
        }

        builder.AppendLine();
        AppendValue(builder, "Recommended candidate for a future smoke test", recommendedCandidate);
        builder.AppendLine("## Current clean-profile smoke-test readiness");
        builder.AppendLine();
        builder.Append("- Status: ").AppendLine(readiness.Status.ToString());
        builder.Append("- Blocking indicators: ").AppendLine(readiness.BlockingIndicators.Count == 0
            ? "None"
            : string.Join(", ", readiness.BlockingIndicators));
        builder.Append("- Explanation: ").AppendLine(readiness.Explanation);
        builder.Append("- Remediation required: ").AppendLine(readiness.Remediation);
        builder.AppendLine("- No automatic cleanup was performed.");
        builder.AppendLine();
        builder.AppendLine("## Reasons for the recommendation");
        builder.AppendLine();
        builder.AppendLine("- The recommendation is provisional and combines only the local backend, executable architecture, and bounded framework evidence.");
        builder.AppendLine("- The official BepInEx 5 Unity Mono x64 distribution is preferred over the pre-release BepInEx 6 candidate for a later clean-profile experiment.");
        builder.AppendLine("- No loader was downloaded, installed, executed, or selected as production-compatible by this report.");
        builder.AppendLine();
        AppendEvidenceLists(builder, evidenceIssues);
        builder.AppendLine("## Security and privacy statement");
        builder.AppendLine();
        builder.AppendLine("- Only selected compatibility metadata below the explicit game root was inspected; no assembly was loaded and no method or private type was examined.");
        builder.AppendLine("- The report contains relative paths, assembly identities, selected framework references, loader indicator names, official release identifiers, and conclusions only.");
        builder.AppendLine("- Absolute paths, usernames, machine names, Steam account data, arbitrary listings, binary contents, secrets, and decompiled source are excluded.");
        builder.AppendLine();
        builder.AppendLine("## Next required experiment");
        builder.AppendLine();
        builder.AppendLine("- Run a reversible clean-profile loader smoke test for the provisional candidate only after this report is reviewed. Do not infer lifecycle hooks, game APIs, Harmony compatibility, or plugin load success from metadata alone.");
        return builder.ToString();
    }

    private static void AppendValue(StringBuilder builder, string title, string value)
    {
        builder.Append("## ").AppendLine(title);
        builder.AppendLine();
        builder.AppendLine(value);
        builder.AppendLine();
    }

    private static void AppendEvidenceLists(StringBuilder builder, IReadOnlyList<DiscoveryIssue> evidence)
    {
        builder.AppendLine("## Conflicting evidence");
        builder.AppendLine();
        var conflicts = evidence
            .Where(item => item.Category == DiscoveryIssueCategory.Conflict)
            .Select(item => item.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var item in conflicts)
        {
            builder.Append("- ").AppendLine(item);
        }

        if (conflicts.Length == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        builder.AppendLine("## Missing evidence");
        builder.AppendLine();
        var missing = evidence
            .Where(item => item.Category == DiscoveryIssueCategory.Missing)
            .Select(item => item.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var item in missing)
        {
            builder.Append("- ").AppendLine(item);
        }

        if (missing.Length == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();

        builder.AppendLine("## Inspection limitations");
        builder.AppendLine();
        var limitations = evidence
            .Where(item => item.Category == DiscoveryIssueCategory.Limitation)
            .Select(item => item.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var item in limitations)
        {
            builder.Append("- ").AppendLine(item);
        }

        if (limitations.Length == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();

        builder.AppendLine("## Warnings");
        builder.AppendLine();
        var warnings = evidence
            .Where(item => item.Category == DiscoveryIssueCategory.Warning)
            .Select(item => item.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var item in warnings)
        {
            builder.Append("- ").AppendLine(item);
        }

        if (warnings.Length == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
    }

    private static DiscoveryIssue CategorizeIssue(string message)
    {
        var category = message.Contains("conflict", StringComparison.OrdinalIgnoreCase)
            || message.Contains("multiple", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ambiguous", StringComparison.OrdinalIgnoreCase)
                ? DiscoveryIssueCategory.Conflict
                : message.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("no ", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("unknown", StringComparison.OrdinalIgnoreCase)
                        ? DiscoveryIssueCategory.Missing
                        : message.Contains("limit", StringComparison.OrdinalIgnoreCase)
                            || message.Contains("omitted", StringComparison.OrdinalIgnoreCase)
                                ? DiscoveryIssueCategory.Limitation
                                : DiscoveryIssueCategory.Warning;
        return new DiscoveryIssue(category, message);
    }

}
