namespace ThroneForge.LoaderSmokeTest;

public static class SmokeTestPathValidator
{
    public static SmokeTestRoots ValidateRoots(
        string repositoryRoot,
        string originalGameRoot,
        string experimentRoot)
    {
        var repository = ValidateExistingDirectory(repositoryRoot, "repository");
        var game = ValidateExistingDirectory(originalGameRoot, "game installation");
        var experiment = CanonicalizeAbsolute(experimentRoot, "experiment root");

        EnsureNoReparseOnExistingPath(repository);
        EnsureNoReparseOnExistingPath(game);
        EnsureNoReparseOnExistingPath(experiment);

        if (IsSameOrDescendant(repository, experiment) || IsSameOrDescendant(game, experiment))
        {
            throw new SmokeTestException("The experiment root must be outside the repository and original game installation.");
        }

        if (File.Exists(experiment))
        {
            throw new SmokeTestException("The experiment root must identify a directory, not a file.");
        }

        var roots = new SmokeTestRoots(
            repository,
            game,
            experiment,
            Path.Combine(experiment, "clean-game"),
            Path.Combine(experiment, "downloads"),
            Path.Combine(experiment, "extracted-loader"),
            Path.Combine(experiment, "evidence"),
            Path.Combine(experiment, "manifests"),
            Path.Combine(experiment, "manifests", "backup"));

        foreach (var derived in new[]
                 {
                     roots.CleanGameRoot,
                     roots.DownloadsRoot,
                     roots.ExtractedLoaderRoot,
                     roots.EvidenceRoot,
                     roots.ManifestsRoot,
                     roots.BackupRoot
                 })
        {
            EnsureNoReparseOnExistingPath(derived);
        }

        return roots;
    }

    public static string EnsureWithin(string root, string candidate)
    {
        var normalizedRoot = CanonicalizeAbsolute(root, "root");
        var normalizedCandidate = CanonicalizeAbsolute(candidate, "candidate");
        if (!IsStrictDescendant(normalizedRoot, normalizedCandidate))
        {
            throw new SmokeTestException("A requested path escapes its validated root.");
        }

        return normalizedCandidate;
    }

    public static string ValidateCleanupTarget(SmokeTestRoots roots, string target)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var normalized = CanonicalizeAbsolute(target, "cleanup target");
        if (!IsSameOrDescendant(roots.ExperimentRoot, normalized))
        {
            throw new SmokeTestException("Cleanup is limited to the validated experiment root.");
        }

        EnsureNoReparseOnExistingPath(normalized);
        return normalized;
    }

    public static string ValidateExecutablePath(string executablePath, string cleanGameRoot, string experimentRoot)
    {
        var executable = CanonicalizeAbsolute(executablePath, "executable");
        if (!File.Exists(executable))
        {
            throw new SmokeTestException("The requested experiment executable does not exist.");
        }

        if (!IsStrictDescendant(cleanGameRoot, executable)
            || !IsSameOrDescendant(experimentRoot, executable))
        {
            throw new SmokeTestException("The experiment executable is outside the disposable profile.");
        }

        EnsureNoReparseOnExistingPath(executable);
        return executable;
    }

    public static string ValidateCommittedReportPath(SmokeTestRoots roots, string expectedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (string.IsNullOrWhiteSpace(expectedFingerprint)
            || expectedFingerprint.Length != 64
            || expectedFingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new SmokeTestException("The expected fingerprint must be a 64-character SHA-256 value.");
        }

        var discoveryRoot = Path.Combine(roots.RepositoryRoot, "docs", "discovery");
        var reportPath = Path.Combine(
            discoveryRoot,
            $"{expectedFingerprint.ToLowerInvariant()}-loader-smoke-test.md");
        var normalized = CanonicalizeAbsolute(reportPath, "committed report");
        if (!IsStrictDescendant(roots.RepositoryRoot, normalized)
            || !IsStrictDescendant(discoveryRoot, normalized)
            || IsSameOrDescendant(roots.OriginalGameRoot, normalized)
            || IsSameOrDescendant(roots.CleanGameRoot, normalized))
        {
            throw new SmokeTestException("The committed report must remain below the repository docs/discovery directory and outside both game profiles.");
        }

        EnsureNoReparseOnExistingPath(normalized);
        return normalized;
    }

    internal static string ValidateNewDirectoryPath(string path)
    {
        var normalized = CanonicalizeAbsolute(path, "target directory");
        if (File.Exists(normalized))
        {
            throw new SmokeTestException("A target directory path is occupied by a file.");
        }

        EnsureNoReparseOnExistingPath(normalized);
        return normalized;
    }

    internal static void EnsureExistingTreeHasNoReparsePoints(string root)
    {
        var normalized = ValidateExistingDirectory(root, "disposable profile");
        EnsureNoReparseOnExistingPath(normalized);
        try
        {
            var pending = new Stack<string>();
            pending.Push(normalized);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new SmokeTestException("The disposable profile contains a symbolic link or reparse point.");
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(entry);
                    }
                }
            }
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new SmokeTestException("The disposable profile could not be inspected safely.", exception);
        }
    }

    private static string ValidateExistingDirectory(string path, string description)
    {
        var normalized = CanonicalizeAbsolute(path, description);
        if (!Directory.Exists(normalized))
        {
            throw new SmokeTestException($"The explicit {description} path must identify an existing directory.");
        }

        return normalized;
    }

    private static string CanonicalizeAbsolute(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            throw new SmokeTestException($"The explicit {description} path must be absolute.");
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new SmokeTestException($"The explicit {description} path is not valid.", exception);
        }
    }

    private static void EnsureNoReparseOnExistingPath(string path)
    {
        FileSystemInfo current = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : File.Exists(path)
                ? new FileInfo(path)
                : new DirectoryInfo(path);

        while (current is not null)
        {
            if (Directory.Exists(current.FullName) || File.Exists(current.FullName))
            {
                try
                {
                    if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new SmokeTestException("The experiment path must not use symbolic links or reparse points.");
                    }
                }
                catch (SmokeTestException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    throw new SmokeTestException("An experiment path parent could not be inspected safely.", exception);
                }
            }

            current = current switch
            {
                DirectoryInfo directory => directory.Parent!,
                FileInfo file => file.Directory!,
                _ => null!
            };
        }
    }

    private static bool IsSameOrDescendant(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate);
        return string.Equals(normalizedRoot, normalizedCandidate, comparison)
            || normalizedCandidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                comparison);
    }

    private static bool IsStrictDescendant(string root, string candidate)
        => !string.Equals(
                Path.TrimEndingDirectorySeparator(root),
                Path.TrimEndingDirectorySeparator(candidate),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && IsSameOrDescendant(root, candidate);
}
