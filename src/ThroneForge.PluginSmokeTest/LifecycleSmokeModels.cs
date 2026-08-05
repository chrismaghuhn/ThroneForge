using System.Diagnostics;

namespace ThroneForge.PluginSmokeTest;

public static class LifecycleBindingIds
{
    public const string ApplicationQuittingV1 = "unity-application-quitting-v1";
}

public sealed record UnityLifecycleContractModel(
    bool HasApplicationType,
    bool HasQuittingEvent,
    string HandlerType,
    bool AddIsPublic,
    bool RemoveIsPublic,
    bool AddIsStatic,
    bool RemoveIsStatic,
    string AssemblySimpleName = "UnityEngine.CoreModule",
    bool ApplicationIsPublicTopLevel = true);

public sealed record UnityLifecycleMetadataResult(
    bool IsValid,
    string BindingId,
    string? FailureCategory,
    string? AssemblyIdentity,
    string? SourceType,
    string? SourceEvent,
    string? HandlerType);

public static class UnityLifecycleMetadataValidator
{
    public static UnityLifecycleMetadataResult Validate(UnityLifecycleContractModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        const string sourceType = "UnityEngine.Application";
        const string sourceEvent = "quitting";
        const string handlerType = "System.Action";

        if (!string.Equals(model.AssemblySimpleName, "UnityEngine.CoreModule", StringComparison.Ordinal)
            || !model.HasApplicationType
            || !model.ApplicationIsPublicTopLevel
            || !model.HasQuittingEvent)
        {
            return Invalid("unity-lifecycle-contract-missing", sourceType, sourceEvent, model.HandlerType);
        }

        if (!string.Equals(model.HandlerType, handlerType, StringComparison.Ordinal)
            || !model.AddIsPublic
            || !model.RemoveIsPublic
            || !model.AddIsStatic
            || !model.RemoveIsStatic)
        {
            return Invalid("unity-lifecycle-contract-invalid", sourceType, sourceEvent, model.HandlerType);
        }

        return new(true, LifecycleBindingIds.ApplicationQuittingV1, null, null, sourceType, sourceEvent, handlerType);
    }

    private static UnityLifecycleMetadataResult Invalid(string category, string sourceType, string sourceEvent, string handlerType)
        => new(false, LifecycleBindingIds.ApplicationQuittingV1, category, null, sourceType, sourceEvent, handlerType);
}

public sealed record LifecycleMarker(
    string Name,
    string Nonce,
    string BindingId,
    string PluginGuid,
    string PluginVersion,
    string ModId,
    string ModVersion,
    int Sequence,
    string ApiIdentity,
    string ContractsIdentity);

public sealed record LifecycleMarkerParseResult(
    bool IsValid,
    string? FailureCategory,
    IReadOnlyList<LifecycleMarker> Markers,
    bool FailureMarkerDetected)
{
    public int InitializationCount => Markers.Count(marker => marker.Name == "THRONEFORGE_LIFECYCLE_INITIALIZED");
    public int QuittingCount => Markers.Count(marker => marker.Name == "THRONEFORGE_UNITY_QUITTING_OBSERVED");
    public int ShutdownCount => Markers.Count(marker => marker.Name == "THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED");
}

public static class LifecycleMarkerParser
{
    private static readonly HashSet<string> ExpectedKeys =
    [
        "nonce",
        "bindingId",
        "pluginGuid",
        "pluginVersion",
        "modId",
        "modVersion",
        "sequence",
        "apiIdentity",
        "contractsIdentity"
    ];

    private static readonly string[] ExpectedNames =
    [
        "THRONEFORGE_LIFECYCLE_INITIALIZED",
        "THRONEFORGE_UNITY_QUITTING_OBSERVED",
        "THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED"
    ];

