using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public sealed record PluginSmokeLogSummary(
    LoaderLogSummary Loader,
    PluginSmokeMarkerParseResult Marker,
    string ExpectedBepInExVersion)
{
    public bool MeetsCriteria
        => string.Equals(Loader.BepInExVersion, ExpectedBepInExVersion, StringComparison.Ordinal)
            && Loader.PreloaderInitialized
            && Loader.ChainloaderInitialized
            && Loader.PluginsDiscovered == 1
            && Loader.FatalErrorCount == 0
            && Loader.ErrorCount == 0
            && Marker.IsValid
            && !Marker.LifecycleMarkerDetected;
}

public static class PluginSmokeLogParser
{
    public static PluginSmokeLogSummary Parse(
        string logText,
        string expectedNonce,
        string? expectedApiIdentity = null,
        string? expectedContractsIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(logText);
        var loader = LoaderLogParser.Parse(logText);
        var marker = PluginSmokeMarkerParser.Parse(
            logText,
            expectedNonce,
            expectedApiIdentity: expectedApiIdentity,
            expectedContractsIdentity: expectedContractsIdentity);
        return new(loader, marker, "5.4.23.5");
    }
}
