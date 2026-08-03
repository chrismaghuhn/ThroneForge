namespace ThroneForge.LoaderSmokeTest;

public static class Program
{
    public static int Main(string[] args)
        => LoaderSmokeTestCli.Run(args, Console.Out, Console.Error);
}

public static class LoaderSmokeTestCli
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            PrintUsage(stderr);
            return 2;
        }

        try
        {
            var mode = ParseMode(args[0]);
            string? gamePath = null;
            string? experimentRoot = null;
            string? expectedFingerprint = null;
            string? repositoryRoot = Directory.GetCurrentDirectory();
            string? archivePath = null;
            string? officialDigest = null;
            string? officialAssetId = null;
            string? officialAssetSize = null;
            var whatIf = false;

            for (var index = 1; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--game-path":
                        gamePath = ReadValue(args, ref index, "--game-path");
                        break;
                    case "--experiment-root":
                        experimentRoot = ReadValue(args, ref index, "--experiment-root");
                        break;
                    case "--expected-fingerprint":
                        expectedFingerprint = ReadValue(args, ref index, "--expected-fingerprint");
                        break;
                    case "--repository-root":
                        repositoryRoot = ReadValue(args, ref index, "--repository-root");
                        break;
                    case "--bepinex-archive":
                        archivePath = ReadValue(args, ref index, "--bepinex-archive");
                        break;
                    case "--official-digest":
                        officialDigest = ReadValue(args, ref index, "--official-digest");
                        break;
                    case "--official-asset-id":
                        officialAssetId = ReadValue(args, ref index, "--official-asset-id");
                        break;
                    case "--official-asset-size":
                        officialAssetSize = ReadValue(args, ref index, "--official-asset-size");
                        break;
                    case "--what-if":
                        whatIf = true;
                        break;
                    default:
                        stderr.WriteLine($"Unknown option '{args[index]}'.");
                        PrintUsage(stderr);
                        return 2;
                }
            }

            if (string.IsNullOrWhiteSpace(gamePath)
                || string.IsNullOrWhiteSpace(experimentRoot)
                || string.IsNullOrWhiteSpace(expectedFingerprint)
                || string.IsNullOrWhiteSpace(repositoryRoot))
            {
                stderr.WriteLine("--game-path, --experiment-root, --expected-fingerprint, and --repository-root are required.");
                PrintUsage(stderr);
                return 2;
            }

            var result = SmokeTestOrchestrator.Run(new LoaderSmokeTestRequest(
                mode,
                gamePath,
                experimentRoot,
                expectedFingerprint,
                repositoryRoot,
                archivePath,
                null,
                whatIf,
                OfficialAssetDigest: officialDigest,
                OfficialAssetId: officialAssetId,
                OfficialAssetSize: officialAssetSize));
            stdout.WriteLine($"Smoke-test outcome: {result.Outcome}");
            stdout.WriteLine(result.Message);
            stdout.WriteLine($"Original fingerprint verified: {result.OriginalInstallationVerified}");
            stdout.WriteLine($"Rollback verified: {result.RollbackVerified}");
            if (result.ReportPath is not null)
            {
                stdout.WriteLine($"Sanitized report: {Path.GetFileName(result.ReportPath)}");
            }

            return result.Outcome is SmokeTestOutcome.Passed or SmokeTestOutcome.PassedWithWarnings
                || mode == SmokeTestMode.Plan
                || mode is SmokeTestMode.Prepare or SmokeTestMode.Install or SmokeTestMode.Cleanup
                || whatIf
                ? 0
                : 1;
        }
        catch (SmokeTestException exception)
        {
            stderr.WriteLine($"Loader smoke test failed: {exception.Message}");
            return 2;
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine($"Invalid loader smoke-test arguments: {exception.Message}");
            return 2;
        }
    }

    private static SmokeTestMode ParseMode(string value)
        => Enum.TryParse<SmokeTestMode>(value, ignoreCase: true, out var mode)
            ? mode
            : throw new ArgumentException("The mode must be Plan, Prepare, Baseline, Install, Launch, Verify, Rollback, Full, Resume, or Cleanup.");

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project src/ThroneForge.LoaderSmokeTest -- <Plan|Prepare|Baseline|Install|Launch|Verify|Rollback|Full|Resume|Cleanup>");
        writer.WriteLine("    --game-path <absolute-path> --experiment-root <external-absolute-path>");
        writer.WriteLine("    --expected-fingerprint <sha256> --repository-root <repository-absolute-path>");
        writer.WriteLine("    [--bepinex-archive <BepInEx_win_x64_5.4.23.5.zip>] [--what-if]");
    }
}
