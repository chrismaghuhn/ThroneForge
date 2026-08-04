using ThroneForge.API;

namespace ThroneForge.PluginLoadDuplicateFixture;

public sealed class FirstSyntheticThroneForgeMod : IThroneForgeMod
{
    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public sealed class SecondSyntheticThroneForgeMod : IThroneForgeMod
{
    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
