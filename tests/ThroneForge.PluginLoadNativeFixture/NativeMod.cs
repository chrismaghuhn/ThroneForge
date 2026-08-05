using System.Runtime.InteropServices;
using ThroneForge.API;

namespace ThroneForge.PluginLoadNativeFixture;

public sealed class NativeMod : IThroneForgeMod
{
    [DllImport("synthetic-native-dependency", EntryPoint = "Probe")]
    private static extern int Probe();

    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
