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
        if (args.Length == 0 || !args[0].Equals("inspect", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage(stderr);
            return 2;
        }

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
        stderr.WriteLine("Usage: dotnet run --project src/ThroneForge.Discovery -- inspect --game-path <absolute-path> [--output-root <path>] [--overwrite]");
    }
}
