using System.Text;

namespace ThroneForge.Discovery;

internal static class DiscoveryReportWriter
{
    public static string Write(string outputRoot, string fingerprint, string markdown, bool overwriteExisting)
        => WriteFile(outputRoot, $"{fingerprint}.md", markdown, overwriteExisting);

    public static string WriteFile(string outputRoot, string fileName, string markdown, bool overwriteExisting)
    {
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(outputRoot);
            if (string.IsNullOrWhiteSpace(fileName)
                || fileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || fileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new DiscoveryException("The discovery report file name is invalid.");
            }

            var reportPath = Path.Combine(outputRoot, fileName);
            if (File.Exists(reportPath) && !overwriteExisting)
            {
                throw new DiscoveryException(
                    $"A discovery report named '{fileName}' already exists. "
                    + "Pass --overwrite to replace it explicitly.");
            }

            temporaryPath = Path.Combine(outputRoot, $".{Path.GetFileNameWithoutExtension(fileName)}.{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.SequentialScan))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(markdown);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, reportPath, overwrite: overwriteExisting);
            return reportPath;
        }
        catch (DiscoveryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new DiscoveryException("Could not write discovery report.", exception);
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
                // Preserve the original discovery or write failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the original discovery or write failure.
            }
        }
    }
}
