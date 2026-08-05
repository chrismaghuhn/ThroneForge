namespace ThroneForge.PluginSmokeTest;

internal static class Program
{
    public static int Main(string[] args) => PluginSmokeCli.Run(args, Console.Out, Console.Error);
}

public static class PluginSmokeCli
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine("Usage: package|admit|deploy|remove|parse-marker with explicit paths and evidence.");
            return 2;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "package" => Package(args, stdout),
                "admit" => Admit(args, stdout),
                "deploy" => Deploy(args, stdout),
                "remove" => Remove(args, stdout),
                "parse-marker" => ParseMarker(args, stdout),
                "inspect" => Inspect(args, stdout),
                "tfm" => Tfm(args, stdout),
                "launch" => Launch(args, stdout),
                "verify-log" => VerifyLog(args, stdout),
                "manifest" => Manifest(args, stdout),
                _ => throw new PluginSmokeException("The requested plugin smoke-test operation is unsupported.")
            };
        }
        catch (PluginSmokeException exception)
        {
            stderr.WriteLine($"Plugin smoke test failed: {exception.Message}");
            return 2;
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine($"Invalid plugin smoke-test arguments: {exception.Message}");
            return 2;
        }
    }

    private static int Package(string[] args, TextWriter stdout)
    {
        var packageRoot = Value(args, "--package-root");
        var manifestPath = Value(args, "--manifest-path");
        var targetFramework = Value(args, "--target-framework");
        var identity = new ThroneForge.Contracts.ModIdentity(
            "dev.throneforge.m1.synthetic-smoke",
            "0.0.1");
        var manifest = PluginPackageManifestService.CreateFromDirectory(
            packageRoot,
            identity,
            [
                "ThroneForge.M1.SyntheticSmoke.dll",
                "ThroneForge.API.dll",
                "ThroneForge.Contracts.dll"
            ],
            targetFramework);
        PluginPackageManifestService.Save(manifestPath, manifest);
        stdout.WriteLine($"package-sha256={manifest.PackageSha256.Value}");
        stdout.WriteLine("package-file-count=3");
        return 0;
    }

    private static int Admit(string[] args, TextWriter stdout)
    {
        var manifest = PluginPackageManifestService.Load(Value(args, "--manifest-path"));
        var fingerprint = new ThroneForge.Contracts.GameFingerprint(Value(args, "--expected-fingerprint"));
        var decision = PluginAdmissionService.EvaluateApprovedPackage(
            manifest,
            new PluginAdmissionInputs(
                fingerprint,
                Value(args, "--adapter-id"),
                Value(args, "--adapter-version"),
                DateTimeOffset.UtcNow));
        stdout.WriteLine($"admission={decision.Status}");
        stdout.WriteLine($"reason={decision.ReasonCode}");
        stdout.WriteLine($"package-sha256={manifest.PackageSha256.Value}");
        if (decision.Binding is not null)
        {
            stdout.WriteLine($"binding-digest={decision.Binding.BindingDigest}");
        }

        return decision.Status == ThroneForge.Runtime.CodeModAdmissionStatus.Approved ? 0 : 1;
    }

    private static int Deploy(string[] args, TextWriter stdout)
    {
        var manifest = PluginPackageManifestService.Load(Value(args, "--manifest-path"));
        var receipt = PluginDeploymentService.Deploy(
            Value(args, "--package-root"),
            Value(args, "--clean-game"),
            manifest,
            new PluginDeploymentPreconditions(true, true, true, true));
        stdout.WriteLine($"deployed-file-count={receipt.DeployedRelativePaths.Count}");
        return 0;
    }

    private static int Remove(string[] args, TextWriter stdout)
    {
        PluginDeploymentService.Remove(Value(args, "--clean-game"), "dev.throneforge.m1.synthetic-smoke");
        stdout.WriteLine("synthetic-plugin-removed=true");
        return 0;
    }

    private static int ParseMarker(string[] args, TextWriter stdout)
    {
        var result = PluginSmokeMarkerParser.Parse(
            File.ReadAllText(Value(args, "--log-path")),
            Value(args, "--nonce"));
        stdout.WriteLine($"marker-valid={result.IsValid}");
        stdout.WriteLine($"marker-count={result.MarkerCount}");
        stdout.WriteLine($"lifecycle-marker={result.LifecycleMarkerDetected}");
        if (!result.IsValid)
        {
            stdout.WriteLine($"failure-category={result.FailureCategory}");
        }

        return result.IsValid ? 0 : 1;
    }

    private static int Inspect(string[] args, TextWriter stdout)
    {
        var metadata = PluginAssemblyMetadataInspector.Inspect(
            Value(args, "--assembly-path"),
            Value(args, "--relative-path"));
        stdout.WriteLine($"assembly-identity={metadata.AssemblyIdentity}");
        stdout.WriteLine($"target-framework={metadata.TargetFramework ?? "unknown"}");
        stdout.WriteLine($"managed={metadata.HasManagedMetadata}");
        stdout.WriteLine($"clr-header={metadata.ClrHeaderPresent}");
        stdout.WriteLine($"il-only={metadata.IlOnly}");
        stdout.WriteLine($"native-entry-point={metadata.NativeEntryPointPresent}");
        stdout.WriteLine($"managed-native-header={metadata.ManagedNativeHeaderPresent}");
        stdout.WriteLine($"pinvoke-count={metadata.PInvokeEntryCount}");
        stdout.WriteLine($"module-initializer={metadata.ModuleInitializerPresent}");
        stdout.WriteLine($"sha256={metadata.Sha256.Value}");
        return 0;
    }

    private static int Tfm(string[] args, TextWriter stdout)
    {
        var evidence = AssemblyPaths(args)
            .Select(path =>
            {
                var metadata = PluginAssemblyMetadataInspector.Inspect(path, Path.GetFileName(path));
                var fileName = Path.GetFileName(path);
                return new ManagedAssemblyCompatibilityEvidence(
                    fileName,
                    metadata.AssemblyIdentity,
                    NormalizeTargetFramework(metadata.TargetFramework),
                    metadata.HasManagedMetadata,
                    fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                        || metadata.TargetFramework?.StartsWith(".NETStandard,Version=v", StringComparison.OrdinalIgnoreCase) == true);
            })
            .ToArray();
        var assessment = PluginTargetFrameworkSelector.Select(evidence, Value(args, "--unity-version"));
        stdout.WriteLine($"recommendation={assessment.Recommendation}");
        stdout.WriteLine($"confidence={assessment.Confidence}");
        stdout.WriteLine($"basis={assessment.Basis}");
        return assessment.Recommendation == PluginTargetFramework.Inconclusive ? 1 : 0;
    }

    private static int Launch(string[] args, TextWriter stdout)
    {
        var result = ThroneForge.LoaderSmokeTest.LaunchObservationService.Observe(
            Value(args, "--executable"),
            Value(args, "--clean-game"),
            Value(args, "--experiment-root"),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["THRONEFORGE_SMOKE_NONCE"] = Value(args, "--nonce")
            });
        stdout.WriteLine($"started={result.Started}");
        stdout.WriteLine($"stable-initialized={result.StableInitialized}");
        stdout.WriteLine($"exited={result.Exited}");
        stdout.WriteLine($"gracefully-closed={result.Exited && !result.RequiresManualClosure}");
        stdout.WriteLine($"manual-closure-required={result.RequiresManualClosure}");
        stdout.WriteLine($"failure-category={result.FailureCategory}");
        return result.Started && result.Exited && !result.RequiresManualClosure ? 0 : 1;
    }

    private static int VerifyLog(string[] args, TextWriter stdout)
    {
        var summary = PluginSmokeLogParser.Parse(
            File.ReadAllText(Value(args, "--log-path")),
            Value(args, "--nonce"),
            Value(args, "--api-identity"),
            Value(args, "--contracts-identity"));
        stdout.WriteLine($"bepinex-version={summary.Loader.BepInExVersion ?? "unknown"}");
        stdout.WriteLine($"preloader={summary.Loader.PreloaderInitialized}");
        stdout.WriteLine($"chainloader={summary.Loader.ChainloaderInitialized}");
        stdout.WriteLine($"plugins={summary.Loader.PluginsDiscovered}");
        stdout.WriteLine($"warnings={summary.Loader.WarningCount}");
        stdout.WriteLine($"errors={summary.Loader.ErrorCount}");
        stdout.WriteLine($"fatal-errors={summary.Loader.FatalErrorCount}");
        stdout.WriteLine($"marker={summary.Marker.IsValid}");
        stdout.WriteLine($"marker-count={summary.Marker.MarkerCount}");
        stdout.WriteLine($"lifecycle-marker={summary.Marker.LifecycleMarkerDetected}");
        stdout.WriteLine($"smoke-criteria={summary.MeetsCriteria}");
        return summary.MeetsCriteria ? 0 : 1;
    }

    private static int Manifest(string[] args, TextWriter stdout)
    {
        var manifest = ThroneForge.LoaderSmokeTest.InstallationCopyService.CaptureManifest(Value(args, "--root"));
        stdout.WriteLine($"manifest-identity={ThroneForge.LoaderSmokeTest.InstallationCopyService.ComputeManifestIdentity(manifest)}");
        stdout.WriteLine($"file-count={manifest.Files.Count}");
        stdout.WriteLine($"directory-count={(manifest.Directories ?? []).Count}");
        return 0;
    }

    private static List<string> AssemblyPaths(string[] args)
    {
        var result = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].Equals("--assembly-path", StringComparison.Ordinal) && index + 1 < args.Length)
            {
                result.Add(args[++index]);
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("At least one --assembly-path value is required.");
        }

        return result;
    }

    private static string? NormalizeTargetFramework(string? targetFramework)
    {
        if (targetFramework is null)
        {
            return null;
        }

        if (targetFramework.Contains(".NETStandard,Version=v2.0", StringComparison.OrdinalIgnoreCase))
        {
            return "netstandard2.0";
        }

        if (targetFramework.Contains(".NETStandard,Version=v2.1", StringComparison.OrdinalIgnoreCase))
        {
            return "netstandard2.1";
        }

        return targetFramework;
    }

    private static string Value(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[index + 1];
    }
}
