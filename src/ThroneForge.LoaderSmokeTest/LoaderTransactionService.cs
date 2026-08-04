namespace ThroneForge.LoaderSmokeTest;

public static class LoaderTransactionService
{
    public static void ValidatePersistedEntries(
        SmokeTestRoots roots,
        IReadOnlyList<TransactionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(entries);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var destinations = new HashSet<string>(comparer);
        var backups = new HashSet<string>(comparer);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new SmokeTestException("The persisted loader transaction contains a null entry.");
            }

            ValidateRelativePath(roots.CleanGameRoot, entry.RelativePath);
            if (!destinations.Add(entry.RelativePath))
            {
                throw new SmokeTestException("The persisted loader transaction contains duplicate normalized destinations.");
            }

            _ = entry.Change switch
            {
                TransactionChangeKind.NewFile when entry.OriginalSha256 is null
                    && IsSha256(entry.ReplacementSha256)
                    && entry.BackupRelativePath is null => true,
                TransactionChangeKind.Overwrite when IsSha256(entry.OriginalSha256)
                    && IsSha256(entry.ReplacementSha256)
                    && !string.IsNullOrWhiteSpace(entry.BackupRelativePath) => ValidateBackupPath(roots, entry.BackupRelativePath, backups),
                TransactionChangeKind.Unchanged when IsSha256(entry.OriginalSha256)
                    && string.Equals(entry.OriginalSha256, entry.ReplacementSha256, StringComparison.OrdinalIgnoreCase)
                    && entry.BackupRelativePath is null => true,
                TransactionChangeKind.CreatedDirectory when entry.OriginalSha256 is null
                    && entry.ReplacementSha256 is null
                    && entry.BackupRelativePath is null => true,
                _ => throw new SmokeTestException("The persisted loader transaction contains inconsistent hashes or change metadata.")
            };
        }
    }

    public static TransactionPlan Prepare(SmokeTestRoots roots, ArchiveInspectionResult archive)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(archive);
        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(roots.CleanGameRoot);

        var entries = new List<TransactionEntry>();
        foreach (var item in archive.Manifest.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var destination = SmokeTestPathValidator.EnsureWithin(
                roots.CleanGameRoot,
                Path.Combine(roots.CleanGameRoot, item.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (item.IsDirectory)
            {
                if (File.Exists(destination))
                {
                    throw new SmokeTestException("The loader archive conflicts with an existing file path.");
                }

                if (!Directory.Exists(destination))
                {
                    entries.Add(new TransactionEntry(item.RelativePath, TransactionChangeKind.CreatedDirectory, null, null, null));
                }

                continue;
            }

            if (Directory.Exists(destination))
            {
                throw new SmokeTestException("The loader archive conflicts with an existing directory path.");
            }

            if (!File.Exists(destination))
            {
                entries.Add(new TransactionEntry(item.RelativePath, TransactionChangeKind.NewFile, null, item.Sha256, null));
                continue;
            }

            var original = FileManifestHasher.HashFile(roots.CleanGameRoot, destination);
            var change = string.Equals(original.Sha256, item.Sha256, StringComparison.OrdinalIgnoreCase)
                ? TransactionChangeKind.Unchanged
                : TransactionChangeKind.Overwrite;
            entries.Add(new TransactionEntry(
                item.RelativePath,
                change,
                original.Sha256,
                item.Sha256,
                change == TransactionChangeKind.Overwrite ? Path.Combine("backup", item.RelativePath).Replace('\\', '/') : null));
        }

        return new TransactionPlan(archive.ArchivePath, entries);
    }

    public static void Apply(
        SmokeTestRoots roots,
        TransactionPlan plan,
        ArchiveInspectionResult archive,
        int? failAfterEntries = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(archive);
        ValidatePersistedEntries(roots, plan.Entries);
        var archiveFiles = archive.Manifest
            .Where(item => !item.IsDirectory)
            .ToDictionary(item => item.RelativePath, StringComparer.Ordinal);

        try
        {
            Directory.CreateDirectory(roots.ExperimentRoot);
            var applied = 0;
            foreach (var transaction in plan.Entries)
            {
                if (transaction.Change == TransactionChangeKind.Unchanged)
                {
                    continue;
                }

                var destination = Path.Combine(roots.CleanGameRoot, transaction.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                SmokeTestPathValidator.EnsureWithin(roots.CleanGameRoot, destination);
                if (transaction.Change == TransactionChangeKind.CreatedDirectory)
                {
                    Directory.CreateDirectory(destination);
                }
                else
                {
                    var parent = Path.GetDirectoryName(destination)
                        ?? throw new SmokeTestException("The loader transaction has no destination parent.");
                    Directory.CreateDirectory(parent);
                    if (transaction.Change == TransactionChangeKind.Overwrite)
                    {
                        var backup = Path.Combine(roots.ManifestsRoot, transaction.BackupRelativePath!.Replace('/', Path.DirectorySeparatorChar));
                        SmokeTestPathValidator.EnsureWithin(roots.BackupRoot, Path.Combine(roots.BackupRoot, transaction.BackupRelativePath["backup/".Length..].Replace('/', Path.DirectorySeparatorChar)));
                        var backupParent = Path.GetDirectoryName(backup)!;
                        Directory.CreateDirectory(backupParent);
                        File.Copy(destination, backup, overwrite: false);
                    }

                    if (archive.ExtractionRoot is null)
                    {
                        throw new SmokeTestException("The loader transaction requires an extracted archive root.");
                    }

                    var source = SmokeTestPathValidator.EnsureWithin(
                        archive.ExtractionRoot,
                        Path.Combine(archive.ExtractionRoot, transaction.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!archiveFiles.ContainsKey(transaction.RelativePath) || !File.Exists(source))
                    {
                        throw new SmokeTestException("The loader transaction source is missing from the validated extraction.");
                    }

                    CopyFile(source, destination);
                }

                applied++;
                if (failAfterEntries is not null && applied >= failAfterEntries.Value)
                {
                    throw new SmokeTestException("Synthetic transaction failure requested.");
                }
            }

            if (!Verify(roots, plan, archive))
            {
                throw new SmokeTestException("The loader transaction verification failed.");
            }
        }
        catch (SmokeTestException)
        {
            Rollback(roots, plan);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Rollback(roots, plan);
            throw new SmokeTestException("The loader transaction failed and was rolled back.", exception);
        }
    }

    public static bool Verify(SmokeTestRoots roots, TransactionPlan plan, ArchiveInspectionResult archive)
    {
        ValidatePersistedEntries(roots, plan.Entries);
        var expected = archive.Manifest
            .Where(item => !item.IsDirectory)
            .ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        foreach (var entry in plan.Entries.Where(item => item.Change is TransactionChangeKind.NewFile or TransactionChangeKind.Overwrite))
        {
            var destination = Path.Combine(roots.CleanGameRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(destination)
                || !expected.TryGetValue(entry.RelativePath, out var expectedFile))
            {
                return false;
            }

            var actual = FileManifestHasher.HashFile(roots.CleanGameRoot, destination);
            if (!string.Equals(actual.Sha256, expectedFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var entry in plan.Entries.Where(item => item.Change == TransactionChangeKind.Unchanged))
        {
            var destination = Path.Combine(roots.CleanGameRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(destination)
                || !string.Equals(FileManifestHasher.HashFile(roots.CleanGameRoot, destination).Sha256, entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var entry in plan.Entries.Where(item => item.Change == TransactionChangeKind.CreatedDirectory))
        {
            if (!Directory.Exists(Path.Combine(roots.CleanGameRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                return false;
            }
        }

        return true;
    }

    public static void Rollback(SmokeTestRoots roots, TransactionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(plan);
        ValidatePersistedEntries(roots, plan.Entries);
        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(roots.CleanGameRoot);
        if (Directory.Exists(roots.BackupRoot))
        {
            SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(roots.BackupRoot);
        }
        foreach (var entry in plan.Entries.Reverse())
        {
            var destination = Path.Combine(roots.CleanGameRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                switch (entry.Change)
                {
                    case TransactionChangeKind.NewFile:
                        if (File.Exists(destination))
                        {
                            File.Delete(destination);
                        }

                        break;
                    case TransactionChangeKind.Overwrite:
                        var backup = Path.Combine(roots.ManifestsRoot, entry.BackupRelativePath!.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(backup))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                            File.Copy(backup, destination, overwrite: true);
                        }

                        break;
                    case TransactionChangeKind.CreatedDirectory:
                        if (Directory.Exists(destination)
                            && !Directory.EnumerateFileSystemEntries(destination).Any())
                        {
                            Directory.Delete(destination);
                        }

                        break;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new SmokeTestException("The disposable loader transaction could not be rolled back safely.", exception);
            }
        }
    }

    public static CopyManifest BuildExpectedAppliedManifest(
        CopyManifest baseline,
        ArchiveInspectionResult archive)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(archive);
        var files = baseline.Files.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        var directories = (baseline.Directories ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var item in archive.Manifest)
        {
            AddParentDirectories(directories, item.RelativePath, item.IsDirectory);
            if (item.IsDirectory)
            {
                directories.Add(item.RelativePath);
            }
            else
            {
                files[item.RelativePath] = new FileManifestEntry(item.RelativePath, item.Size, item.Sha256);
            }
        }

        return new CopyManifest(
            files.Values.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray(),
            directories.Order(StringComparer.Ordinal).ToArray());
    }

    private static void AddParentDirectories(HashSet<string> directories, string path, bool isDirectory)
    {
        var parts = path.Split('/');
        var count = isDirectory ? parts.Length - 1 : parts.Length - 1;
        for (var index = 1; index <= count; index++)
        {
            directories.Add(string.Join('/', parts[..index]));
        }
    }

    private static void ValidateRelativePath(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains('\0', StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal)
            || path.StartsWith('/')
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new SmokeTestException("The persisted loader transaction contains an unsafe relative path.");
        }

        SmokeTestPathValidator.EnsureWithin(root, Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool ValidateBackupPath(SmokeTestRoots roots, string path, HashSet<string> backups)
    {
        if (!path.StartsWith("backup/", StringComparison.Ordinal))
        {
            throw new SmokeTestException("The persisted loader backup path is outside the validated backup root.");
        }

        var relative = path["backup/".Length..];
        ValidateRelativePath(roots.BackupRoot, relative);
        if (!backups.Add(path))
        {
            throw new SmokeTestException("The persisted loader transaction contains duplicate backup destinations.");
        }

        return true;
    }

    private static bool IsSha256(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length == 64
            && value.All(Uri.IsHexDigit);

    private static void CopyFile(string source, string destination)
    {
        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
            {
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
