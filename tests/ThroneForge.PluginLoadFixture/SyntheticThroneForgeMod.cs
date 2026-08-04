using ThroneForge.API;

namespace ThroneForge.PluginLoadFixture;

public sealed class SyntheticThroneForgeMod : IThroneForgeMod
{
    public static int ConstructorCalls { get; private set; }

    public static int InitializeCalls { get; private set; }

    public static int ShutdownCalls { get; private set; }

    public SyntheticThroneForgeMod()
    {
        ConstructorCalls++;
    }

    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
    {
        InitializeCalls++;
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken)
    {
        ShutdownCalls++;
        return ValueTask.CompletedTask;
    }
}
