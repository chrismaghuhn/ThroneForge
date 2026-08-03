using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ThroneForge.Discovery;

internal sealed record UnityVersionScanResult(
    string Version,
    IReadOnlyList<UnityVersionEvidence> Evidence,
    IReadOnlyList<string> Explanations);

internal static class UnityVersionEvidenceReader
{
    private const int MaximumTextFileBytes = 64 * 1024;
    private const int MaximumGlobalManagersPrefixBytes = 256 * 1024;
    private static readonly Regex UnityVersionPattern = new(
        @"\b20\d{2}\.\d+\.\d+(?:(?:[a-z]\d+(?:c\d+)?)|(?:\.\d+))\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static UnityVersionScanResult Read(
        DirectoryInfo gameRoot,
        IReadOnlyList<string> dataDirectories,
        string? executableRelativePath,
        Func<string, string?> versionResourceReader)
    {
        ArgumentNullException.ThrowIfNull(gameRoot);
        ArgumentNullException.ThrowIfNull(dataDirectories);
        ArgumentNullException.ThrowIfNull(versionResourceReader);

        var evidence = new List<UnityVersionEvidence>();
        var explanations = new List<string>();
        foreach (var dataDirectory in dataDirectories.Order(StringComparer.OrdinalIgnoreCase))
        {
            ReadBoundedTextCandidate(
                gameRoot,
                CombineRelative(dataDirectory, "UnityVersion.txt"),
                "UnityVersion.txt",
                MaximumTextFileBytes,
                evidence,
                explanations);
            ReadBoundedTextCandidate(
                gameRoot,
                CombineRelative(dataDirectory, "globalgamemanagers"),
                "globalgamemanagers",
                MaximumGlobalManagersPrefixBytes,
                evidence,
                explanations);
        }

        ReadVersionResourceCandidate(
            gameRoot,
            executableRelativePath,
            "executable version resource",
            versionResourceReader,
            evidence,
            explanations);
        ReadVersionResourceCandidate(
            gameRoot,
            "UnityPlayer.dll",
            "UnityPlayer.dll version resource",
            versionResourceReader,
            evidence,
            explanations);

        var versionIdentities = evidence
            .Select(item => NormalizeVersionIdentity(item.Version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var version = versionIdentities.Length switch
        {
            0 => "Unknown",
            1 => evidence[0].Version,
            _ => "Conflicting"
        };
        if (versionIdentities.Length == 0)
        {
            explanations.Add("Unity version: Unknown; no bounded local version evidence was found.");
        }
        else if (versionIdentities.Length > 1)
        {
            explanations.Add("Conflicting Unity version evidence was found across bounded local sources.");
        }

        return new UnityVersionScanResult(version, evidence, explanations);
    }

    private static void ReadBoundedTextCandidate(
        DirectoryInfo gameRoot,
        string relativePath,
        string source,
        int maximumBytes,
        List<UnityVersionEvidence> evidence,
        List<string> explanations)
    {
        if (!DiscoveryPathValidator.TryResolveReadFile(gameRoot, relativePath, out var fullPath))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);
            var bytesToRead = (int)Math.Min(stream.Length, maximumBytes);
            var bytes = new byte[bytesToRead];
            var read = 0;
            while (read < bytes.Length)
            {
                var current = stream.Read(bytes, read, bytes.Length - read);
                if (current == 0)
                {
                    break;
                }

                read += current;
            }

            if (stream.Length > maximumBytes)
            {
                explanations.Add($"Unity version evidence '{relativePath}' exceeded the bounded read limit; only the prefix was inspected.");
            }

            var content = Encoding.UTF8.GetString(bytes, 0, read);
            AddVersionEvidence(relativePath, source, content, evidence);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            explanations.Add($"Unity version evidence '{relativePath}' could not be read.");
        }
    }

    private static void ReadVersionResourceCandidate(
        DirectoryInfo gameRoot,
        string? relativePath,
        string source,
        Func<string, string?> versionResourceReader,
        List<UnityVersionEvidence> evidence,
        List<string> explanations)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || !DiscoveryPathValidator.TryResolveReadFile(gameRoot, relativePath, out var fullPath))
        {
            return;
        }

        try
        {
            var version = versionResourceReader(fullPath);
            if (!string.IsNullOrWhiteSpace(version))
            {
                AddVersionEvidence(relativePath, source, version, evidence);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            explanations.Add($"Version-resource evidence '{relativePath}' could not be read.");
        }
    }

    private static void AddVersionEvidence(
        string relativePath,
        string source,
        string content,
        List<UnityVersionEvidence> evidence)
    {
        var match = UnityVersionPattern.Match(content);
        if (match.Success)
        {
            evidence.Add(new UnityVersionEvidence(
                source,
                relativePath,
                match.Value,
                "Version extracted from bounded local metadata."));
        }
    }

    private static string CombineRelative(string directory, string fileName)
        => string.IsNullOrEmpty(directory) ? fileName : $"{directory}/{fileName}";

    private static string NormalizeVersionIdentity(string version)
    {
        var match = Regex.Match(
            version,
            @"^(?<major>20\d{2})\.(?<minor>\d+)\.(?<patch>\d+)",
            RegexOptions.CultureInvariant);
        return match.Success
            ? $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}.{match.Groups["patch"].Value}"
            : version;
    }

    public static string? ReadFileVersionResource(string path)
        => FileVersionInfo.GetVersionInfo(path).FileVersion;
}
