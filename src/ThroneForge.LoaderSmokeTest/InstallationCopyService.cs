namespace ThroneForge.LoaderSmokeTest;

public static class InstallationCopyService
{
    public static string ComputeManifestIdentity(CopyManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var canonical = new System.Text.StringBuilder("throneforge-copy-manifest-v1\n");
        foreach (var directory in (manifest.Directories ?? []).Order(StringComparer.Ordinal))
        {
            canonical.Append("D|").Append(directory).Append('\n');
        }

        foreach (var file in manifest.Files.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            canonical.Append("F|")
                .Append(file.RelativePath).Append('|')
                .Append(file.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(file.Sha256.ToLowerInvariant()).Append('\n');
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

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

                CopyAndHash(sourceFile, destination, relativePath);
            }

            Directory.Move(temporaryRoot, roots.CleanGameRoot);
            return CaptureManifest(roots.CleanGameRoot);
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
        try
        {
            var normalized = Path.GetFullPath(root);
            SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(normalized);
            var entries = EnumerateFilesSafely(normalized)
                .Select(path => FileManifestHasher.HashFile(normalized, path))
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToArray();
            var directories = Directory.EnumerateDirectories(normalized, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(normalized, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();
            return new CopyManifest(entries, directories);
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The complete profile manifest could not be captured safely.", exception);
        }
    }

    public static ManifestVerificationResult RestoreFilesToManifest(string root, CopyManifest baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var expected = baseline.Files
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var expectedDirectories = (baseline.Directories ?? [])
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

        return CompareManifests(baseline, CaptureManifest(root));
    }

    public static ManifestVerificationResult CompareManifests(CopyManifest expected, CopyManifest actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var expectedFiles = expected.Files.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        var actualFiles = actual.Files.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        var added = actualFiles.Keys
            .Except(expectedFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => new ManifestDifference(path, null, actualFiles[path]))
            .ToArray();
        var removed = expectedFiles.Keys
            .Except(actualFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => new ManifestDifference(path, expectedFiles[path], null))
            .ToArray();
        var changed = expectedFiles.Keys
            .Intersect(actualFiles.Keys, StringComparer.Ordinal)
            .Where(path => expectedFiles[path].Size != actualFiles[path].Size
                || !string.Equals(expectedFiles[path].Sha256, actualFiles[path].Sha256, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(path => new ManifestDifference(path, expectedFiles[path], actualFiles[path]))
            .ToArray();
        var expectedDirectories = (expected.Directories ?? []).ToHashSet(StringComparer.Ordinal);
        var actualDirectories = (actual.Directories ?? []).ToHashSet(StringComparer.Ordinal);
        var unexpectedDirectories = actualDirectories
            .Except(expectedDirectories, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingDirectories = expectedDirectories
            .Except(actualDirectories, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var status = changed.Length > 0
            ? ManifestVerificationStatus.ChangedFiles
            : added.Length > 0 || unexpectedDirectories.Length > 0
                ? ManifestVerificationStatus.AddedFiles
                : removed.Length > 0 || missingDirectories.Length > 0
                    ? ManifestVerificationStatus.RemovedFiles
                    : ManifestVerificationStatus.Matches;
        return new ManifestVerificationResult(
            status,
            added,
            removed,
            changed,
            unexpectedDirectories,
            missingDirectories);
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
