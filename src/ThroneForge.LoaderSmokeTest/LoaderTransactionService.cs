namespace ThroneForge.LoaderSmokeTest;

public static class LoaderTransactionService
{
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
                        var backup = Path.Combine(roots.ExperimentRoot, transaction.BackupRelativePath!.Replace('/', Path.DirectorySeparatorChar));
                        var backupParent = Path.GetDirectoryName(backup)!;
                        Directory.CreateDirectory(backupParent);
                        File.Copy(destination, backup, overwrite: false);
                    }

                    if (archive.ExtractionRoot is null)
                    {
                        throw new SmokeTestException("The loader transaction requires an extracted archive root.");
                    }

                    var source = Path.Combine(archive.ExtractionRoot, transaction.RelativePath.Replace('/', Path.DirectorySeparatorChar));
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

        return true;
    }

    public static void Rollback(SmokeTestRoots roots, TransactionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(plan);
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
                        var backup = Path.Combine(roots.ExperimentRoot, entry.BackupRelativePath!.Replace('/', Path.DirectorySeparatorChar));
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
