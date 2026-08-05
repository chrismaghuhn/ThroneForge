using System.Diagnostics;
using ThroneForge.API;
using ThroneForge.Contracts;

namespace ThroneForge.PluginSmokeTest;

public enum LifecycleHostState
{
    Created = 0,
    Initializing,
    Initialized,
    ShutdownRequested,
    ShutdownCompleted,
    Faulted
}

public static class LifecycleBindingIds
{
    public const string ApplicationQuittingV1 = "unity-application-quitting-v1";
}

public static class LifecycleFailureCategories
{
    public const string InvalidState = "invalid-state";
    public const string AsynchronousLifecycleNotSupported = "asynchronous-lifecycle-not-supported";
    public const string LifecycleException = "lifecycle-exception";
    public const string InvalidMarker = "invalid-marker";
    public const string DuplicateMarker = "duplicate-marker";
    public const string MissingMarker = "missing-marker";
    public const string WrongNonce = "wrong-nonce";
    public const string RuntimeIdentityMismatch = "runtime-identity-mismatch";
}

public sealed class LifecycleStateException : Exception
{
    public LifecycleStateException(string failureCategory, string message)
        : base(message)
    {
        FailureCategory = failureCategory;
    }

    public string FailureCategory { get; }
}

public sealed class SyntheticLifecycleContext : IModContext
{
    public SyntheticLifecycleContext()
    {
        Identity = new ModIdentity("dev.throneforge.m1.lifecycle-smoke", "0.0.1");
        Capabilities = new NoCapabilities();
    }

    public ModIdentity Identity { get; }

    public ICapabilityService Capabilities { get; }

    private sealed class NoCapabilities : ICapabilityService
    {
        public bool IsAvailable(string capabilityKey) => false;
    }
}

public sealed class LifecycleHost
{
    private readonly IThroneForgeMod _mod;
    private readonly IModContext _context;
    private int _state = (int)LifecycleHostState.Created;

    public LifecycleHost(IThroneForgeMod mod, IModContext context, Action<string>? markerSink = null)
    {
        _mod = mod ?? throw new ArgumentNullException(nameof(mod));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        MarkerSink = markerSink ?? (_ => { });
    }

    public LifecycleHostState State => (LifecycleHostState)Volatile.Read(ref _state);

    public Action<string> MarkerSink { get; }

    public void Initialize(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _state, (int)LifecycleHostState.Initializing, (int)LifecycleHostState.Created)
            != (int)LifecycleHostState.Created)
        {
            Volatile.Write(ref _state, (int)LifecycleHostState.Faulted);
            Fail(LifecycleFailureCategories.InvalidState, "Lifecycle initialization was requested more than once.");
        }

        try
        {
            var operation = _mod.InitializeAsync(_context, cancellationToken);
            EnsureSynchronous(operation);
            Volatile.Write(ref _state, (int)LifecycleHostState.Initialized);
        }
        catch (LifecycleStateException)
        {
            Volatile.Write(ref _state, (int)LifecycleHostState.Faulted);
            throw;
        }
        catch
        {
            Volatile.Write(ref _state, (int)LifecycleHostState.Faulted);
            throw new LifecycleStateException(LifecycleFailureCategories.LifecycleException, "Synthetic lifecycle initialization failed.");
        }
    }

    public void ObserveApplicationQuitting(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _state, (int)LifecycleHostState.ShutdownRequested, (int)LifecycleHostState.Initialized)
            != (int)LifecycleHostState.Initialized)
        {
            Volatile.Write(ref _state, (int)LifecycleHostState.Faulted);
            Fail(LifecycleFailureCategories.InvalidState, "Unity quitting was observed in an invalid lifecycle state.");
        }

        try
        {
            var operation = _mod.ShutdownAsync(cancellationToken);
            EnsureSynchronous(operation);
            Volatile.Write(ref _state, (int)LifecycleHostState.ShutdownCompleted);
        }
        catch (LifecycleStateException)
        {
            Volatile.Write(ref _state, (int)LifecycleHostState.Faulted);
            throw;
        }
        catch
        {
            Volatile.Write(ref _state, (int)LifecycleHostState.Faulted);
            throw new LifecycleStateException(LifecycleFailureCategories.LifecycleException, "Synthetic lifecycle shutdown failed.");
        }
    }

    public void Cleanup()
    {
        // Event unsubscription is owned by the Unity-facing host. This method is deliberately idempotent.
        _ = State;
    }

    private static void EnsureSynchronous(ValueTask operation)
    {
        var task = operation.AsTask();
        if (!task.IsCompletedSuccessfully)
        {
            throw new LifecycleStateException(
                LifecycleFailureCategories.AsynchronousLifecycleNotSupported,
                "The synthetic lifecycle operation did not complete synchronously.");
        }

        task.GetAwaiter().GetResult();
    }

    private static void Fail(string category, string message)
        => throw new LifecycleStateException(category, message);
}

