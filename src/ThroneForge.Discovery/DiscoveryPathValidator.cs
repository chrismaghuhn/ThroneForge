namespace ThroneForge.Discovery;

internal static class DiscoveryPathValidator
{
    public static DirectoryInfo ValidateGameRoot(string? suppliedPath)
    {
        if (string.IsNullOrWhiteSpace(suppliedPath) || !Path.IsPathRooted(suppliedPath))
        {
            throw new DiscoveryException("--game-path must be an explicit absolute path to an existing directory.");
        }

        var fullPath = GetFullPath(suppliedPath, "--game-path");
        if (!Directory.Exists(fullPath))
        {
            if (File.Exists(fullPath))
            {
                throw new DiscoveryException("--game-path must point to a directory, not a file.");
            }

            throw new DiscoveryException("The directory supplied by --game-path does not exist.");
        }

        EnsureNoReparsePoint(fullPath, "--game-path must not be a symbolic link or reparse point.");
        return new DirectoryInfo(fullPath);
    }

    public static string ValidateOutputRoot(DirectoryInfo gameRoot, string? suppliedPath)
    {
        if (string.IsNullOrWhiteSpace(suppliedPath))
        {
            throw new DiscoveryException("--output-root must identify a writable directory outside the game installation.");
        }

        var outputRoot = GetFullPath(suppliedPath, "--output-root");
        if (IsSameOrDescendant(gameRoot.FullName, outputRoot))
        {
            throw new DiscoveryException("--output-root must be outside the inspected game installation.");
        }

        if (File.Exists(outputRoot) && !Directory.Exists(outputRoot))
        {
            throw new DiscoveryException("--output-root must identify a directory, not a file.");
        }

        EnsureNoReparsePointInExistingPath(outputRoot);
        return outputRoot;
    }

    internal static bool TryResolveReadFile(
        DirectoryInfo gameRoot,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(Path.Combine(gameRoot.FullName, relativePath));
            if (!IsSameOrDescendant(gameRoot.FullName, fullPath)
                || !File.Exists(fullPath))
            {
                return false;
            }

            EnsureNoReparsePointOnExistingPath(
                fullPath,
                "A compatibility candidate uses a symbolic link or reparse point and was not inspected.");
            return true;
        }
        catch (DiscoveryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new DiscoveryException("A compatibility candidate could not be accessed safely.", exception);
        }
    }

    internal static bool TryResolveReadDirectory(
        DirectoryInfo gameRoot,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(Path.Combine(gameRoot.FullName, relativePath));
            if (!IsSameOrDescendant(gameRoot.FullName, fullPath)
                || !Directory.Exists(fullPath))
            {
                return false;
            }

            EnsureNoReparsePointOnExistingPath(
                fullPath,
                "A compatibility directory uses a symbolic link or reparse point and was not inspected.");
            return true;
        }
        catch (DiscoveryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new DiscoveryException("A compatibility directory could not be accessed safely.", exception);
        }
    }

    private static string GetFullPath(string suppliedPath, string optionName)
    {
        try
        {
            return Path.GetFullPath(suppliedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new DiscoveryException($"{optionName} is not a valid accessible path.", exception);
        }
    }

    private static void EnsureNoReparsePoint(string path, string message)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new DiscoveryException(message);
            }
        }
        catch (DiscoveryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new DiscoveryException("The supplied path is not accessible.", exception);
        }
    }

    private static void EnsureNoReparsePointInExistingPath(string path)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            var existsAsDirectory = Directory.Exists(current.FullName);
            var existsAsFile = File.Exists(current.FullName);
            if (existsAsFile && !existsAsDirectory)
            {
                throw new DiscoveryException("--output-root has a file in its parent path and cannot be used.");
            }

            try
            {
                if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new DiscoveryException(
                        "--output-root must not be a symbolic link or reparse point, including in its parent path.");
                }
            }
            catch (DiscoveryException)
            {
                throw;
            }
            catch (FileNotFoundException)
            {
                // The output path may be a not-yet-created directory.
            }
            catch (DirectoryNotFoundException)
            {
                // Walk upward until an existing parent is found.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new DiscoveryException("--output-root is not accessible.", exception);
            }

            current = current.Parent;
        }
    }

    private static void EnsureNoReparsePointOnExistingPath(string path, string message)
    {
        FileSystemInfo? current = File.Exists(path)
            ? new FileInfo(path)
            : new DirectoryInfo(path);
        while (current is not null)
        {
            try
            {
                if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new DiscoveryException(message);
                }
            }
            catch (DiscoveryException)
            {
                throw;
            }
            catch (FileNotFoundException)
            {
                // The candidate disappeared between existence and attribute checks.
            }
            catch (DirectoryNotFoundException)
            {
                // The candidate disappeared between existence and attribute checks.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new DiscoveryException("A compatibility candidate is not accessible.", exception);
            }

            current = current switch
            {
                DirectoryInfo directory => directory.Parent,
                FileInfo file => file.Directory,
                _ => null
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
        if (string.Equals(normalizedRoot, normalizedCandidate, comparison))
        {
            return true;
        }

        var rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(rootPrefix, comparison);
    }
}
