using System.Runtime.CompilerServices;
using ThroneForge.API;

namespace ThroneForge.PluginLoadModuleInitializerFixture;

internal static class SyntheticModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        throw new InvalidOperationException("synthetic-module-initializer");
    }
}

public sealed class ModuleInitializerMod : IThroneForgeMod
{
    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
