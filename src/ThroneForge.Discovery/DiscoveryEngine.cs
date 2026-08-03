using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ThroneForge.Discovery;

public sealed class DiscoveryEngine
{
    public const string DiscoveryToolVersion = "0.1.0";
    public const string FingerprintAlgorithmVersion = "throneforge-game-fingerprint-v1";

    private const long MaximumSelectedFileBytes = 64 * 1024 * 1024;
    private const int MaximumUnityVersionBytes = 64 * 1024;
    private static readonly Regex UnityVersionPattern = new(
        @"\b20\d{2}\.\d+\.\d+[a-z]\d+(?:c\d+)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "The instance-shaped API keeps the discovery engine extensible without introducing mutable state.")]
    public DiscoveryResult Inspect(DiscoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var gameRoot = ValidateGameRoot(request.GamePath);
        var files = EnumerateFiles(gameRoot);
        var directories = EnumerateDirectories(gameRoot);
        var fileMap = files.ToDictionary(item => item.RelativePath, StringComparer.OrdinalIgnoreCase);
        var directorySet = new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase);

        var detectedEvidence = new List<EvidenceItem>();
        var missingOrConflictingEvidence = new List<string>();
        var (backend, monoCount, il2CppCount) = DetectBackend(fileMap, directorySet, detectedEvidence);
        AddBackendExplanation(backend, monoCount, il2CppCount, missingOrConflictingEvidence);

        var executable = SelectMainExecutable(fileMap, gameRoot);
        var architectureProbe = executable
            ?? fileMap.GetValueOrDefault("GameAssembly.dll");
        var executableArchitecture = architectureProbe is null
            ? ExecutableArchitecture.Unknown
            : ReadExecutableArchitecture(architectureProbe, detectedEvidence, missingOrConflictingEvidence);

        var unityVersion = DetectUnityVersion(fileMap, detectedEvidence, missingOrConflictingEvidence);
        var selectedFiles = SelectCompatibilityFiles(fileMap, executable, detectedEvidence, missingOrConflictingEvidence);
        var fingerprint = CreateFingerprint(backend, executableArchitecture, unityVersion, selectedFiles);

        var outputRoot = Path.GetFullPath(request.OutputRoot);
        var reportMarkdown = BuildReport(
            fingerprint,
            request.DiscoveryTimestampUtc ?? DateTimeOffset.UtcNow,
            backend,
            executableArchitecture,
            unityVersion,
            detectedEvidence,
            missingOrConflictingEvidence,
            selectedFiles);
        var reportPath = DiscoveryReportWriter.Write(
            outputRoot,
            fingerprint,
            reportMarkdown,
            request.OverwriteExisting);

        return new DiscoveryResult(
            fingerprint,
            DiscoveryToolVersion,
            FingerprintAlgorithmVersion,
            backend,
            executableArchitecture,
            unityVersion,
            detectedEvidence,
            missingOrConflictingEvidence,
            selectedFiles,
            reportPath,
            reportMarkdown);
    }

    private static DirectoryInfo ValidateGameRoot(string? suppliedPath)
    {
        if (string.IsNullOrWhiteSpace(suppliedPath) || !Path.IsPathRooted(suppliedPath))
        {
            throw new DiscoveryException("--game-path must be an explicit absolute path to an existing directory.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(suppliedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            throw new DiscoveryException("--game-path is not a valid accessible path.", exception);
        }

        if (!Directory.Exists(fullPath))
        {
            if (File.Exists(fullPath))
            {
                throw new DiscoveryException("--game-path must point to a directory, not a file.");
            }

            throw new DiscoveryException("The directory supplied by --game-path does not exist.");
        }

        try
        {
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new DiscoveryException("--game-path must not be a symbolic link or reparse point.");
            }

            return new DirectoryInfo(fullPath);
        }
        catch (DiscoveryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DiscoveryException("The directory supplied by --game-path is not accessible.", exception);
        }
    }

    private static List<FileEntry> EnumerateFiles(DirectoryInfo root)
    {
        var files = new List<FileEntry>();
        foreach (var entry in EnumerateEntries(root))
        {
            if (entry.Info is FileInfo file)
            {
                files.Add(new FileEntry(ToRelativePath(root.FullName, file.FullName), file.FullName));
            }
        }

        return files;
    }

    private static List<string> EnumerateDirectories(DirectoryInfo root)
    {
        var directories = new List<string>();
        foreach (var entry in EnumerateEntries(root))
        {
            if (entry.Info is DirectoryInfo directory)
            {
                directories.Add(ToRelativePath(root.FullName, directory.FullName));
            }
        }

        return directories;
    }

