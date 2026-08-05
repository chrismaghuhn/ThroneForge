using BepInEx;
using ThroneForge.API;
using ThroneForge.Contracts;

namespace ThroneForge.M1.SyntheticSmoke;

[BepInPlugin("dev.throneforge.m1.synthetic-smoke", "ThroneForge M1 Synthetic Smoke", "0.0.1")]
public sealed class ThroneForgeSyntheticPlugin : BaseUnityPlugin, IThroneForgeMod
{
    private const string PluginGuid = "dev.throneforge.m1.synthetic-smoke";
    private const string PluginVersion = "0.0.1";
    private const string ApiIdentity = "__THRONEFORGE_API_IDENTITY__";
    private const string ContractsIdentity = "__THRONEFORGE_CONTRACTS_IDENTITY__";

    private void Awake()
    {
        var nonce = Environment.GetEnvironmentVariable("THRONEFORGE_SMOKE_NONCE");
        if (string.IsNullOrWhiteSpace(nonce) || nonce.Any(char.IsControl) || nonce.Any(char.IsWhiteSpace))
        {
            Logger.LogError("THRONEFORGE_SYNTHETIC_PLUGIN_NONCE_INVALID");
            return;
        }

        Logger.LogInfo(
            $"THRONEFORGE_SYNTHETIC_PLUGIN_READY|nonce={nonce}|pluginGuid={PluginGuid}|pluginVersion={PluginVersion}|api={ApiIdentity}|contracts={ContractsIdentity}");
    }

    public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("THRONEFORGE_SYNTHETIC_PLUGIN_LIFECYCLE_INVOKED");

    public ValueTask ShutdownAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException("THRONEFORGE_SYNTHETIC_PLUGIN_LIFECYCLE_INVOKED");
}
