using System.Security.Cryptography;

namespace ThroneForge.LoaderSmokeTest;

internal static class FileManifestHasher
{
    public static FileManifestEntry HashFile(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long size = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                size += read;
            }

            return new FileManifestEntry(
                relative,
                size,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("A required local file could not be read safely.", exception);
        }
    }
}
