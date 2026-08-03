using System.Text;

namespace ThroneForge.LoaderSmokeTest;

public static class SmokeTestReportWriter
{
    public static string BuildReport(SmokeTestDetailedReport data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var builder = new StringBuilder();
        builder.AppendLine("# Thronefall Loader Smoke-Test Report");
        builder.AppendLine();
        Append(builder, "Base game fingerprint", data.Fingerprint);
        Append(builder, "Task version", data.TaskVersion);
        Append(builder, "Test timestamp UTC", data.TimestampUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        Append(builder, "Original installation verification", data.OriginalInstallationVerification);
        Append(builder, "Disposable profile verification", data.DisposableProfileVerification);
        Append(builder, "Baseline launch result", data.BaselineLaunchResult);
        Append(builder, "Loader candidate", "BepInEx 5 Unity Mono x64");
        Append(builder, "Official release verification", data.OfficialReleaseVerification);
        Append(builder, "Archive asset name", data.ArchiveAssetName);
        Append(builder, "Archive asset ID", data.ArchiveAssetId);
        Append(builder, "Archive size", data.ArchiveSize);
        Append(builder, "Archive digest status", data.ArchiveDigestStatus);
        Append(builder, "Observed SHA-256", data.ObservedSha256);
        Append(builder, "Secure extraction result", data.SecureExtractionResult);
        Append(builder, "Transaction summary", data.TransactionSummary);
        Append(builder, "Loader-enabled launch result", data.LoaderEnabledLaunchResult);
        Append(builder, "Generated BepInEx evidence", data.GeneratedBepInExEvidence);
        Append(builder, "BepInEx version observed", data.LogSummary.BepInExVersion ?? "Unknown");
        Append(builder, "Preloader status", data.LogSummary.PreloaderInitialized ? "Initialized" : "Not evidenced");
        Append(builder, "Chainloader status", data.LogSummary.ChainloaderInitialized ? "Initialized" : "Not evidenced");
        Append(builder, "Custom plugins loaded", data.LogSummary.PluginsDiscovered.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(
            builder,
            "Sanitized log summary",
            $"Version: {data.LogSummary.BepInExVersion ?? "Unknown"}; "
            + $"configuration generated: {data.LogSummary.ConfigurationGenerated}; "
            + $"preloader initialized: {data.LogSummary.PreloaderInitialized}; "
            + $"chainloader initialized: {data.LogSummary.ChainloaderInitialized}; "
            + $"plugins discovered: {data.LogSummary.PluginsDiscovered}; "
            + $"warnings: {data.LogSummary.WarningCount}; "
            + $"errors: {data.LogSummary.ErrorCount}; "
            + $"fatal errors: {data.LogSummary.FatalErrorCount}.");
        AppendList(builder, "Warnings", data.Warnings);
        AppendList(builder, "Errors", data.Errors);
        Append(builder, "Overall result", data.Outcome.ToString());
        Append(builder, "Rollback result", data.RollbackResult);
        Append(builder, "Original installation post-verification", data.OriginalPostVerification);
        Append(builder, "Original full-manifest post-verification", data.OriginalFullManifestPostVerification);
        Append(builder, "Original runtime-readiness post-verification", data.OriginalRuntimeReadinessPostVerification);
        Append(builder, "Original loader-indicator post-verification", data.OriginalLoaderIndicatorPostVerification);
        Append(builder, "Disposable full-manifest rollback verification", data.DisposableFullManifestRollbackVerification);
        Append(builder, "Recovery or rollback state", data.RecoveryOrRollbackState);
        Append(builder, "Remaining uncertainty", data.RemainingUncertainty);
        Append(builder, "Next permitted task", data.NextPermittedTask);
        builder.AppendLine("## Security and privacy statement");
        builder.AppendLine();
        builder.AppendLine("The original installation was treated as read-only. This committed report contains only sanitized process and loader summaries. Absolute paths, usernames, machine names, raw logs, binaries, archives, save data, secrets, and decompiled source are excluded.");
        return builder.ToString();
    }

    public static string BuildSanitizedReport(SmokeTestReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var builder = new StringBuilder();
        builder.AppendLine("# Thronefall Loader Smoke-Test Report");
        builder.AppendLine();
        Append(builder, "Base game fingerprint", data.Fingerprint);
        Append(builder, "Task version", data.TaskVersion);
        Append(builder, "Original installation verification", "Performed against the explicit installation; absolute path intentionally omitted.");
        Append(builder, "Disposable profile verification", "Performed in an external disposable profile; absolute path intentionally omitted.");
        Append(builder, "Loader candidate", "BepInEx 5 Unity Mono x64 5.4.23.5");
        Append(builder, "BepInEx version observed", data.BepInExVersion ?? "Unknown");
        Append(builder, "Overall result", data.Outcome.ToString());
        builder.AppendLine("## Security and privacy statement");
        builder.AppendLine();
        builder.AppendLine("Only sanitized process and loader summary evidence is retained. Original and experiment absolute paths, raw logs, binaries, archives, usernames, machine names, and secrets are excluded.");
        builder.AppendLine();
        builder.AppendLine("## Remaining uncertainty");
        builder.AppendLine();
        builder.AppendLine("This report does not verify a ThroneForge plugin, Harmony compatibility, lifecycle bindings, game APIs, catalog extraction, or custom waves.");
        return builder.ToString();
    }

    public static string WriteAtomic(string outputPath, string markdown, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathRooted(outputPath))
        {
            throw new SmokeTestException("The smoke-test report path must be an absolute path.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new SmokeTestException("The smoke-test report has no parent directory.");
        Directory.CreateDirectory(parent);
        if (File.Exists(fullPath) && !overwrite)
        {
            throw new SmokeTestException("A smoke-test report already exists; explicit overwrite is required.");
        }

        var temporary = Path.Combine(parent, $".{Path.GetFileNameWithoutExtension(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, fullPath, overwrite);
            return fullPath;
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The smoke-test report could not be written safely.", exception);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
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

    private static void Append(StringBuilder builder, string heading, string value)
    {
        builder.Append("## ").AppendLine(heading);
        builder.AppendLine();
        builder.AppendLine(value);
        builder.AppendLine();
    }

    private static void AppendList(StringBuilder builder, string heading, IReadOnlyList<string> values)
    {
        builder.Append("## ").AppendLine(heading);
        builder.AppendLine();
        if (values.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var value in values)
            {
                builder.Append("- ").AppendLine(value);
            }
        }

        builder.AppendLine();
    }
}