    public static LifecycleMarkerParseResult Parse(
        string text,
        string expectedNonce,
        string expectedApiIdentity,
        string expectedContractsIdentity)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedNonce);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedApiIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContractsIdentity);

        var markers = new List<LifecycleMarker>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var markerLine = ExtractMarkerLine(line, out var markerTokenCount);
            if (markerTokenCount > 1)
            {
                return Invalid(LifecycleFailureCategories.InvalidMarker, markers, false);
            }

            if (markerTokenCount == 1 && markerLine.Length == 0)
            {
                return Invalid(LifecycleFailureCategories.InvalidMarker, markers, false);
            }

            var name = ExpectedNames.FirstOrDefault(candidate => markerLine.StartsWith(candidate + "|", StringComparison.Ordinal));
            if (name is null)
            {
                if (markerLine.StartsWith("THRONEFORGE_LIFECYCLE_FAILED|", StringComparison.Ordinal))
                {
                    return Invalid(LifecycleFailureCategories.InvalidMarker, markers, true);
                }

                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in markerLine.Split('|').Skip(1))
            {
                var separator = part.IndexOf('=');
                if (separator <= 0 || !values.TryAdd(part[..separator], part[(separator + 1)..]))
                {
                    return Invalid(LifecycleFailureCategories.InvalidMarker, markers, false);
                }
            }

            if (values.Count != ExpectedKeys.Count || values.Keys.Any(key => !ExpectedKeys.Contains(key)))
            {
                return Invalid(LifecycleFailureCategories.InvalidMarker, markers, false);
            }

            if (markers.Any(marker => marker.Name == name))
            {
                return Invalid(LifecycleFailureCategories.DuplicateMarker, markers, false);
            }

            if (!TryRead(values, "nonce", out var nonce)
                || !string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            {
                return Invalid(LifecycleFailureCategories.WrongNonce, markers, false);
            }

            var bindingId = string.Empty;
            var pluginGuid = string.Empty;
            var pluginVersion = string.Empty;
            var modId = string.Empty;
            var modVersion = string.Empty;
            var apiIdentity = string.Empty;
            var contractsIdentity = string.Empty;
            var bindingValid = TryRead(values, "bindingId", out bindingId)
                && string.Equals(bindingId, LifecycleBindingIds.ApplicationQuittingV1, StringComparison.Ordinal)
                && TryRead(values, "pluginGuid", out pluginGuid)
                && string.Equals(pluginGuid, "dev.throneforge.m1.lifecycle-smoke", StringComparison.Ordinal)
                && TryRead(values, "pluginVersion", out pluginVersion)
                && string.Equals(pluginVersion, "0.0.1", StringComparison.Ordinal)
                && TryRead(values, "modId", out modId)
                && string.Equals(modId, "dev.throneforge.m1.lifecycle-smoke", StringComparison.Ordinal)
                && TryRead(values, "modVersion", out modVersion)
                && string.Equals(modVersion, "0.0.1", StringComparison.Ordinal)
                && TryRead(values, "apiIdentity", out apiIdentity)
                && string.Equals(apiIdentity, expectedApiIdentity, StringComparison.Ordinal)
                && TryRead(values, "contractsIdentity", out contractsIdentity)
                && string.Equals(contractsIdentity, expectedContractsIdentity, StringComparison.Ordinal);
            var sequenceValid = TryReadInt(values, "sequence", out var sequence);
            if (!bindingValid)
            {
                return Invalid(LifecycleFailureCategories.RuntimeIdentityMismatch, markers, false);
            }

            if (!sequenceValid
                || sequence != markers.Count + 1
                || !string.Equals(name, ExpectedNames[markers.Count], StringComparison.Ordinal))
            {
                return Invalid(LifecycleFailureCategories.InvalidMarkerOrder, markers, false);
            }

            markers.Add(new(name, nonce, bindingId, pluginGuid, pluginVersion, modId, modVersion, sequence, apiIdentity, contractsIdentity));
        }

        if (markers.Count != ExpectedNames.Length || ExpectedNames.Any(name => markers.All(marker => marker.Name != name)))
        {
            return Invalid(LifecycleFailureCategories.MissingMarker, markers, false);
        }

        return new(true, null, markers.ToArray(), false);
    }

    private static string ExtractMarkerLine(string line, out int markerTokenCount)
    {
        var tokens = ExpectedNames.Append("THRONEFORGE_LIFECYCLE_FAILED").ToArray();
        markerTokenCount = tokens.Sum(token => CountToken(line, token));

        if (tokens.Any(token => line.StartsWith(token + "|", StringComparison.Ordinal)))
        {
            return line;
        }

        if (line.Length > 0 && line[0] == '[')
        {
            var closingBracket = line.IndexOf(']');
            if (closingBracket >= 0)
            {
                var payload = line[(closingBracket + 1)..].TrimStart();
                if (tokens.Any(token => payload.StartsWith(token + "|", StringComparison.Ordinal)))
                {
                    return payload;
                }
            }
        }

        return string.Empty;
    }

    private static int CountToken(string line, string token)
    {
        var count = 0;
        var offset = 0;
        while (offset < line.Length)
        {
            var index = line.IndexOf(token + "|", offset, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            offset = index + token.Length + 1;
        }

        return count;
    }

    private static bool TryRead(Dictionary<string, string> values, string key, out string value)
    {
        if (values.TryGetValue(key, out var found) && !string.IsNullOrWhiteSpace(found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadInt(Dictionary<string, string> values, string key, out int value)
    {
        if (values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
        return false;
    }

    private static LifecycleMarkerParseResult Invalid(string category, IReadOnlyList<LifecycleMarker> markers, bool failureMarker)
        => new(false, category, markers.ToArray(), failureMarker);
}

public sealed record LifecycleLogStabilityResult(bool IsStable, string? FailureCategory, string? Text);

public static class LifecycleLogStabilityObserver
{
    public static LifecycleLogStabilityResult Observe(string path, TimeSpan maximumWait, TimeSpan pollInterval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var deadline = Stopwatch.GetTimestamp() + (long)(maximumWait.TotalSeconds * Stopwatch.Frequency);
        (long Length, DateTime LastWriteUtc)? previous = null;
        var sawFile = false;
        var sawUnreadable = false;

        while (Stopwatch.GetTimestamp() <= deadline)
        {
            if (!File.Exists(path))
            {
                Thread.Sleep(pollInterval);
                continue;
            }

            sawFile = true;
            try
            {
                var info = new FileInfo(path);
                var current = (info.Length, info.LastWriteTimeUtc);
                if (previous is not null && previous.Value == current)
                {
                    try
                    {
                        return new(true, null, File.ReadAllText(path));
                    }
                    catch (IOException)
                    {
                        sawUnreadable = true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        sawUnreadable = true;
                    }
                }

                previous = current;
            }
            catch (UnauthorizedAccessException)
            {
                sawUnreadable = true;
            }
            catch (IOException)
            {
                sawUnreadable = true;
            }

            Thread.Sleep(pollInterval);
        }

        return new(false, sawFile ? sawUnreadable ? "log-not-readable" : "log-not-stable" : "log-missing", null);
    }
}
