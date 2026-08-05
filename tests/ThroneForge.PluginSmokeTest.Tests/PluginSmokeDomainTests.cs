using System.Security.Cryptography;
using System.Text;
using ThroneForge.Contracts;
using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class PluginSmokeDomainTests
{
    private static readonly GameFingerprint Fingerprint =
        new("1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d");

    [Fact]
    public void TfmSelectorUsesUnityEvidenceForNetstandard21()
    {
        var result = PluginTargetFrameworkSelector.Select(
            [new ManagedAssemblyCompatibilityEvidence("BepInEx/core.dll", "BepInEx.Core, Version=5.4.23.5.0", null, true, true)],
            "2022.3.62f2");

        Assert.Equal(PluginTargetFramework.Netstandard21Candidate, result.Recommendation);
        Assert.Equal(PluginTfmConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void TfmSelectorReturnsInconclusiveWithoutEvidence()
    {
        var result = PluginTargetFrameworkSelector.Select([], null);

        Assert.Equal(PluginTargetFramework.Inconclusive, result.Recommendation);
        Assert.Equal(PluginTfmConfidence.None, result.Confidence);
    }

    [Fact]
    public void PackageDigestIsDeterministicAndChangesWithContent()
    {
        var identity = new ModIdentity("dev.throneforge.m1.synthetic-smoke", "0.0.1");
        var first = PluginPackageManifestService.Create(
            identity,
            [new PluginPackageFile("Plugin.dll", 3, Digest("abc"), "Plugin, Version=1.0.0.0", "netstandard2.1")]);
        var second = PluginPackageManifestService.Create(
            identity,
            [new PluginPackageFile("Plugin.dll", 4, Digest("abcd"), "Plugin, Version=1.0.0.0", "netstandard2.1")]);

        Assert.Equal(first.PackageSha256, PluginPackageManifestService.ComputeDigest(first));
        Assert.NotEqual(first.PackageSha256, second.PackageSha256);
    }

    [Theory]
    [InlineData("../outside.dll")]
    [InlineData("C:/outside.dll")]
    [InlineData("plugin\\outside.dll")]
    [InlineData("plugin:stream.dll")]
    public void PackageManifestRejectsUnsafeRelativePaths(string path)
    {
        var identity = new ModIdentity("dev.throneforge.m1.synthetic-smoke", "0.0.1");

        Assert.Throws<PluginSmokeException>(() => PluginPackageManifestService.Create(
            identity,
            [new PluginPackageFile(path, 1, Digest("x"), "Plugin, Version=1.0.0.0", "netstandard2.1")]));
    }

    [Fact]
    public void MarkerParserRequiresExactlyOneMatchingNonce()
    {
        var text = string.Join(
            '\n',
            "THRONEFORGE_SYNTHETIC_PLUGIN_READY",
            "nonce=nonce-123",
            "pluginGuid=dev.throneforge.m1.synthetic-smoke",
            "pluginVersion=0.0.1",
            "api=ThroneForge.API, Version=1.0.0.0",
            "contracts=ThroneForge.Contracts, Version=1.0.0.0");

        var result = PluginSmokeMarkerParser.Parse(text, "nonce-123");

        Assert.True(result.IsValid);
        Assert.Equal(1, result.MarkerCount);
        Assert.Equal("dev.throneforge.m1.synthetic-smoke", result.PluginGuid);
    }

    [Fact]
    public void MarkerParserRejectsDuplicateOrWrongNonce()
    {
        var text = "THRONEFORGE_SYNTHETIC_PLUGIN_READY\nnonce=wrong\npluginGuid=dev.throneforge.m1.synthetic-smoke\npluginVersion=0.0.1";

        var result = PluginSmokeMarkerParser.Parse(text, "expected");

        Assert.False(result.IsValid);
        Assert.Contains("nonce", result.FailureCategory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkerParserReturnsStructuredInvalidResultForDuplicateKeys()
    {
        var result = PluginSmokeMarkerParser.Parse(
            "THRONEFORGE_SYNTHETIC_PLUGIN_READY|nonce=n|nonce=second|pluginGuid=dev.throneforge.m1.synthetic-smoke|pluginVersion=0.0.1",
            "n");

        Assert.False(result.IsValid);
        Assert.Equal("duplicate-key", result.FailureCategory);
    }

    [Fact]
    public void LoggerPrefixedSingleLineMarkerIsAccepted()
    {
        var result = PluginSmokeMarkerParser.Parse(
            "[Info   :ThroneForge M1 Synthetic Smoke] THRONEFORGE_SYNTHETIC_PLUGIN_READY|nonce=n|pluginGuid=dev.throneforge.m1.synthetic-smoke|pluginVersion=0.0.1|api=ThroneForge.API, Version=1.0.0.0|contracts=ThroneForge.Contracts, Version=1.0.0.0",
            "n",
            expectedApiIdentity: "ThroneForge.API, Version=1.0.0.0",
            expectedContractsIdentity: "ThroneForge.Contracts, Version=1.0.0.0");

        Assert.True(result.IsValid);
        Assert.Equal(1, result.MarkerCount);
    }

    [Fact]
    public void MarkerParserRejectsLifecycleMarker()
    {
        var result = PluginSmokeMarkerParser.Parse(
            "THRONEFORGE_SYNTHETIC_PLUGIN_READY\nnonce=n\npluginGuid=dev.throneforge.m1.synthetic-smoke\npluginVersion=0.0.1\nTHRONEFORGE_SYNTHETIC_PLUGIN_LIFECYCLE_INVOKED",
            "n");

        Assert.False(result.IsValid);
        Assert.True(result.LifecycleMarkerDetected);
    }

    [Fact]
    public void DeploymentPathStaysInsideCleanGamePluginsDirectory()
    {
        var cleanGame = Path.Combine(Path.GetTempPath(), "throneforge-clean-game");

        var deployment = PluginDeploymentPath.GetPluginDirectory(cleanGame, "dev.throneforge.m1.synthetic-smoke");

        Assert.StartsWith(
            Path.GetFullPath(Path.Combine(cleanGame, "BepInEx", "plugins")),
            Path.GetFullPath(deployment),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", Path.GetRelativePath(cleanGame, deployment));
    }

    [Fact]
    public void PrivateExperimentRequestRequiresExplicitEvidence()
    {
        var request = new PluginSmokeRequest(
            "C:/Game",
            "C:/Experiments",
            "C:/archive.zip",
            Fingerprint,
            new Sha256Digest("82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"),
            "C:/Repo");

        Assert.Equal(Fingerprint.Value, request.ExpectedFingerprint.Value);
        Assert.DoesNotContain("Thronefall", request.ToSanitizedString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:/", request.ToSanitizedString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PackageManifestRoundTripsAndAdmissionIsApprovedForExactEvidence()
    {
        var identity = new ModIdentity("dev.throneforge.m1.synthetic-smoke", "0.0.1");
        var manifest = PluginPackageManifestService.Create(
            identity,
            [new PluginPackageFile("Plugin.dll", 3, Digest("abc"), "Plugin, Version=1.0.0.0", "netstandard2.1")]);
        var manifestPath = Path.Combine(Path.GetTempPath(), $"throneforge-package-{Guid.NewGuid():N}.json");

        try
        {
            PluginPackageManifestService.Save(manifestPath, manifest);
            var loaded = PluginPackageManifestService.Load(manifestPath);
            var decision = PluginAdmissionService.EvaluateApprovedPackage(
                loaded,
                new PluginAdmissionInputs(Fingerprint, "throneforge.adapter", "1.0.0", DateTimeOffset.UtcNow));

            Assert.Equal(manifest.PackageSha256, loaded.PackageSha256);
            Assert.Equal(ThroneForge.Runtime.CodeModAdmissionStatus.Approved, decision.Status);
            Assert.NotNull(decision.Binding);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public void DeploymentRejectsUnreadyProfileBeforeCreatingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throneforge-deploy-{Guid.NewGuid():N}");
        var original = Path.Combine(root, "original");
        var experiment = Path.Combine(root, "experiment");
        Directory.CreateDirectory(original);

        try
        {
            var binding = new CodeModAdmissionBinding(
                new ModIdentity("dev.throneforge.m1.synthetic-smoke", "0.0.1"),
                Digest("package"),
                Fingerprint,
                "throneforge.adapter",
                "1.0.0");
            Assert.Throws<PluginSmokeException>(() => PluginDeploymentService.DeriveContext(
                original,
                Path.Combine(experiment, "clean-game"),
                experiment,
                root,
                Fingerprint.Value,
                binding));
            Assert.False(Directory.Exists(Path.Combine(experiment, "clean-game", "BepInEx")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Task6OwnershipStateRequiresExactFingerprintAndCleanGameRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throneforge-task6-{Guid.NewGuid():N}");
        try
        {
            var state = Task6ExperimentStateService.CreatePrepared(
                root,
                Fingerprint.Value,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            Task6ExperimentStateService.SaveAtomic(root, state);

            var loaded = Task6ExperimentStateService.LoadAndValidate(root, Fingerprint.Value);

            Assert.Equal(Task6ExperimentStatus.Prepared, loaded.Status);
            Assert.Equal("clean-game", loaded.CleanGameRelativePath);
            Assert.Throws<PluginSmokeException>(() => Task6ExperimentStateService.LoadAndValidate(root, new string('a', 64)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Task6OwnershipStateRejectsArbitraryExistingDirectoryWithoutMarker()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throneforge-task6-unowned-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<PluginSmokeException>(() => Task6ExperimentStateService.LoadAndValidate(root, Fingerprint.Value));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PackageManifestLoaderRejectsUnsupportedSchemaVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"throneforge-package-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{\"SchemaVersion\":\"future\",\"ModId\":\"dev.throneforge.m1.synthetic-smoke\",\"ModVersion\":\"0.0.1\",\"PackageSha256\":\"" + new string('0', 64) + "\",\"Files\":[]}");
        try
        {
            Assert.Throws<PluginSmokeException>(() => PluginPackageManifestService.Load(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void PackageValidationAcceptsOnlyTheExpectedPluginMetadata()
    {
        var metadata = new PluginAssemblyMetadata(
            "ThroneForge.M1.SyntheticSmoke.dll",
            1,
            Digest("synthetic-plugin"),
            "ThroneForge.M1.SyntheticSmoke, Version=1.0.0.0",
            ".NETStandard,Version=v2.1",
            true,
            true,
            true,
            false,
            false,
            0,
            false,
            ["BepInEx", "ThroneForge.API", "ThroneForge.Contracts", "netstandard"],
            1,
            "dev.throneforge.m1.synthetic-smoke",
            "ThroneForge M1 Synthetic Smoke",
            "0.0.1",
            1,
            true,
            true,
            1);

        PluginPackageValidationRules.Validate(
            "ThroneForge.M1.SyntheticSmoke.dll",
            metadata,
            "netstandard2.1");
    }

    [Fact]
    public void ApiAndContractsPublicSurfaceIsEquivalentAcrossNet10AndNetstandard21()
    {
        var repository = FindRepositoryRoot();
        var apiNet10 = Path.Combine(repository, "artifacts", "bin", "ThroneForge.API", "Release", "net10.0", "ThroneForge.API.dll");
        var apiNetstandard = Path.Combine(repository, "artifacts", "bin", "ThroneForge.API", "Release", "netstandard2.1", "ThroneForge.API.dll");
        var contractsNet10 = Path.Combine(repository, "artifacts", "bin", "ThroneForge.Contracts", "Release", "net10.0", "ThroneForge.Contracts.dll");
        var contractsNetstandard = Path.Combine(repository, "artifacts", "bin", "ThroneForge.Contracts", "Release", "netstandard2.1", "ThroneForge.Contracts.dll");

        Assert.True(File.Exists(apiNet10) && File.Exists(apiNetstandard) && File.Exists(contractsNet10) && File.Exists(contractsNetstandard), "Both public target-framework outputs must be built before parity validation.");
        PublicSurfaceParityService.RequireEquivalent(apiNet10, apiNetstandard);
        PublicSurfaceParityService.RequireEquivalent(contractsNet10, contractsNetstandard);
    }

    [Fact]
    public void TransactionalDeploymentRemovesPartialFilesAndDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"throneforge-deploy-transaction-{Guid.NewGuid():N}");
        var clean = Path.Combine(root, "clean-game");
        Directory.CreateDirectory(Path.Combine(clean, "BepInEx", "plugins"));
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["ThroneForge.M1.SyntheticSmoke.dll"] = Encoding.UTF8.GetBytes("one"),
            ["ThroneForge.API.dll"] = Encoding.UTF8.GetBytes("two"),
            ["ThroneForge.Contracts.dll"] = Encoding.UTF8.GetBytes("three")
        };
        var files = bytes.Select(item => new PluginPackageFile(
            item.Key,
            item.Value.LongLength,
            new Sha256Digest(Convert.ToHexString(SHA256.HashData(item.Value)).ToLowerInvariant()),
            Path.GetFileNameWithoutExtension(item.Key) + ", Version=1.0.0.0",
            "netstandard2.1")).ToArray();
        var manifest = PluginPackageManifestService.Create(new ModIdentity("dev.throneforge.m1.synthetic-smoke", "0.0.1"), files);
        var roots = new SmokeTestRoots(root, Path.Combine(root, "original"), root, clean, Path.Combine(root, "downloads"), Path.Combine(root, "extract"), Path.Combine(root, "evidence"), Path.Combine(root, "manifests"), Path.Combine(root, "backup"));
        var binding = new CodeModAdmissionBinding(new ModIdentity("dev.throneforge.m1.synthetic-smoke", "0.0.1"), manifest.PackageSha256, Fingerprint, "throneforge.adapter", "1.0.0");
        var context = new PluginDeploymentContext(
            roots,
            new Task6ExperimentState(Task6ExperimentStateService.SchemaVersion, Task6ExperimentStateService.TaskVersion, Fingerprint.Value, Guid.NewGuid().ToString("N"), new string('a', 40), "clean-game", Task6ExperimentStatus.LaunchObserved),
            new DisposableProfileBaseline("baseline", "task", Fingerprint.Value, new CopyManifest([], []), new CopyManifest([], ["BepInEx", "BepInEx/plugins"])),
            new LoaderTransactionState("schema", "task", Fingerprint.Value, "baseline", "archive.zip", new string('a', 64), LoaderTransactionStatus.LaunchObserved, new CopyManifest([], []), [], []),
            new CopyManifest([], ["BepInEx", "BepInEx/plugins"]),
            binding);

        try
        {
            Assert.Throws<PluginSmokeException>(() => PluginDeploymentService.DeployCaptured(new CapturedPluginPackage(manifest, bytes, new Dictionary<string, PluginAssemblyMetadata>()), context, failAfterFiles: 2));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(clean, "BepInEx", "plugins")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void MetadataInspectorReadsTheExactBuiltApiAssembly()
    {
        var apiPath = typeof(ThroneForge.API.IThroneForgeMod).Assembly.Location;
        var metadata = PluginAssemblyMetadataInspector.Inspect(apiPath, "ThroneForge.API.dll");

        Assert.True(metadata.HasManagedMetadata);
        Assert.True(metadata.ClrHeaderPresent);
        Assert.True(metadata.IlOnly);
        Assert.False(metadata.NativeEntryPointPresent);
        Assert.Equal(0, metadata.PInvokeEntryCount);
        Assert.DoesNotContain("C:\\", metadata.RelativePath, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginLogParserRequiresLoaderAndExactlyOneNonceMarker()
    {
        var log = string.Join(
            '\n',
            "BepInEx 5.4.23.5",
            "configuration file generated",
            "Preloader finished",
            "Chainloader initialized",
            "1 plugins loaded",
            "THRONEFORGE_SYNTHETIC_PLUGIN_READY",
            "nonce=n",
            "pluginGuid=dev.throneforge.m1.synthetic-smoke",
            "pluginVersion=0.0.1",
            "api=api",
            "contracts=contracts");

        var summary = PluginSmokeLogParser.Parse(log, "n", "api", "contracts");

        Assert.True(summary.MeetsCriteria);
        Assert.Equal(1, summary.Loader.PluginsDiscovered);
    }

    [Fact]
    public void PluginLogParserRejectsLifecycleMarkerAndFatalErrors()
    {
        var summary = PluginSmokeLogParser.Parse(
            "BepInEx 5.4.23.5\nPreloader finished\nChainloader initialized\n1 plugins loaded\nERROR fatal\nTHRONEFORGE_SYNTHETIC_PLUGIN_LIFECYCLE_INVOKED",
            "n");

        Assert.False(summary.MeetsCriteria);
        Assert.True(summary.Marker.LifecycleMarkerDetected);
        Assert.True(summary.Loader.FatalErrorCount > 0);
    }

    private static Sha256Digest Digest(string text)
        => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src"))
                && File.Exists(Path.Combine(current.FullName, "ThroneForge.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located for public-surface parity validation.");
    }
}
