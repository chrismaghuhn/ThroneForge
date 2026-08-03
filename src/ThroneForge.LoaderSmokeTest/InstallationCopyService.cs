namespace ThroneForge.LoaderSmokeTest;

public static class InstallationCopyService
{
    public static CopyManifest Copy(SmokeTestRoots roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(roots.OriginalGameRoot);

        if (Directory.Exists(roots.CleanGameRoot) || File.Exists(roots.CleanGameRoot))
        {
            throw new SmokeTestException("The disposable clean-game directory already exists; refusing to merge into it.");
        }

        Directory.CreateDirectory(roots.ExperimentRoot);
        var temporaryRoot = Path.Combine(
            roots.ExperimentRoot,
            $".clean-game-copy-{Guid.NewGuid():N}.tmp");
        var entries = new List<FileManifestEntry>();
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            foreach (var sourceFile in EnumerateFilesSafely(roots.OriginalGameRoot))
            {
                var relativePath = Path.GetRelativePath(roots.OriginalGameRoot, sourceFile).Replace('\\', '/');
                var destination = Path.Combine(temporaryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                entries.Add(CopyAndHash(sourceFile, destination, relativePath));
            }

            Directory.Move(temporaryRoot, roots.CleanGameRoot);
            return new CopyManifest(entries.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
        }
        catch (SmokeTestException)
        {
            TryDelete(temporaryRoot);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            TryDelete(temporaryRoot);
            throw new SmokeTestException("The disposable game copy could not be completed; no partial profile was retained.", exception);
        }
    }

    public static CopyManifest CaptureManifest(string root)
    {
        var normalized = Path.GetFullPath(root);
        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(normalized);
        var entries = EnumerateFilesSafely(normalized)
            .Select(path => FileManifestHasher.HashFile(normalized, path))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        return new CopyManifest(entries);
    }

    public static void RestoreFilesToManifest(string root, CopyManifest baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var expected = baseline.Files
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var expectedDirectories = baseline.Files
            .SelectMany(item => ParentDirectories(item.RelativePath))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var file in EnumerateFilesSafely(root).ToArray())
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (!expected.Contains(relative))
            {
                File.Delete(file);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(item => item.Length)
                     .ThenByDescending(item => item, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, directory).Replace('\\', '/');
            if (!expectedDirectories.Contains(relative)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

    private static IEnumerable<string> ParentDirectories(string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        while (!string.IsNullOrEmpty(directory))
        {
            yield return directory.Replace('\\', '/');
            directory = Path.GetDirectoryName(directory);
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new SmokeTestException("The game copy could not be enumerated safely.", exception);
            }

            foreach (var entry in entries)
            {
                var attributes = GetAttributesSafely(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new SmokeTestException("The original installation contains a symbolic link or reparse point; copying stopped.");
                }

                if (Directory.Exists(entry))
                {
                    pending.Push(entry);
                }
                else if (File.Exists(entry))
                {
                    yield return entry;
                }
                else
                {
                    throw new SmokeTestException("The original installation contains an unsupported filesystem entry.");
                }
            }
        }
    }

    private static FileAttributes GetAttributesSafely(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new SmokeTestException("The original installation could not be inspected safely.", exception);
        }
    }

    private static FileManifestEntry CopyAndHash(string source, string destination, string relativePath)
    {
        try
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
            using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long size = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
                size += read;
            }

            output.Flush(flushToDisk: true);
            return new FileManifestEntry(relativePath, size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("A game file could not be copied safely.", exception);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
