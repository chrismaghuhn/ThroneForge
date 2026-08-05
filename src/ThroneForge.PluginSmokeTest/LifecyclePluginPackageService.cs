using ThroneForge.Contracts;

namespace ThroneForge.PluginSmokeTest;

public static class LifecyclePluginPackageService
{
    public const string PrimaryAssemblyName = "ThroneForge.M1.LifecycleSmoke";
    public const string PluginGuid = "dev.throneforge.m1.lifecycle-smoke";
    public const string PluginName = "ThroneForge M1 Lifecycle Smoke";
    public const string PluginVersion = "0.0.1";
    public const string TargetFramework = "netstandard2.1";
    public static readonly string[] ExpectedPackagePaths =
    [
        $"{PrimaryAssemblyName}.dll",
        "ThroneForge.API.dll",
        "ThroneForge.Contracts.dll"
    ];

    private static readonly HashSet<string> AllowedReferences = new(StringComparer.Ordinal)
    {
        "System.Runtime", "System.Threading", "System.Threading.Tasks", "System.Collections", "System.Private.CoreLib",
        "netstandard", "System.Linq", "System.ObjectModel", "System.ComponentModel.Primitives", "System.Runtime.InteropServices",
        "BepInEx", "UnityEngine", "UnityEngine.CoreModule", "ThroneForge.API", "ThroneForge.Contracts"
    };

    public static PluginPackageManifest CreateManifestFromDirectory(string packageRoot)
    {
        var identity = new ModIdentity(PluginGuid, PluginVersion);
        var captured = CaptureFiles(packageRoot);
        var files = captured.Select(item => new PluginPackageFile(
            item.RelativePath,
            item.Metadata.Size,
            item.Metadata.Sha256,
            item.Metadata.AssemblyIdentity,
            TargetFramework)).ToArray();
        return PluginPackageManifestService.Create(identity, files);
    }

    public static CapturedPluginPackage CaptureAndValidate(string packageRoot, PluginPackageManifest expectedManifest)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        var captured = CaptureFiles(packageRoot);
        var actualManifest = PluginPackageManifestService.Create(
            new ModIdentity(PluginGuid, PluginVersion),
            captured.Select(item => new PluginPackageFile(
                item.RelativePath,
                item.Metadata.Size,
                item.Metadata.Sha256,
                item.Metadata.AssemblyIdentity,
                TargetFramework)).ToArray());

        if (expectedManifest.Identity != actualManifest.Identity
            || expectedManifest.PackageSha256 != actualManifest.PackageSha256
            || expectedManifest.Files.Count != actualManifest.Files.Count
            || expectedManifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Zip(actualManifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
                .Any(pair => pair.First != pair.Second))
        {
            throw new PluginSmokeException("The current lifecycle package bytes do not match the saved expected manifest.");
        }

        return new CapturedPluginPackage(
            actualManifest,
            captured.ToDictionary(item => item.RelativePath, item => item.Bytes, StringComparer.Ordinal),
            captured.ToDictionary(item => item.RelativePath, item => item.Metadata, StringComparer.Ordinal));
    }

    private static List<CapturedFile> CaptureFiles(string packageRoot)
    {
        var root = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(root))
        {
            throw new PluginSmokeException("The lifecycle package directory does not exist.");
        }

        ThroneForge.LoaderSmokeTest.SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(root);
        var actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualFiles.SequenceEqual(ExpectedPackagePaths.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new PluginSmokeException("The lifecycle package must contain exactly the three expected assembly files.");
        }

        var result = new List<CapturedFile>(ExpectedPackagePaths.Length);
        foreach (var relativePath in ExpectedPackagePaths)
        {
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = CaptureFile(fullPath);
            var metadata = PluginAssemblyMetadataInspector.InspectBytes(bytes, relativePath);
            ValidateMetadata(relativePath, metadata);
            result.Add(new(relativePath, bytes, metadata));
        }

        return result;
    }

    private static byte[] CaptureFile(string path)
    {
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            if (input.Length > PluginAssemblyMetadataInspector.DefaultMaximumBytes)
            {
                throw new PluginSmokeException("A lifecycle package file exceeds the bounded read limit.");
            }

            using var output = new MemoryStream(checked((int)input.Length));
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("A lifecycle package file could not be captured safely.", exception);
        }
    }

    private static void ValidateMetadata(string relativePath, PluginAssemblyMetadata metadata)
    {
        if (!metadata.HasManagedMetadata || !metadata.ClrHeaderPresent || !metadata.IlOnly
            || metadata.NativeEntryPointPresent || metadata.ManagedNativeHeaderPresent
            || metadata.PInvokeEntryCount != 0 || metadata.ModuleInitializerPresent)
        {
            throw new PluginSmokeException("A lifecycle package assembly is not a pure managed IL image.");
        }

        var actualTfm = NormalizeTargetFramework(metadata.TargetFramework);
        if (!string.Equals(actualTfm, TargetFramework, StringComparison.Ordinal))
        {
            throw new PluginSmokeException("A lifecycle package assembly has an unexpected target framework.");
        }

        var expectedAssemblyIdentity = $"{Path.GetFileNameWithoutExtension(relativePath)}, Version=1.0.0.0";
        if (!string.Equals(metadata.AssemblyIdentity, expectedAssemblyIdentity, StringComparison.Ordinal))
        {
            throw new PluginSmokeException("A lifecycle package filename and assembly identity do not match.");
        }

        if (metadata.AssemblyReferences.Any(reference => !AllowedReferences.Contains(reference)))
        {
            throw new PluginSmokeException("A lifecycle package assembly has an unexpected managed reference.");
        }

        if (!relativePath.Equals($"{PrimaryAssemblyName}.dll", StringComparison.Ordinal))
        {
            return;
        }

        if (metadata.BepInPluginAttributeCount != 1
            || !string.Equals(metadata.BepInPluginGuid, PluginGuid, StringComparison.Ordinal)
            || !string.Equals(metadata.BepInPluginName, PluginName, StringComparison.Ordinal)
            || !string.Equals(metadata.BepInPluginVersion, PluginVersion, StringComparison.Ordinal)
            || metadata.PublicPluginImplementationCount != 1
            || !metadata.HasExpectedBaseUnityPluginReference
            || !metadata.HasThroneForgeModImplementation
            || metadata.ThroneForgeModImplementationCount != 1)
        {
            throw new PluginSmokeException("The lifecycle plugin does not contain exactly the expected BepInEx and ThroneForge metadata.");
        }
    }

    private static string? NormalizeTargetFramework(string? value)
        => value?.Contains(".NETStandard,Version=v2.1", StringComparison.OrdinalIgnoreCase) == true
            ? TargetFramework
            : value?.Trim().ToLowerInvariant();

    private sealed record CapturedFile(string RelativePath, byte[] Bytes, PluginAssemblyMetadata Metadata);
}
