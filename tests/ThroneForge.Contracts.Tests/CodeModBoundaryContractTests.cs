using System.Reflection;
using ThroneForge.API;
using ThroneForge.Contracts;
using Xunit;

namespace ThroneForge.Contracts.Tests;

public sealed class CodeModBoundaryContractTests
{
    [Fact]
    public void DescriptorNormalizesPackageHashWithoutStoringAPath()
    {
        var descriptor = new CodeModDescriptor(
            new ModIdentity("dev.example.mod", "1.0.0"),
            "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789");

        Assert.Equal(
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            descriptor.PackageSha256);
        Assert.DoesNotContain("\\", descriptor.PackageSha256, StringComparison.Ordinal);
        Assert.DoesNotContain("/", descriptor.PackageSha256, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(CodeModDescriptor).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(string)
                && property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActivationRequestPreservesOnlyPortableFacts()
    {
        var descriptor = new CodeModDescriptor(
            new ModIdentity("dev.example.mod", "1.0.0"),
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789");
        var request = new CodeModActivationRequest(
            descriptor,
            new GameFingerprint("game-fingerprint"),
            AdapterCompatibility.Supported,
            packageIntegrityVerified: true,
            explicitApproval: false);

        Assert.Equal(descriptor, request.Mod);
        Assert.Equal("game-fingerprint", request.GameFingerprint.Value);
        Assert.True(request.PackageIntegrityVerified);
        Assert.False(request.ExplicitApproval);
    }

    [Fact]
    public void PublicModContractUsesPortableLifecycleOnly()
    {
        var initialize = typeof(IThroneForgeMod).GetMethod(nameof(IThroneForgeMod.InitializeAsync));
        var shutdown = typeof(IThroneForgeMod).GetMethod(nameof(IThroneForgeMod.ShutdownAsync));

        Assert.NotNull(initialize);
        Assert.NotNull(shutdown);
        Assert.Equal(typeof(ValueTask), initialize!.ReturnType);
        Assert.Equal(typeof(ValueTask), shutdown!.ReturnType);
        Assert.DoesNotContain(
            typeof(IThroneForgeMod).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "UnityEngine" or "BepInEx" or "HarmonyLib" or "Assembly-CSharp");
    }
}
