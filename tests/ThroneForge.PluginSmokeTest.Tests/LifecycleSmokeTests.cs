using System.Reflection;
using System.Threading.Tasks;
using ThroneForge.API;
using ThroneForge.Contracts;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleSmokeTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string ApiIdentity = "ThroneForge.API, Version=1.0.0.0";
    private const string ContractsIdentity = "ThroneForge.Contracts, Version=1.0.0.0";
    private static readonly int[] ExpectedSequence = [1, 2, 3];

    [Fact]
    public void PublicApplicationQuittingContractIsAccepted()
    {
        var result = UnityLifecycleMetadataValidator.Validate(new UnityLifecycleContractModel(
            HasApplicationType: true,
            HasQuittingEvent: true,
            HandlerType: "System.Action",
            AddIsPublic: true,
            RemoveIsPublic: true,
            AddIsStatic: true,
            RemoveIsStatic: true));

        Assert.True(result.IsValid);
        Assert.Equal("unity-application-quitting-v1", result.BindingId);
    }

    [Theory]
    [MemberData(nameof(InvalidUnityContracts))]
    public void InvalidPublicApplicationContractsAreRejected(UnityLifecycleContractModel model)
        => Assert.False(UnityLifecycleMetadataValidator.Validate(model).IsValid);

    public static IEnumerable<object[]> InvalidUnityContracts()
    {
        yield return new object[] { new UnityLifecycleContractModel(false, true, "System.Action", true, true, true, true) };
        yield return new object[] { new UnityLifecycleContractModel(true, false, "System.Action", true, true, true, true) };
        yield return new object[] { new UnityLifecycleContractModel(true, true, "System.EventHandler", true, true, true, true) };
        yield return new object[] { new UnityLifecycleContractModel(true, true, "System.Action", false, true, true, true) };
        yield return new object[] { new UnityLifecycleContractModel(true, true, "System.Action", true, true, false, true) };
    }

    [Fact]
    public void SyntheticContextHasExactIdentityAndNoCapabilities()
    {
        var context = new SyntheticLifecycleContext();

        Assert.Equal("dev.throneforge.m1.lifecycle-smoke", context.Identity.Id);
        Assert.Equal("0.0.1", context.Identity.Version);
        Assert.False(context.Capabilities.IsAvailable("anything"));
        Assert.False(context.Capabilities.IsAvailable(string.Empty));
    }

    [Fact]
    public void LifecycleHostInitializesAndShutsDownExactlyOnce()
    {
        var mod = new RecordingMod();
        var host = new LifecycleHost(mod, new SyntheticLifecycleContext(), _ => { });

        host.Initialize();
        host.ObserveApplicationQuitting();

        Assert.Equal(LifecycleHostState.ShutdownCompleted, host.State);
        Assert.Equal(1, mod.InitializeCount);
        Assert.Equal(1, mod.ShutdownCount);
        Assert.Throws<LifecycleStateException>(() => host.Initialize());
        Assert.Throws<LifecycleStateException>(() => host.ObserveApplicationQuitting());
    }

    [Fact]
    public void ShutdownBeforeInitializationFailsClosed()
    {
        var host = new LifecycleHost(new RecordingMod(), new SyntheticLifecycleContext(), _ => { });

        Assert.Throws<LifecycleStateException>(() => host.ObserveApplicationQuitting());
        Assert.Equal(LifecycleHostState.Faulted, host.State);
    }

    [Fact]
    public void LifecycleExceptionsTransitionToFaulted()
    {
        var host = new LifecycleHost(new ThrowingMod(), new SyntheticLifecycleContext(), _ => { });

        Assert.Throws<LifecycleStateException>(() => host.Initialize());
        Assert.Equal(LifecycleHostState.Faulted, host.State);
    }

    [Fact]
    public void ShutdownExceptionTransitionsToFaulted()
    {
        var host = new LifecycleHost(new ShutdownThrowingMod(), new SyntheticLifecycleContext(), _ => { });

        host.Initialize();
        Assert.Throws<LifecycleStateException>(() => host.ObserveApplicationQuitting());
        Assert.Equal(LifecycleHostState.Faulted, host.State);
    }

    [Fact]
    public void IncompleteAsyncLifecycleIsRejected()
    {
        var host = new LifecycleHost(new IncompleteMod(), new SyntheticLifecycleContext(), _ => { });

        var exception = Assert.Throws<LifecycleStateException>(() => host.Initialize());
        Assert.Equal("asynchronous-lifecycle-not-supported", exception.FailureCategory);
    }

    [Fact]
    public void IncompleteAsyncShutdownIsRejected()
    {
        var host = new LifecycleHost(new IncompleteShutdownMod(), new SyntheticLifecycleContext(), _ => { });

        host.Initialize();
        var exception = Assert.Throws<LifecycleStateException>(() => host.ObserveApplicationQuitting());
        Assert.Equal("asynchronous-lifecycle-not-supported", exception.FailureCategory);
        Assert.Equal(LifecycleHostState.Faulted, host.State);
    }

    [Fact]
    public void MarkerParserRequiresNonceAndExactSequence()
    {
        var text = string.Join(Environment.NewLine,
            Marker("THRONEFORGE_LIFECYCLE_INITIALIZED", 1),
            Marker("THRONEFORGE_UNITY_QUITTING_OBSERVED", 2),
            Marker("THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED", 3));

        var result = LifecycleMarkerParser.Parse(text, Nonce, ApiIdentity, ContractsIdentity);

        Assert.True(result.IsValid);
        Assert.Equal(ExpectedSequence, result.Markers.Select(marker => marker.Sequence));
    }

    [Theory]
    [InlineData("THRONEFORGE_LIFECYCLE_INITIALIZED", 1)]
    [InlineData("THRONEFORGE_UNITY_QUITTING_OBSERVED", 2)]
    [InlineData("THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED", 3)]
    public void DuplicateMarkersAreRejected(string markerName, int sequence)
    {
        var text = string.Join(Environment.NewLine,
            Marker("THRONEFORGE_LIFECYCLE_INITIALIZED", 1),
            Marker("THRONEFORGE_UNITY_QUITTING_OBSERVED", 2),
            Marker("THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED", 3),
            Marker(markerName, sequence));

        Assert.False(LifecycleMarkerParser.Parse(text, Nonce, ApiIdentity, ContractsIdentity).IsValid);
    }

    [Fact]
    public void WrongNonceAndRuntimeIdentityAreRejected()
    {
        var text = Marker("THRONEFORGE_LIFECYCLE_INITIALIZED", 1, nonce: "wrong");
        var result = LifecycleMarkerParser.Parse(text, Nonce, ApiIdentity, ContractsIdentity);

        Assert.False(result.IsValid);
        Assert.DoesNotContain("C:\\", result.FailureCategory ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ThroneForge.Other, Version=1.0.0.0", "ThroneForge.Contracts, Version=1.0.0.0")]
    [InlineData("ThroneForge.API, Version=1.0.0.0", "ThroneForge.Other, Version=1.0.0.0")]
    public void RuntimeApiAndContractsIdentityMismatchesAreRejected(string api, string contracts)
    {
        var text = string.Join(Environment.NewLine,
            Marker("THRONEFORGE_LIFECYCLE_INITIALIZED", 1, api: api, contracts: contracts),
            Marker("THRONEFORGE_UNITY_QUITTING_OBSERVED", 2, api: api, contracts: contracts),
            Marker("THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED", 3, api: api, contracts: contracts));

        Assert.False(LifecycleMarkerParser.Parse(text, Nonce, ApiIdentity, ContractsIdentity).IsValid);
    }

    [Fact]
    public void FailureMarkerCannotSatisfyLifecycleEvidence()
    {
        var result = LifecycleMarkerParser.Parse(
            "THRONEFORGE_LIFECYCLE_FAILED|category=lifecycle-initialization-failed",
            Nonce,
            ApiIdentity,
            ContractsIdentity);

        Assert.False(result.IsValid);
        Assert.True(result.FailureMarkerDetected);
    }

    [Fact]
    public void LifecyclePackageShapeIsExact()
    {
        Assert.Equal("ThroneForge.M1.LifecycleSmoke", LifecyclePluginPackageService.PrimaryAssemblyName);
        Assert.Equal("dev.throneforge.m1.lifecycle-smoke", LifecyclePluginPackageService.PluginGuid);
        Assert.Equal(3, LifecyclePluginPackageService.ExpectedPackagePaths.Length);
        Assert.Contains("ThroneForge.M1.LifecycleSmoke.dll", LifecyclePluginPackageService.ExpectedPackagePaths);
        Assert.Contains("ThroneForge.API.dll", LifecyclePluginPackageService.ExpectedPackagePaths);
        Assert.Contains("ThroneForge.Contracts.dll", LifecyclePluginPackageService.ExpectedPackagePaths);
    }

    [Fact]
    public void OnDestroyFallbackAloneCannotPass()
    {
        var result = LifecycleMarkerParser.Parse(
            Marker("THRONEFORGE_LIFECYCLE_INITIALIZED", 1) + Environment.NewLine +
            Marker("THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED", 3),
            Nonce,
            ApiIdentity,
            ContractsIdentity);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void StableLogObservationIsBounded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"throneforge-lifecycle-{Guid.NewGuid():N}.log");
        try
        {
            File.WriteAllText(path, "stable");
            var result = LifecycleLogStabilityObserver.Observe(path, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(25));

            Assert.True(result.IsStable);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingLogHasStableFailureCategory()
    {
        var result = LifecycleLogStabilityObserver.Observe(
            Path.Combine(Path.GetTempPath(), "throneforge-lifecycle-missing.log"),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(10));

        Assert.False(result.IsStable);
        Assert.Equal("log-missing", result.FailureCategory);
    }

    private static string Marker(string name, int sequence, string? nonce = null, string? api = null, string? contracts = null)
        => $"{name}|nonce={nonce ?? Nonce}|bindingId=unity-application-quitting-v1|pluginGuid=dev.throneforge.m1.lifecycle-smoke|pluginVersion=0.0.1|modId=dev.throneforge.m1.lifecycle-smoke|modVersion=0.0.1|sequence={sequence}|apiIdentity={api ?? ApiIdentity}|contractsIdentity={contracts ?? ContractsIdentity}";

    private sealed class RecordingMod : IThroneForgeMod
    {
        public int InitializeCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
        {
            InitializeCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            ShutdownCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingMod : IThroneForgeMod
    {
        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("synthetic");

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("synthetic");
    }

    private sealed class ShutdownThrowingMod : IThroneForgeMod
    {
        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("synthetic");
    }

    private sealed class IncompleteMod : IThroneForgeMod
    {
        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
            => new(new TaskCompletionSource().Task);

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
            => new(new TaskCompletionSource().Task);
    }

    private sealed class IncompleteShutdownMod : IThroneForgeMod
    {
        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
            => new(new TaskCompletionSource().Task);
    }
}
