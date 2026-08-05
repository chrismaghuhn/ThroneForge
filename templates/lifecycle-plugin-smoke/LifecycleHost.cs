using System;
using System.Threading;
using System.Threading.Tasks;
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

public static class LifecycleFailureCategories
{
    public const string InvalidState = "invalid-state";
    public const string AsynchronousLifecycleNotSupported = "asynchronous-lifecycle-not-supported";
    public const string LifecycleInitializationFailed = "lifecycle-initialization-failed";
    public const string LifecycleShutdownFailed = "lifecycle-shutdown-failed";
    public const string LifecycleException = "lifecycle-exception";
    public const string InvalidMarker = "invalid-marker";
    public const string InvalidMarkerOrder = "invalid-marker-order";
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
            EnsureSynchronous(operation, LifecycleFailureCategories.LifecycleInitializationFailed);
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
            throw new LifecycleStateException(LifecycleFailureCategories.LifecycleInitializationFailed, "Synthetic lifecycle initialization failed.");
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
            EnsureSynchronous(operation, LifecycleFailureCategories.LifecycleShutdownFailed);
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
            throw new LifecycleStateException(LifecycleFailureCategories.LifecycleShutdownFailed, "Synthetic lifecycle shutdown failed.");
        }
    }

    public void Cleanup()
    {
        // Event unsubscription is owned by the Unity-facing host. This method is deliberately idempotent.
        _ = State;
    }

    private static void EnsureSynchronous(ValueTask operation, string failureCategory)
    {
        if (!operation.IsCompleted)
        {
            throw new LifecycleStateException(
                LifecycleFailureCategories.AsynchronousLifecycleNotSupported,
                "The synthetic lifecycle operation did not complete synchronously.");
        }

        try
        {
            operation.GetAwaiter().GetResult();
        }
        catch (LifecycleStateException)
        {
            throw;
        }
        catch
        {
            throw new LifecycleStateException(failureCategory, "Synthetic lifecycle operation failed.");
        }
    }

    private static void Fail(string category, string message)
        => throw new LifecycleStateException(category, message);
}
