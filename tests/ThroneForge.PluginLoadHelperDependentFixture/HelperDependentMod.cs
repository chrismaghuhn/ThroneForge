using ThroneForge.API;
using ThroneForge.PluginLoadHelperFixture;

namespace ThroneForge.PluginLoadHelperDependentFixture;

public sealed class HelperDependentMod : IThroneForgeMod
{
    public static string DependencyMarker => Helper.Value;

    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
