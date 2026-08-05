using System;
using System.Reflection;
using System.Threading;
using BepInEx;
using ThroneForge.API;
using UnityEngine;
using ThroneForge.PluginSmokeTest;

namespace ThroneForge.M1.LifecycleSmoke;

[BepInPlugin("dev.throneforge.m1.lifecycle-smoke", "ThroneForge M1 Lifecycle Smoke", "0.0.1")]
public sealed class ThroneForgeLifecyclePlugin : BaseUnityPlugin
{
    private const string BindingId = "unity-application-quitting-v1";
    private const string PluginGuid = "dev.throneforge.m1.lifecycle-smoke";
    private const string PluginVersion = "0.0.1";
    private const string ModId = "dev.throneforge.m1.lifecycle-smoke";
    private const string ModVersion = "0.0.1";
    private LifecycleHost? _host;
    private int _quittingObserved;
    private int _subscribed;

    private void Awake()
    {
        try
        {
            var nonce = Environment.GetEnvironmentVariable("THRONEFORGE_SMOKE_NONCE");
            if (!IsSafeNonce(nonce))
            {
                Fail("invalid-nonce");
                return;
            }

            var apiIdentity = FormatIdentity(typeof(IThroneForgeMod).Assembly.GetName());
            var contractsIdentity = FormatIdentity(typeof(ModIdentity).Assembly.GetName());
            _host = new LifecycleHost(new SyntheticMod(), new SyntheticLifecycleContext());
            if (Interlocked.Exchange(ref _subscribed, 1) != 0)
            {
                Fail("duplicate-subscription");
                return;
            }

            Application.quitting += OnApplicationQuitting;
            _host.Initialize();
            LogMarker("THRONEFORGE_LIFECYCLE_INITIALIZED", nonce, apiIdentity, contractsIdentity, 1);
        }
        catch (LifecycleStateException exception)
        {
            Fail(exception.FailureCategory);
        }
        catch
        {
            Fail("lifecycle-initialization-failed");
        }
    }

    private void OnApplicationQuitting()
    {
        if (Interlocked.Exchange(ref _quittingObserved, 1) != 0)
        {
            Fail("duplicate-quitting-event");
            return;
        }

        var nonce = Environment.GetEnvironmentVariable("THRONEFORGE_SMOKE_NONCE");
        if (!IsSafeNonce(nonce) || _host is null)
        {
            Fail("quitting-before-initialization");
            return;
        }

        var apiIdentity = FormatIdentity(typeof(IThroneForgeMod).Assembly.GetName());
        var contractsIdentity = FormatIdentity(typeof(ModIdentity).Assembly.GetName());
        try
        {
            LogMarker("THRONEFORGE_UNITY_QUITTING_OBSERVED", nonce!, apiIdentity, contractsIdentity, 2);
            _host.ObserveApplicationQuitting();
            LogMarker("THRONEFORGE_LIFECYCLE_SHUTDOWN_COMPLETED", nonce, apiIdentity, contractsIdentity, 3);
            Unsubscribe();
        }
        catch (LifecycleStateException exception)
        {
            Fail(exception.FailureCategory);
        }
        catch
        {
            Fail("lifecycle-shutdown-failed");
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        _host?.Cleanup();
    }

    private void Unsubscribe()
    {
        if (Interlocked.Exchange(ref _subscribed, 0) == 1)
        {
            Application.quitting -= OnApplicationQuitting;
        }
    }

    private void LogMarker(string name, string? nonce, string apiIdentity, string contractsIdentity, int sequence)
        => Logger.LogInfo($"{name}|nonce={nonce}|bindingId={BindingId}|pluginGuid={PluginGuid}|pluginVersion={PluginVersion}|modId={ModId}|modVersion={ModVersion}|sequence={sequence}|apiIdentity={apiIdentity}|contractsIdentity={contractsIdentity}");

    private void Fail(string category)
        => Logger.LogError($"THRONEFORGE_LIFECYCLE_FAILED|category={category}");

    private static bool IsSafeNonce(string? nonce)
        => !string.IsNullOrWhiteSpace(nonce)
            && nonce.Length <= 128
            && nonce.IndexOfAny(['|', '\r', '\n', '\t', ' ']) < 0;

    private static string FormatIdentity(AssemblyName identity)
        => $"{identity.Name}, Version={identity.Version}";

    private sealed class SyntheticMod : IThroneForgeMod
    {
        private int _initialized;
        private int _shutdown;

        public ValueTask InitializeAsync(IModContext context, CancellationToken cancellationToken)
        {
            if (context is null
                || context.Identity.Id != ModId
                || context.Identity.Version != ModVersion
                || context.Capabilities.IsAvailable("throneforge.synthetic.lifecycle"))
            {
                throw new InvalidOperationException("invalid-lifecycle-context");
            }

            if (Interlocked.Exchange(ref _initialized, 1) != 0)
            {
                throw new InvalidOperationException("duplicate-initialization");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _initialized) != 1 || Interlocked.Exchange(ref _shutdown, 1) != 0)
            {
                throw new InvalidOperationException("invalid-lifecycle-shutdown");
            }

            return ValueTask.CompletedTask;
        }
    }
}
