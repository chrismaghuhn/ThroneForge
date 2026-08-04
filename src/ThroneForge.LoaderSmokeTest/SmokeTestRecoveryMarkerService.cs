using System.Text.Json;

namespace ThroneForge.LoaderSmokeTest;

public static class SmokeTestRecoveryMarkerService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Write(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(fullPath)
                ?? throw new SmokeTestException("The recovery marker has no parent directory.");
            Directory.CreateDirectory(parent);
            File.WriteAllText(
                fullPath,
                JsonSerializer.Serialize(
                    new
                    {
                        SchemaVersion = "throneforge-loader-recovery-v1",
                        State = SmokeTestRollbackState.ManualClosureRequired.ToString(),
                        Message = "The copied process requires manual graceful closure before rollback.",
                        RollbackMode = SmokeTestMode.Rollback.ToString()
                    },
                    JsonOptions));
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The local recovery marker could not be written safely.", exception);
        }
    }

    public static void Clear(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The local recovery marker could not be cleared safely.", exception);
        }
    }
}