    private static IEnumerable<Entry> EnumerateEntries(DirectoryInfo root)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = current.EnumerateFileSystemInfos(
                    "*",
                    new EnumerationOptions
                    {
                        IgnoreInaccessible = false,
                        RecurseSubdirectories = false,
                        ReturnSpecialDirectories = false,
                        AttributesToSkip = 0
                    });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new DiscoveryException(
                    $"The installation could not be read beneath '{current.Name}'. Check permissions and try again.",
                    exception);
            }

            IEnumerator<FileSystemInfo> enumerator;
            try
            {
                enumerator = entries.GetEnumerator();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new DiscoveryException("The installation could not be enumerated.", exception);
            }

            using (enumerator)
            {
                while (true)
                {
                    FileSystemInfo entry;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }

                        entry = enumerator.Current;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        throw new DiscoveryException("The installation could not be enumerated.", exception);
                    }

                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo directory)
                    {
                        pending.Push(directory);
                    }

                    yield return new Entry(entry);
                }
            }
        }
    }

    private static (BackendClassification Backend, int MonoCount, int Il2CppCount) DetectBackend(
        IReadOnlyDictionary<string, FileEntry> files,
        IReadOnlySet<string> directories,
        List<EvidenceItem> evidence)
    {
        var monoSignals = new List<(string Path, string Description)>();
        var il2CppSignals = new List<(string Path, string Description)>();

        foreach (var dataDirectory in directories
                     .Where(path => IsTopLevelDataDirectory(path))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            AddDirectorySignal(directories, monoSignals, dataDirectory + "/Managed", "Managed data directory");
            AddDirectorySignal(directories, monoSignals, dataDirectory + "/MonoBleedingEdge", "Mono runtime directory");
            AddDirectorySignal(directories, monoSignals, dataDirectory + "/mono", "Mono runtime directory");
            AddFileSignal(files, monoSignals, dataDirectory + "/Managed/Assembly-CSharp.dll", "Managed game assembly");
            AddDirectorySignal(directories, il2CppSignals, dataDirectory + "/il2cpp_data", "IL2CPP data directory");
        }

        AddDirectorySignal(directories, monoSignals, "MonoBleedingEdge", "Mono runtime directory");
        AddDirectorySignal(directories, monoSignals, "mono", "Mono runtime directory");
        AddFileSignal(files, il2CppSignals, "GameAssembly.dll", "IL2CPP native runtime");

        foreach (var file in files.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (file.EndsWith("/global-metadata.dat", StringComparison.OrdinalIgnoreCase)
                && file.Contains("/il2cpp_data/", StringComparison.OrdinalIgnoreCase))
            {
                il2CppSignals.Add((file, "IL2CPP global metadata"));
            }
        }

        foreach (var signal in monoSignals.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            evidence.Add(new EvidenceItem("Mono backend", signal.Path, signal.Description));
        }

        foreach (var signal in il2CppSignals.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            evidence.Add(new EvidenceItem("IL2CPP backend", signal.Path, signal.Description));
        }

        var monoCount = monoSignals.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var il2CppCount = il2CppSignals.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var backend = monoCount >= 2 && il2CppCount >= 2
            ? BackendClassification.Ambiguous
            : monoCount >= 2
                ? BackendClassification.Mono
                : il2CppCount >= 2
                    ? BackendClassification.IL2CPP
                    : BackendClassification.Unknown;

        if (monoCount > 0 && il2CppCount > 0 && backend != BackendClassification.Ambiguous)
        {
            backend = BackendClassification.Ambiguous;
        }

        return (backend, monoCount, il2CppCount);
    }

    private static void AddBackendExplanation(
        BackendClassification backend,
        int monoCount,
        int il2CppCount,
        List<string> explanations)
    {
        if (backend == BackendClassification.Ambiguous)
        {
            explanations.Add("Conflicting Mono and IL2CPP evidence was detected; no backend was selected.");
        }
        else if (backend == BackendClassification.Unknown)
        {
            explanations.Add(
                $"Insufficient compatible backend evidence: Mono signals={monoCount}, IL2CPP signals={il2CppCount}; "
                + "at least two compatible signals are required.");
        }
    }

    private static FileEntry? SelectMainExecutable(IReadOnlyDictionary<string, FileEntry> files, DirectoryInfo root)
    {
        var executables = files.Values
            .Where(file => !file.RelativePath.Contains('/')
                && file.RelativePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (executables.Count == 0)
        {
            return null;
        }

        var preferredName = root.Name + ".exe";
        return executables.FirstOrDefault(file => file.RelativePath.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
            ?? executables[0];
    }

    private static ExecutableArchitecture ReadExecutableArchitecture(
        FileEntry executable,
        List<EvidenceItem> evidence,
        List<string> explanations)
    {
        if (!PeArchitectureReader.TryRead(executable.FullPath, out var architecture))
        {
            explanations.Add($"The selected executable '{executable.RelativePath}' has a missing or malformed PE header.");
            return ExecutableArchitecture.Unknown;
        }

        evidence.Add(new EvidenceItem("Executable architecture", executable.RelativePath, $"PE machine maps to {architecture}."));
        return architecture;
    }

    private static string DetectUnityVersion(
        IReadOnlyDictionary<string, FileEntry> files,
        List<EvidenceItem> evidence,
        List<string> explanations)
    {
        var candidates = files.Values
            .Where(file => file.RelativePath.EndsWith("/UnityVersion.txt", StringComparison.OrdinalIgnoreCase)
                || file.RelativePath.Equals("UnityVersion.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in candidates)
        {
            try
            {
                if (new FileInfo(candidate.FullPath).Length > MaximumUnityVersionBytes)
                {
                    explanations.Add($"Unity version evidence '{candidate.RelativePath}' exceeded the read limit.");
                    continue;
                }

                var content = File.ReadAllText(candidate.FullPath, Encoding.UTF8);
                var match = UnityVersionPattern.Match(content);
                if (match.Success)
                {
                    evidence.Add(new EvidenceItem("Unity version", candidate.RelativePath, "Version read from local file evidence."));
                    return match.Value;
                }
            }
            catch (IOException)
            {
                explanations.Add($"Unity version evidence '{candidate.RelativePath}' could not be read.");
            }
            catch (UnauthorizedAccessException)
            {
                explanations.Add($"Unity version evidence '{candidate.RelativePath}' could not be read.");
            }
        }

        explanations.Add("Unity version: Unknown; no stable local version evidence was found.");
        return "Unknown";
    }

    private static List<SelectedFileEvidence> SelectCompatibilityFiles(
        IReadOnlyDictionary<string, FileEntry> files,
        FileEntry? executable,
        List<EvidenceItem> evidence,
        List<string> explanations)
    {
        var candidates = new List<FileEntry>();
        if (executable is not null)
        {
            candidates.Add(executable);
        }

        candidates.AddRange(files.Values.Where(file => file.RelativePath.EndsWith("/Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase)));
        candidates.AddRange(files.Values.Where(file => file.RelativePath.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase)));
        candidates.AddRange(files.Values.Where(file => file.RelativePath.EndsWith("/global-metadata.dat", StringComparison.OrdinalIgnoreCase)));
        candidates.AddRange(files.Values.Where(file => file.RelativePath.EndsWith("/UnityVersion.txt", StringComparison.OrdinalIgnoreCase)
            || file.RelativePath.Equals("UnityVersion.txt", StringComparison.OrdinalIgnoreCase)));

        var selected = new List<SelectedFileEvidence>();
        foreach (var candidate in candidates
                     .DistinctBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(candidate.FullPath);
                if (info.Length > MaximumSelectedFileBytes)
                {
                    explanations.Add($"Selected file '{candidate.RelativePath}' exceeded the 64 MiB read limit and was omitted.");
                    continue;
                }

                var hash = ComputeSha256(candidate.FullPath);
                selected.Add(new SelectedFileEvidence(candidate.RelativePath, info.Length, hash));
                evidence.Add(new EvidenceItem("Selected compatibility file", candidate.RelativePath, "Included in fingerprint input."));
            }
            catch (IOException)
            {
                explanations.Add($"Selected file '{candidate.RelativePath}' could not be read.");
            }
            catch (UnauthorizedAccessException)
            {
                explanations.Add($"Selected file '{candidate.RelativePath}' could not be read.");
            }
        }

        return selected;
    }

    private static string CreateFingerprint(
        BackendClassification backend,
        ExecutableArchitecture architecture,
        string unityVersion,
        IEnumerable<SelectedFileEvidence> selectedFiles)
    {
        var builder = new StringBuilder()
            .Append(FingerprintAlgorithmVersion).Append('\n')
            .Append("backend=").Append(backend).Append('\n')
            .Append("architecture=").Append(architecture).Append('\n')
            .Append("unity=").Append(unityVersion).Append('\n');

        foreach (var file in selectedFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            builder.Append("file=")
                .Append(file.RelativePath)
                .Append('|')
                .Append(file.Size.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(file.Sha256)
                .Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string BuildReport(
        string fingerprint,
        DateTimeOffset discoveryTimestampUtc,
        BackendClassification backend,
        ExecutableArchitecture architecture,
        string unityVersion,
        IEnumerable<EvidenceItem> detectedEvidence,
        IEnumerable<string> missingOrConflictingEvidence,
        IEnumerable<SelectedFileEvidence> selectedFiles)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Thronefall Discovery Report");
        builder.AppendLine();
        builder.AppendLine("## Fingerprint");
        builder.AppendLine();
        builder.AppendLine(fingerprint);
        builder.AppendLine();
        builder.AppendLine("## Discovery tool version");
        builder.AppendLine();
        builder.AppendLine(DiscoveryToolVersion);
        builder.AppendLine();
        builder.AppendLine("## Fingerprint algorithm version");
        builder.AppendLine();
        builder.AppendLine(FingerprintAlgorithmVersion);
        builder.AppendLine();
        builder.AppendLine("## Discovery timestamp in UTC");
        builder.AppendLine();
        builder.AppendLine(discoveryTimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("## Backend classification");
        builder.AppendLine();
        builder.AppendLine(backend.ToString());
        builder.AppendLine();
        builder.AppendLine("## Executable architecture");
        builder.AppendLine();
        builder.AppendLine(architecture.ToString());
        builder.AppendLine();
        builder.AppendLine("## Unity-version evidence");
        builder.AppendLine();
        builder.AppendLine(unityVersion);
        builder.AppendLine();
        AppendEvidenceSection(builder, "Detected evidence", detectedEvidence);
        AppendStringSection(builder, "Missing or conflicting evidence", missingOrConflictingEvidence);
        builder.AppendLine("## Relevant files using relative paths only");
        builder.AppendLine();
        foreach (var file in selectedFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            builder.Append("- ").AppendLine(file.RelativePath);
        }

        if (!selectedFiles.Any())
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        builder.AppendLine("## Selected file sizes and SHA-256 values");
        builder.AppendLine();
        foreach (var file in selectedFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            builder.Append("- ").Append(file.RelativePath)
                .Append(" | ").Append(file.Size.ToString(CultureInfo.InvariantCulture))
                .Append(" bytes | SHA-256 ").AppendLine(file.Sha256);
        }

        if (!selectedFiles.Any())
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        builder.AppendLine("## Compatibility conclusions");
        builder.AppendLine();
        builder.Append("- Backend classification: ").Append(backend).AppendLine(".");
        builder.Append("- Executable architecture: ").Append(architecture).AppendLine(".");
        builder.Append("- Unity version evidence: ").Append(unityVersion).AppendLine(".");
        builder.AppendLine("- This report records local evidence only and does not establish runtime compatibility.");
        builder.AppendLine();
        builder.AppendLine("## Unverified assumptions");
        builder.AppendLine();
        builder.AppendLine("- File and directory names are layout indicators, not proof of internal game behavior.");
        builder.AppendLine("- No loader, plugin binding, lifecycle behavior, or game API compatibility was tested.");
        builder.AppendLine();
        builder.AppendLine("## Recommended next investigation");
        builder.AppendLine();
        builder.AppendLine("- Review the locally evidenced runtime layout before selecting adapter targets or APIs.");
        builder.AppendLine();
        builder.AppendLine("## Privacy and sanitization statement");
        builder.AppendLine();
        builder.AppendLine("- The report contains relative paths, selected file sizes, selected SHA-256 values, and local evidence labels only.");
        builder.AppendLine("- The absolute installation path, usernames, machine names, timestamps used for fingerprinting, and arbitrary directory listings are excluded from fingerprint input.");
        return builder.ToString();
    }

    private static void AppendEvidenceSection(StringBuilder builder, string title, IEnumerable<EvidenceItem> evidence)
    {
        builder.Append("## ").AppendLine(title);
        builder.AppendLine();
        var items = evidence.OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToList();
        foreach (var item in items)
        {
            builder.Append("- ").Append(item.Category).Append(": ")
                .Append(item.RelativePath).Append(" — ").AppendLine(item.Description);
        }

        if (items.Count == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
    }

    private static void AppendStringSection(StringBuilder builder, string title, IEnumerable<string> values)
    {
        builder.Append("## ").AppendLine(title);
        builder.AppendLine();
        var items = values.Distinct(StringComparer.Ordinal).ToList();
        foreach (var item in items)
        {
            builder.Append("- ").AppendLine(item);
        }

        if (items.Count == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
    }

    private static bool IsTopLevelDataDirectory(string path)
        => !path.Contains('/') && path.EndsWith("_Data", StringComparison.OrdinalIgnoreCase);

    private static void AddDirectorySignal(
        IReadOnlySet<string> directories,
        List<(string Path, string Description)> signals,
        string path,
        string description)
    {
        if (directories.Contains(path))
        {
            signals.Add((path, description));
        }
    }

    private static void AddFileSignal(
        IReadOnlyDictionary<string, FileEntry> files,
        List<(string Path, string Description)> signals,
        string path,
        string description)
    {
        if (files.ContainsKey(path))
        {
            signals.Add((path, description));
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToRelativePath(string root, string fullPath)
        => Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    private sealed record FileEntry(string RelativePath, string FullPath);

    private sealed record Entry(FileSystemInfo Info);
}
