using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ThroneForge.Contracts;
using ThroneForge.PluginLoadTest;
using ThroneForge.Runtime;
using Xunit;

namespace ThroneForge.PluginLoadTest.Tests;

public sealed class PluginLoadProbeTests
{
    [Fact]
    public void MatchingBoundFixtureLoadsWithoutExplicitlyInvokingPluginCode()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(artifactPath);

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Loaded, result.Status);
        Assert.Equal("ThroneForge.PluginLoadFixture", result.AssemblyName);
        Assert.Equal(
            "ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod",
            Assert.Single(result.ImplementedContractTypes));
        Assert.Equal(PluginUnloadStatus.UnloadObserved, result.UnloadStatus);
        Assert.True(result.UnloadRequested);
        Assert.Contains("full-trust", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("without invoking plugin code", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuccessfulResultRecordsTheSingleAssemblyClosure()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.NotNull(result.ClosureEvidence);
        Assert.Equal(CreateHash(artifactPath).ToLowerInvariant(), result.ClosureEvidence!.PrimaryArtifactSha256.Value);
        Assert.Contains(result.ClosureEvidence.SharedAssemblyIdentities, identity => identity.StartsWith("ThroneForge.API,", StringComparison.Ordinal));
        Assert.Contains(result.ClosureEvidence.SharedAssemblyIdentities, identity => identity.StartsWith("ThroneForge.Contracts,", StringComparison.Ordinal));
        Assert.Empty(result.ClosureEvidence.NonPlatformAssemblyReferences);
        Assert.False(result.ClosureEvidence.NativeDependenciesDetected);
    }

    [Fact]
    public void HelperDependentArtifactIsRejectedBeforeAssemblyLoad()
    {
        var artifactPath = FixturePath("ThroneForge.PluginLoadHelperDependentFixture");

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ManagedDependencyNotAllowed, result.ReasonCode);
        Assert.Null(result.AssemblyName);
        Assert.DoesNotContain(artifactPath, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeDependencyIsRejectedDuringMetadataPreflight()
    {
        var artifactPath = FixturePath("ThroneForge.PluginLoadNativeFixture");

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(PluginLoadReasonCodes.NativeDependencyNotAllowed, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void ModuleInitializerIsRejectedBeforeLoad()
    {
        var artifactPath = FixturePath("ThroneForge.PluginLoadModuleInitializerFixture");

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ModuleInitializerNotAllowed, result.ReasonCode);
        Assert.Null(result.AssemblyName);
        Assert.DoesNotContain(artifactPath, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidContractShapesAreRejectedWithStableIssues()
    {
        var artifactPath = FixturePath("ThroneForge.PluginLoadInvalidShapesFixture");

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Failed, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ContractInvalid, result.ReasonCode);
        Assert.Contains(PluginContractIssueCodes.Internal, result.ContractIssues);
        Assert.Contains(PluginContractIssueCodes.Nested, result.ContractIssues);
        Assert.Contains(PluginContractIssueCodes.Abstract, result.ContractIssues);
        Assert.Contains(PluginContractIssueCodes.OpenGeneric, result.ContractIssues);
        Assert.Equal(PluginUnloadStatus.UnloadObserved, result.UnloadStatus);
    }

    [Fact]
    public void InternalContractImplementationIsRejected()
    {
        var result = PluginLoadProbe.Load(CreateRequest(FixturePath("ThroneForge.PluginLoadInvalidShapesFixture")));

        Assert.Equal(PluginLoadReasonCodes.ContractInvalid, result.ReasonCode);
        Assert.Contains(PluginContractIssueCodes.Internal, result.ContractIssues);
    }

    [Fact]
    public void NestedContractImplementationIsRejected()
    {
        var result = PluginLoadProbe.Load(CreateRequest(FixturePath("ThroneForge.PluginLoadInvalidShapesFixture")));

        Assert.Equal(PluginLoadReasonCodes.ContractInvalid, result.ReasonCode);
        Assert.Contains(PluginContractIssueCodes.Nested, result.ContractIssues);
    }

    [Fact]
    public void AbstractAndOpenGenericContractImplementationsAreRejected()
    {
        var result = PluginLoadProbe.Load(CreateRequest(FixturePath("ThroneForge.PluginLoadInvalidShapesFixture")));

        Assert.Equal(PluginLoadReasonCodes.ContractInvalid, result.ReasonCode);
        Assert.Contains(PluginContractIssueCodes.Abstract, result.ContractIssues);
        Assert.Contains(PluginContractIssueCodes.OpenGeneric, result.ContractIssues);
    }

    [Fact]
    public void DuplicateApiIdentityIsRejectedBeforeLoad()
    {
        var artifactPath = FixturePath("ThroneForge.PluginLoadDuplicateApiFixture");

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ManagedDependencyNotAllowed, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void DuplicateContractsIdentityIsRejectedBeforeLoad()
    {
        var artifactPath = FixturePath("ThroneForge.PluginLoadDuplicateContractsFixture");

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ManagedDependencyNotAllowed, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void CapturedBytesRemainTheLoadInputAfterTheDiskFileChanges()
    {
        var sourcePath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var tempPath = Path.Combine(Path.GetTempPath(), $"throneforge-captured-{Guid.NewGuid():N}.dll");
        File.Copy(sourcePath, tempPath);

        try
        {
            var capture = PluginLoadProbe.CaptureArtifact(tempPath);
            File.WriteAllBytes(tempPath, new byte[] { 0x4D, 0x5A, 0x00, 0x01 });

            var result = PluginLoadProbe.Load(CreateRequest(tempPath, packageHashOverride: capture.Sha256.Value), capture);

            Assert.Equal(PluginLoadStatus.Loaded, result.Status);
            Assert.Equal(PluginUnloadStatus.UnloadObserved, result.UnloadStatus);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void RetainedAssemblyReferenceReportsBoundedUnloadFailure()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var retained = RetainedAssembly.Load(File.ReadAllBytes(artifactPath));

        try
        {
            Assert.Equal(
                PluginUnloadStatus.UnloadNotObservedWithinBound,
                PluginLoadProbe.ObserveUnload(retained.ContextReference));
        }
        finally
        {
            retained.Release();
        }

        Assert.Equal(PluginUnloadStatus.UnloadObserved, PluginLoadProbe.ObserveUnload(retained.ContextReference));
    }

    [Fact]
    public void MissingApprovalIsRejectedBeforeLoading()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(artifactPath, includeApproval: false);

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.ApprovalRequired, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void ChangedArtifactIsRejectedBeforeLoading()
    {
        var sourcePath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var tempPath = Path.Combine(Path.GetTempPath(), $"throneforge-plugin-{Guid.NewGuid():N}.dll");
        File.Copy(sourcePath, tempPath);

        try
        {
            File.AppendAllText(tempPath, "changed");
            var request = CreateRequest(tempPath, packageHashOverride: CreateHash(sourcePath));

            var result = PluginLoadProbe.Load(request);

            Assert.Equal(PluginLoadStatus.Rejected, result.Status);
            Assert.Equal(CodeModAdmissionReasonCodes.IntegrityNotVerified, result.ReasonCode);
            Assert.Null(result.AssemblyName);
            Assert.DoesNotContain(tempPath, result.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void ApprovalForAnotherGameFingerprintIsRejectedBeforeLoading()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(
            artifactPath,
            approval: CreateApproval(
                new ModIdentity("dev.example.synthetic", "1.0.0"),
                CreateHash(artifactPath),
                new GameFingerprint("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")));

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.ApprovalMismatch, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void CompatibilityForAnotherGameFingerprintIsRejectedBeforeLoading()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var fingerprint = new GameFingerprint("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        var request = CreateRequest(
            artifactPath,
            compatibility: new AdapterCompatibilityEvidence(
                new GameFingerprint("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                "thronefall.adapter",
                "1.0.0",
                AdapterCompatibility.Supported));

        Assert.Equal(fingerprint.Value, request.GameFingerprint.Value);
        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.CompatibilityFingerprintMismatch, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void UnsupportedCompatibilityIsRejectedBeforeLoading()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(
            artifactPath,
            compatibility: new AdapterCompatibilityEvidence(
                new GameFingerprint("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                "thronefall.adapter",
                "1.0.0",
                AdapterCompatibility.SupportedWithWarnings));

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.CompatibilityUnsupported, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void UnknownCompatibilityIsRejectedBeforeLoading()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(
            artifactPath,
            compatibility: new AdapterCompatibilityEvidence(
                CreateFingerprint(),
                "thronefall.adapter",
                "1.0.0",
                (AdapterCompatibility)int.MaxValue));

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.CompatibilityUnsupported, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void DeniedApprovalIsRejectedBeforeLoading()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(
            artifactPath,
            approval: CreateApproval(
                CreateIdentity(),
                CreateHash(artifactPath),
                CreateFingerprint(),
                CodeModApprovalDecision.Denied));

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Rejected, result.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.ApprovalMismatch, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void ArtifactWithoutContractIsRejectedAfterAdmission()
    {
        var artifactPath = typeof(ThroneForge.API.IThroneForgeMod).Assembly.Location;
        var request = CreateRequest(artifactPath);

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Failed, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ContractMissing, result.ReasonCode);
        Assert.NotNull(result.Binding);
    }

    [Fact]
    public void MultipleContractImplementationsAreRejected()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadDuplicateFixture.FirstSyntheticThroneForgeMod).Assembly.Location;
        var request = CreateRequest(artifactPath);

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Failed, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ContractAmbiguous, result.ReasonCode);
        Assert.Equal(2, result.ImplementedContractTypes.Count);
    }

    [Fact]
    public void MalformedAssemblyIsRejectedWithoutRawPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"throneforge-malformed-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A, 0x00, 0x01, 0x02 });

        try
        {
            var result = PluginLoadProbe.Load(CreateRequest(path));

            Assert.Equal(PluginLoadStatus.Failed, result.Status);
            Assert.Equal(PluginLoadReasonCodes.AssemblyLoadFailed, result.ReasonCode);
            Assert.DoesNotContain(path, result.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MalformedRequestIsRejectedWithoutThrowing()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var valid = CreateRequest(artifactPath);
        var malformed = new PluginLoadRequest(
            artifactPath,
            null!,
            valid.GameFingerprint,
            valid.Approval,
            valid.CompatibilityEvidence);

        var result = PluginLoadProbe.Load(malformed);

        Assert.Equal(PluginLoadStatus.Failed, result.Status);
        Assert.Equal(PluginLoadReasonCodes.InvalidRequest, result.ReasonCode);
        Assert.Null(result.AssemblyName);
    }

    [Fact]
    public void InvalidArtifactPathIsRejectedWithoutEchoingThePath()
    {
        var validPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;
        var valid = CreateRequest(validPath);
        const string invalidPath = "invalid\0plugin.dll";
        var request = valid with { ArtifactPath = invalidPath };

        var result = PluginLoadProbe.Load(request);

        Assert.Equal(PluginLoadStatus.Failed, result.Status);
        Assert.Equal(PluginLoadReasonCodes.ArtifactUnavailable, result.ReasonCode);
        Assert.DoesNotContain(invalidPath, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultDoesNotExposeTheArtifactPath()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.DoesNotContain(artifactPath, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(artifactPath, result.ReasonCode, StringComparison.Ordinal);
        Assert.DoesNotContain(artifactPath, result.AssemblyName ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(artifactPath, string.Join("\n", result.ImplementedContractTypes), StringComparison.Ordinal);
    }

    private static string FixturePath(string projectName)
    {
        var fileName = projectName is "ThroneForge.PluginLoadDuplicateApiDependency"
            ? "ThroneForge.API.dll"
            : projectName is "ThroneForge.PluginLoadDuplicateContractsDependency"
                ? "ThroneForge.Contracts.dll"
                : $"{projectName}.dll";
        var repositoryRoot = FindRepositoryRoot();
        var path = Path.Combine(repositoryRoot, "artifacts", "bin", projectName, "Release", "net10.0", fileName);
        Assert.True(File.Exists(path), $"Expected source-only fixture output was not built: {projectName}.");
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private sealed class RetainedAssembly
    {
        private Assembly? _assembly;

        private RetainedAssembly(AssemblyLoadContext context, Assembly assembly)
        {
            _assembly = assembly;
            ContextReference = new WeakReference(context);
            context.Unload();
        }

        public WeakReference ContextReference { get; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static RetainedAssembly Load(byte[] bytes)
        {
            var context = new AssemblyLoadContext("retained-plugin-context", isCollectible: true);
            var assembly = context.LoadFromStream(new MemoryStream(bytes, writable: false));
            return new RetainedAssembly(context, assembly);
        }

        public void Release() => _assembly = null;
    }

    [Fact]
    public void SuccessfulResultPreservesTheAdmissionBindingDigest()
    {
        var artifactPath = typeof(ThroneForge.PluginLoadFixture.SyntheticThroneForgeMod).Assembly.Location;

        var result = PluginLoadProbe.Load(CreateRequest(artifactPath));

        Assert.Equal(PluginLoadStatus.Loaded, result.Status);
        Assert.NotNull(result.Binding);
        Assert.Equal(
            CodeModAdmissionGate.Evaluate(
                    new CodeModActivationRequest(
                        CreateDescriptor(artifactPath),
                        CreateFingerprint(),
                        new CodeModIntegrityEvidence(
                            CreateIdentity(),
                            new Sha256Digest(CreateHash(artifactPath)),
                            new Sha256Digest(CreateHash(artifactPath)),
                            CodeModIntegrityVerificationStatus.Verified,
                            "sha256-file"),
                        CreateApproval(CreateIdentity(), CreateHash(artifactPath), CreateFingerprint()),
                        CreateCompatibility()))
                .Binding!
                .BindingDigest,
            result.Binding!.BindingDigest);
    }

    private static PluginLoadRequest CreateRequest(
        string artifactPath,
        CodeModApprovalRecord? approval = null,
        AdapterCompatibilityEvidence? compatibility = null,
        string? packageHashOverride = null,
        bool includeApproval = true)
    {
        var identity = CreateIdentity();
        var fingerprint = CreateFingerprint();
        var packageHash = packageHashOverride ?? CreateHash(artifactPath);
        return new PluginLoadRequest(
            artifactPath,
            new CodeModDescriptor(identity, packageHash),
            fingerprint,
            includeApproval ? approval ?? CreateApproval(identity, packageHash, fingerprint) : null,
            compatibility ?? CreateCompatibility());
    }

    private static ModIdentity CreateIdentity() => new("dev.example.synthetic", "1.0.0");

    private static GameFingerprint CreateFingerprint() =>
        new("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    private static AdapterCompatibilityEvidence CreateCompatibility() =>
        new(CreateFingerprint(), "thronefall.adapter", "1.0.0", AdapterCompatibility.Supported);

    private static CodeModDescriptor CreateDescriptor(string artifactPath) =>
        new(CreateIdentity(), CreateHash(artifactPath));

    private static CodeModApprovalRecord CreateApproval(
        ModIdentity identity,
        string packageHash,
        GameFingerprint fingerprint,
        CodeModApprovalDecision decision = CodeModApprovalDecision.Approved) =>
        new(
            identity,
            new Sha256Digest(packageHash),
            fingerprint,
            decision,
            CodeModApprovalScope.ExactPackageAndGameBuild,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));

    private static string CreateHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
