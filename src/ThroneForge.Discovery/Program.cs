namespace ThroneForge.Discovery;

public static class Program
{
    public static int Main(string[] args)
        => DiscoveryCli.Run(args, Console.Out, Console.Error);
}

public static class DiscoveryCli
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            PrintUsage(stderr);
            return 2;
        }

        if (args[0].Equals("inspect", StringComparison.OrdinalIgnoreCase))
        {
            return RunInspect(args, stdout, stderr);
        }

        if (args[0].Equals("runtime-compatibility", StringComparison.OrdinalIgnoreCase))
        {
            return RunRuntimeCompatibility(args, stdout, stderr);
        }

        if (args[0].Equals("runtime-compatibility-evidence", StringComparison.OrdinalIgnoreCase))
        {
            return RunRuntimeCompatibilityEvidence(args, stdout, stderr);
        }

        stderr.WriteLine($"Unknown command '{args[0]}'.");
        PrintUsage(stderr);
        return 2;
    }

    private static int RunInspect(string[] args, TextWriter stdout, TextWriter stderr)
    {

        string? gamePath = null;
        var outputRoot = Path.Combine("docs", "discovery");
        var overwrite = false;

        try
        {
            for (var index = 1; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--game-path":
                        gamePath = ReadOptionValue(args, ref index, "--game-path");
                        break;
                    case "--output-root":
                        outputRoot = ReadOptionValue(args, ref index, "--output-root");
                        break;
                    case "--overwrite":
                        overwrite = true;
                        break;
                    default:
                        stderr.WriteLine($"Unknown option '{args[index]}'.");
                        PrintUsage(stderr);
                        return 2;
                }
            }
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine(exception.Message);
            PrintUsage(stderr);
            return 2;
        }

        if (string.IsNullOrWhiteSpace(gamePath))
        {
            stderr.WriteLine("--game-path is required.");
            PrintUsage(stderr);
            return 2;
        }

        try
        {
            var result = new DiscoveryEngine().Inspect(new DiscoveryRequest(gamePath, outputRoot, overwrite));
            stdout.WriteLine($"Discovery report: {Path.GetFileName(result.ReportPath)}");
            stdout.WriteLine($"Backend: {result.Backend}");
            stdout.WriteLine($"Executable architecture: {result.ExecutableArchitecture}");
            stdout.WriteLine($"Fingerprint: {result.Fingerprint}");
            return 0;
        }
        catch (DiscoveryException exception)
        {
            stderr.WriteLine($"Discovery failed: {exception.Message}");
            return 2;
        }
    }

    private static int RunRuntimeCompatibility(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? gamePath = null;
        string? fingerprint = null;
        var outputRoot = Path.Combine("docs", "discovery");
        var overwrite = false;

        try
        {
            for (var index = 1; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--game-path":
                        gamePath = ReadOptionValue(args, ref index, "--game-path");
                        break;
                    case "--fingerprint":
                        fingerprint = ReadOptionValue(args, ref index, "--fingerprint");
                        break;
                    case "--output-root":
                        outputRoot = ReadOptionValue(args, ref index, "--output-root");
                        break;
                    case "--overwrite":
                        overwrite = true;
                        break;
                    default:
                        stderr.WriteLine($"Unknown option '{args[index]}'.");
                        PrintUsage(stderr);
                        return 2;
                }
            }
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine(exception.Message);
            PrintUsage(stderr);
            return 2;
        }

        if (string.IsNullOrWhiteSpace(gamePath))
        {
            stderr.WriteLine("--game-path is required.");
            PrintUsage(stderr);
            return 2;
        }

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            stderr.WriteLine("--fingerprint is required.");
            PrintUsage(stderr);
            return 2;
        }

        try
        {
            var result = new RuntimeCompatibilityEngine().Inspect(
                new RuntimeCompatibilityRequest(gamePath, fingerprint, outputRoot, overwrite));
            stdout.WriteLine($"Runtime compatibility report: {Path.GetFileName(result.ReportPath)}");
            stdout.WriteLine($"Managed runtime profile: {result.ManagedRuntimeProfile}");
            stdout.WriteLine($"Executable architecture: {result.ExecutableArchitecture}");
            stdout.WriteLine($"Selected executable: {result.SelectedExecutableRelativePath ?? "unknown"}");
            stdout.WriteLine($"Target-framework recommendation: {result.TargetFrameworkRecommendation}");
            stdout.WriteLine($"Recommended candidate: {result.RecommendedCandidate}");
            stdout.WriteLine($"Current clean-profile smoke-test readiness: {result.SmokeTestReadiness.Status}");
            return 0;
        }
        catch (DiscoveryException exception)
        {
            stderr.WriteLine($"Runtime compatibility inspection failed: {exception.Message}");
            return 2;
        }
    }

    private static int RunRuntimeCompatibilityEvidence(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var parsed = ParseRuntimeCompatibilityOptions(args, stdout, stderr, includeOutput: false);
        if (parsed is null)
        {
            return 2;
        }

        try
        {
            var result = new RuntimeCompatibilityEngine().Inspect(parsed);
            stdout.WriteLine(RuntimeCompatibilityEvidenceContract.Serialize(result));
            return 0;
        }
        catch (DiscoveryException exception)
        {
            stderr.WriteLine($"Runtime compatibility evidence failed: {exception.Message}");
            return 2;
        }
    }

    private static RuntimeCompatibilityRequest? ParseRuntimeCompatibilityOptions(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        bool includeOutput)
    {
        string? gamePath = null;
        string? fingerprint = null;
        var outputRoot = Path.Combine("docs", "discovery");
        var overwrite = false;
        try
        {
            for (var index = 1; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--game-path": gamePath = ReadOptionValue(args, ref index, "--game-path"); break;
                    case "--fingerprint": fingerprint = ReadOptionValue(args, ref index, "--fingerprint"); break;
                    case "--output-root": outputRoot = ReadOptionValue(args, ref index, "--output-root"); break;
                    case "--overwrite": overwrite = true; break;
                    default: stderr.WriteLine($"Unknown option '{args[index]}'."); PrintUsage(stderr); return null;
                }
            }
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine(exception.Message);
            PrintUsage(stderr);
            return null;
        }

        if (string.IsNullOrWhiteSpace(gamePath) || string.IsNullOrWhiteSpace(fingerprint))
        {
            stderr.WriteLine("--game-path and --fingerprint are required.");
            PrintUsage(stderr);
            return null;
        }

        return new RuntimeCompatibilityRequest(gamePath, fingerprint, outputRoot, overwrite);
    }

    private static string ReadOptionValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static void PrintUsage(TextWriter stderr)
    {
        stderr.WriteLine("Usage:");
        stderr.WriteLine("  dotnet run --project src/ThroneForge.Discovery -- inspect --game-path <absolute-path> [--output-root <path>] [--overwrite]");
        stderr.WriteLine("  dotnet run --project src/ThroneForge.Discovery -- runtime-compatibility --game-path <absolute-path> --fingerprint <sha256> [--output-root <path>] [--overwrite]");
        stderr.WriteLine("  dotnet run --project src/ThroneForge.Discovery -- runtime-compatibility-evidence --game-path <absolute-path> --fingerprint <sha256>");
    }
}