public sealed record UnityLifecycleContractModel(
    bool HasApplicationType,
    bool HasQuittingEvent,
    string HandlerType,
    bool AddIsPublic,
    bool RemoveIsPublic,
    bool AddIsStatic,
    bool RemoveIsStatic);

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

        if (!model.HasApplicationType || !model.HasQuittingEvent)
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
            var name = ExpectedNames.FirstOrDefault(candidate => line.StartsWith(candidate + "|", StringComparison.Ordinal));
            if (name is null)
            {
                if (line.StartsWith("THRONEFORGE_LIFECYCLE_FAILED|", StringComparison.Ordinal))
                {
                    return Invalid(LifecycleFailureCategories.InvalidMarker, markers, true);
                }

                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in line.Split('|').Skip(1))
            {
                var separator = part.IndexOf('=');
                if (separator <= 0 || !values.TryAdd(part[..separator], part[(separator + 1)..]))
                {
                    return Invalid(LifecycleFailureCategories.InvalidMarker, markers, false);
                }
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

            if (!TryRead(values, "bindingId", out var bindingId)
                || !string.Equals(bindingId, LifecycleBindingIds.ApplicationQuittingV1, StringComparison.Ordinal)
                || !TryRead(values, "pluginGuid", out var pluginGuid)
                || !string.Equals(pluginGuid, "dev.throneforge.m1.lifecycle-smoke", StringComparison.Ordinal)
                || !TryRead(values, "pluginVersion", out var pluginVersion)
                || !string.Equals(pluginVersion, "0.0.1", StringComparison.Ordinal)
                || !TryRead(values, "modId", out var modId)
                || !string.Equals(modId, "dev.throneforge.m1.lifecycle-smoke", StringComparison.Ordinal)
                || !TryRead(values, "modVersion", out var modVersion)
                || !string.Equals(modVersion, "0.0.1", StringComparison.Ordinal)
                || !TryRead(values, "apiIdentity", out var apiIdentity)
                || !string.Equals(apiIdentity, expectedApiIdentity, StringComparison.Ordinal)
                || !TryRead(values, "contractsIdentity", out var contractsIdentity)
                || !string.Equals(contractsIdentity, expectedContractsIdentity, StringComparison.Ordinal)
                || !TryReadInt(values, "sequence", out var sequence)
                || sequence != Array.IndexOf(ExpectedNames, name) + 1)
            {
                return Invalid(LifecycleFailureCategories.RuntimeIdentityMismatch, markers, false);
            }

            markers.Add(new(name, nonce, bindingId, pluginGuid, pluginVersion, modId, modVersion, sequence, apiIdentity, contractsIdentity));
        }

        if (markers.Count != ExpectedNames.Length || ExpectedNames.Any(name => markers.All(marker => marker.Name != name)))
        {
            return Invalid(LifecycleFailureCategories.MissingMarker, markers, false);
        }

        return new(true, null, markers.OrderBy(marker => marker.Sequence).ToArray(), false);
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
