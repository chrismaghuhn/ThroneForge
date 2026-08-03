using ThroneForge.Contracts;

namespace ThroneForge.GameAdapter.Abstractions;

public interface IGameAdapter
{
    GameFingerprint Fingerprint { get; }

    AdapterCompatibility Compatibility { get; }

    AdapterCapabilities Capabilities { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    Task ShutdownAsync(CancellationToken cancellationToken);
}

public interface IGameCatalogProvider
{
    Task<GameCatalog> ExportAsync(CancellationToken cancellationToken);
}

public interface IWaveRuntimeBridge
{
    WaveValidationResult ValidateForRuntime(WaveDefinition definition);

    Task<WaveHandle> RegisterAsync(WaveDefinition definition, CancellationToken cancellationToken);
}

public interface IGameLifecycleSource
{
    IDisposable Subscribe(IGameLifecycleObserver observer);
}

public interface IGameLifecycleObserver;

