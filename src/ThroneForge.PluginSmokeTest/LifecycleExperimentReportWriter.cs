using System.Globalization;
using System.Text;
using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public sealed class LifecycleExperimentReportWriter
{
    private readonly string repositoryRoot;
    private readonly string expectedFingerprint;

    public LifecycleExperimentReportWriter(string repositoryRoot, string expectedFingerprint)
    {
        this.repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        this.expectedFingerprint = expectedFingerprint ?? throw new ArgumentNullException(nameof(expectedFingerprint));
    }

    public string Write(LifecycleExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var reportPath = SmokeTestPathValidator.ValidateLifecycleReportPath(repositoryRoot, expectedFingerprint);
        var parent = Path.GetDirectoryName(reportPath) ?? throw new PluginSmokeException("The lifecycle report has no safe parent.");
        Directory.CreateDirectory(parent);
        var content = Build(result);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(reportPath)}.{Guid.NewGuid():N}.tmp");
        return WriteAtomic(reportPath, content, temporary);
    }

    public string AppendRecovery(LifecycleExperimentRollbackResult recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        var reportPath = SmokeTestPathValidator.ValidateLifecycleReportPath(repositoryRoot, expectedFingerprint);
        if (!File.Exists(reportPath))
        {
            throw new PluginSmokeException("The lifecycle report cannot be extended before the experiment report exists.");
        }

        var builder = new StringBuilder(File.ReadAllText(reportPath, Encoding.UTF8).TrimEnd());
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Recovery");
        builder.AppendLine();
        Append(builder, "Recovery required", "true");
        Append(builder, "Plugin removal", recovery.PluginRemovalStatus.ToString());
        Append(builder, "Loader rollback", recovery.LoaderRollbackStatus.ToString());
        Append(builder, "Loader rollback verified", recovery.LoaderRollbackVerified.ToString());
        Append(builder, "Disposable restoration", recovery.DisposableRestored.ToString());
        Append(builder, "Original verification", recovery.OriginalVerified.ToString());
        Append(builder, "Recovery result", recovery.OverallResult);
        Append(builder, "Recovery category", SafeToken(recovery.FailureCategory));
        return WriteAtomic(reportPath, builder.ToString(), null);
    }

    private static string WriteAtomic(string reportPath, string content, string? temporaryPath)
    {
        var parent = Path.GetDirectoryName(reportPath) ?? throw new PluginSmokeException("The lifecycle report has no safe parent.");
        var temporary = temporaryPath ?? Path.Combine(parent, $".{Path.GetFileName(reportPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, reportPath, overwrite: true);
            return reportPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("The lifecycle report could not be written safely.", exception);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private string Build(LifecycleExperimentResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Thronefall Lifecycle Binding Report");
        builder.AppendLine();
        Append(builder, "Report version", "throneforge-lifecycle-binding-v2");
        Append(builder, "Game fingerprint", expectedFingerprint.ToLowerInvariant());
        Append(builder, "Binding ID", LifecycleBindingIds.ApplicationQuittingV1);
        var quittingObserved = result.OverallResult == "Passed"
            && result.QuittingCount == 1
            && string.Equals(result.MarkerEncounterOrder, "1,2,3", StringComparison.Ordinal);
        Append(builder, "Source", quittingObserved
            ? "public UnityEngine.Application.quitting event observed while Thronefall was running"
            : "planned public UnityEngine.Application.quitting binding; event not verified");
        Append(builder, "Current stage", result.CurrentStage.ToString());
        Append(builder, "Failed stage", result.FailedStage?.ToString() ?? "none");
        Append(builder, "Last completed stage", result.LastCompletedStage?.ToString() ?? "none");
        Append(builder, "Primary failed stage", result.PrimaryFailedStage?.ToString() ?? "none");
        Append(builder, "Primary failure category", result.PrimaryFailureCategory ?? "none");
        Append(builder, "Cleanup failure category", result.CleanupFailureCategory ?? "none");
        Append(builder, "Overall result", result.OverallResult);
        Append(builder, "Stable category", result.StableCategory);
        Append(builder, "Stage state persisted", result.StageStatePersisted.ToString());
        Append(builder, "Loader applied", result.LoaderApplied.ToString());
        Append(builder, "Plugin deployed", result.PluginDeployed.ToString());
        Append(builder, "Process active", result.ProcessActive.ToString());
        Append(builder, "Selected executable relative path", SafeRelative(result.SelectedExecutableRelativePath));
        Append(builder, "Unity source assembly identity", SafeToken(result.UnitySourceAssemblyIdentity));
        Append(builder, "Package SHA-256", SafeToken(result.PackageSha256));
        Append(builder, "Admission binding digest", SafeToken(result.AdmissionBindingDigest));
        Append(builder, "Initialization count", result.InitializationCount?.ToString(CultureInfo.InvariantCulture) ?? "not-observed");
        Append(builder, "Unity-quitting count", result.QuittingCount?.ToString(CultureInfo.InvariantCulture) ?? "not-observed");
        Append(builder, "Shutdown count", result.ShutdownCount?.ToString(CultureInfo.InvariantCulture) ?? "not-observed");
        Append(builder, "Marker encounter order", result.MarkerEncounterOrder ?? "not-observed");
        Append(builder, "Runtime API identity", result.RuntimeApiIdentity ?? "not-observed");
        Append(builder, "Runtime Contracts identity", result.RuntimeContractsIdentity ?? "not-observed");
        Append(builder, "Plugin count", result.PluginCount?.ToString(CultureInfo.InvariantCulture) ?? "not-observed");
        Append(builder, "Loader warnings/errors/fatal", result.WarningCount is null ? "not-observed" : $"{result.WarningCount}/{result.ErrorCount}/{result.FatalErrorCount}");
        Append(builder, "Plugin removal verified", Format(result.PluginRemovalVerified));
        Append(builder, "Plugin removal status", result.PluginRemovalStatus?.ToString() ?? "not-observed");
        Append(builder, "Loader rollback verified", Format(result.LoaderRollbackVerified));
        Append(builder, "Loader rollback status", result.LoaderRollbackStatus?.ToString() ?? "not-observed");
        Append(builder, "Disposable restoration verified", Format(result.DisposableRestorationVerified));
        Append(builder, "Original manifest verified", Format(result.OriginalManifestVerified));
        Append(builder, "Original runtime verified", Format(result.OriginalRuntimeVerified));
        Append(builder, "Original loader indicators absent", Format(result.OriginalLoaderIndicatorsAbsent));
        Append(builder, "Recovery marker persisted", Format(result.RecoveryMarkerPersisted));
        Append(builder, "Recovery marker failure category", SafeToken(result.RecoveryMarkerFailureCategory));
        Append(builder, "Rollback command", SafeToken(result.RollbackCommand));
        Append(builder, "Recovery action", SafeToken(result.RecoveryAction));
        builder.AppendLine();
        builder.AppendLine("## Historical private attempts");
        builder.AppendLine();
        builder.AppendLine("- Attempt 1: Failed before LoaderInstall completion; the transaction state was not persisted and no package or lifecycle evidence was produced.");
        builder.AppendLine("- Corrective attempt: Failed at OriginalPreflight with category `original-preflight-failed` because the harness expected a different selected-executable presentation; no transaction, package, deployment or lifecycle evidence was produced.");
        builder.AppendLine();
        builder.AppendLine("## Scope and uncertainty");
        builder.AppendLine();
        builder.AppendLine("This task is limited to the public Unity `Application.quitting` event. It does not verify a Thronefall-defined lifecycle method, Harmony compatibility, game APIs, gameplay state, catalog extraction, save compatibility, asynchronous lifecycle support or arbitrary third-party code safety.");
        builder.AppendLine("No nonce, absolute path, raw log, manifest, binary, username, machine name or private experiment state is included.");
        return builder.ToString();
    }

    private static string Format(bool? value) => value switch
    {
        true => "true",
        false => "false",
        _ => "not-observed"
    };

    private static string SafeRelative(string? value)
        => string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || value.Contains(':')
            || value.Split('/').Any(part => part is "" or "." or "..")
            ? "not-observed"
            : value;

    private static string SafeToken(string? value)
        => string.IsNullOrWhiteSpace(value)
            || value.Any(character => char.IsControl(character) || character is '\\' or '/' or ':')
            ? "not-observed"
            : value;

    private static void Append(StringBuilder builder, string name, string value)
    {
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ');
        builder.Append("- ").Append(name).Append(": ").AppendLine(sanitized);
    }
}
