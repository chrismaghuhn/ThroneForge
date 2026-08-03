using System.IO.Compression;
using System.Security.Cryptography;

namespace ThroneForge.LoaderSmokeTest;

public static class ArchiveSafetyService
{
    public static ArchiveInspectionResult Inspect(
        string archivePath,
        ArchiveSafetyLimits? limits = null)
    {
        var normalizedArchive = ValidateArchivePath(archivePath);
        var effectiveLimits = limits ?? new ArchiveSafetyLimits();
        try
        {
            using var stream = new FileStream(normalizedArchive, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            return InspectEntries(normalizedArchive, archive, effectiveLimits);
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new SmokeTestException("The loader archive is not a readable ZIP archive.", exception);
        }
    }

    public static ArchiveInspectionResult Extract(
        string archivePath,
        string extractionRoot,
        ArchiveSafetyLimits? limits = null)
    {
        var inspected = Inspect(archivePath, limits);
        var target = SmokeTestPathValidator.ValidateNewDirectoryPath(extractionRoot);
        var parent = Directory.GetParent(target)?.FullName
            ?? throw new SmokeTestException("The extraction target has no usable parent directory.");
        Directory.CreateDirectory(parent);
        var temporaryRoot = Path.Combine(parent, $".{Path.GetFileName(target)}-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            using var stream = new FileStream(inspected.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries)
            {
                var relativePath = NormalizeEntryPath(entry.FullName, limits ?? new ArchiveSafetyLimits());
                if (entry.FullName.EndsWith('/'))
                {
                    var directory = SmokeTestPathValidator.EnsureWithin(temporaryRoot, Path.Combine(temporaryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                    Directory.CreateDirectory(directory);
                    continue;
                }

                var destination = SmokeTestPathValidator.EnsureWithin(
                    temporaryRoot,
                    Path.Combine(temporaryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                var destinationParent = Path.GetDirectoryName(destination)
                    ?? throw new SmokeTestException("The archive entry has no destination parent.");
                Directory.CreateDirectory(destinationParent);
                SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(destinationParent);
                using var input = entry.Open();
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }

            Directory.Move(temporaryRoot, target);
            return inspected with { ExtractionRoot = target };
        }
        catch (SmokeTestException)
        {
            TryDelete(temporaryRoot);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            TryDelete(temporaryRoot);
            throw new SmokeTestException("The loader archive could not be extracted safely.", exception);
        }
    }

    private static ArchiveInspectionResult InspectEntries(
        string archivePath,
        ZipArchive archive,
        ArchiveSafetyLimits limits)
    {
        if (archive.Entries.Count > limits.MaximumEntries)
        {
            throw new SmokeTestException("The loader archive contains too many entries.");
        }

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);
        var manifest = new List<ArchiveManifestEntry>();
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var relativePath = NormalizeEntryPath(entry.FullName, limits);
            if (!seen.Add(relativePath))
            {
                throw new SmokeTestException("The loader archive contains duplicate normalized destinations.");
            }

            if (IsSymbolicLink(entry))
            {
                throw new SmokeTestException("The loader archive contains a symbolic-link entry.");
            }

            var isDirectory = entry.FullName.EndsWith('/');
            if (!isDirectory)
            {
                if (entry.Length < 0 || entry.Length > limits.MaximumExpandedBytes - expandedBytes)
                {
                    throw new SmokeTestException("The loader archive exceeds the expanded-size limit.");
                }

                expandedBytes += entry.Length;
                using var input = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[64 * 1024];
                long size = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    size += read;
                    if (size > limits.MaximumExpandedBytes)
                    {
                        throw new SmokeTestException("The loader archive exceeds the expanded-size limit.");
                    }
                }

                manifest.Add(new ArchiveManifestEntry(
                    relativePath,
                    false,
                    size,
                    Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
            }
            else
            {
                manifest.Add(new ArchiveManifestEntry(relativePath, true, 0, string.Empty));
            }
        }

        return new ArchiveInspectionResult(
            archivePath,
            manifest.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray(),
            expandedBytes);
    }

    private static string NormalizeEntryPath(string entryName, ArchiveSafetyLimits limits)
    {
        if (string.IsNullOrWhiteSpace(entryName)
            || entryName.Contains('\0')
            || entryName.Contains('\\', StringComparison.Ordinal)
            || entryName.Length > limits.MaximumPathLength
            || entryName.StartsWith('/')
            || entryName.StartsWith('\\')
            || entryName.StartsWith("//", StringComparison.Ordinal)
            || entryName.StartsWith("\\\\", StringComparison.Ordinal)
            || (entryName.Length >= 2 && char.IsLetter(entryName[0]) && entryName[1] == ':')
            || entryName.Contains(':', StringComparison.Ordinal))
        {
            throw new SmokeTestException("The loader archive contains an unsafe path.");
        }

        var parts = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
        {
            throw new SmokeTestException("The loader archive contains a traversal path.");
        }

        return string.Join('/', parts);
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        return ((entry.ExternalAttributes >> 16) & unixFileTypeMask) == unixSymbolicLink;
    }

    private static string ValidateArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !File.Exists(path))
        {
            throw new SmokeTestException("The loader archive path must be an existing absolute file path.");
        }

        var normalized = Path.GetFullPath(path);
        SmokeTestPathValidator.ValidateNewDirectoryPath(Path.GetDirectoryName(normalized) ?? normalized);
        return normalized;
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
