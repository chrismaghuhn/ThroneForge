using ThroneForge.API;

namespace ThroneForge.PluginLoadInvalidShapesFixture;

internal sealed class InternalMod : IThroneForgeMod
{
    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public abstract class AbstractMod : IThroneForgeMod
{
    public abstract ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken);

    public abstract ValueTask ShutdownAsync(CancellationToken cancellationToken);
}

public sealed class OpenGenericMod<T> : IThroneForgeMod
{
    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public static class NestedHost
{
    public sealed class NestedMod : IThroneForgeMod
    {
        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
