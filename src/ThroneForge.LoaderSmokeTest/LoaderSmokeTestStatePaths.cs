namespace ThroneForge.LoaderSmokeTest;

/// <summary>
/// Owns the persisted Task-3 loader-smoke-test state paths shared by all consumers.
/// </summary>
public static class LoaderSmokeTestStatePaths
{
    public const string BaselineFileName = "baseline-copy-manifest.json";
    public const string TransactionStateFileName = "transaction-state.json";

    public static string GetBaselinePath(SmokeTestRoots roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return Path.Combine(roots.ManifestsRoot, BaselineFileName);
    }

    public static string GetTransactionStatePath(SmokeTestRoots roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return Path.Combine(roots.ManifestsRoot, TransactionStateFileName);
    }
}
