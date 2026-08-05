using ThroneForge.API;

namespace ThroneForge.PluginLoadFixture;

public sealed class SyntheticThroneForgeMod : IThroneForgeMod
{
    public SyntheticThroneForgeMod()
    {
        throw new InvalidOperationException("synthetic-constructor");
    }

    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("synthetic-initialize");
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("synthetic-shutdown");
    }
}
