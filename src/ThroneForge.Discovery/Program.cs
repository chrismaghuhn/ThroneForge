using ThroneForge.Discovery;

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0 || !args[0].Equals("inspect", StringComparison.OrdinalIgnoreCase))
    {
        PrintUsage();
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
                    Console.Error.WriteLine($"Unknown option '{args[index]}'.");
                    PrintUsage();
                    return 2;
            }
        }
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        PrintUsage();
        return 2;
    }

    if (string.IsNullOrWhiteSpace(gamePath))
    {
        Console.Error.WriteLine("--game-path is required.");
        PrintUsage();
        return 2;
    }

    try
    {
        var result = new DiscoveryEngine().Inspect(new DiscoveryRequest(gamePath, outputRoot, overwrite));
        Console.WriteLine($"Discovery report: {Path.GetFileName(result.ReportPath)}");
        Console.WriteLine($"Backend: {result.Backend}");
        Console.WriteLine($"Executable architecture: {result.ExecutableArchitecture}");
        Console.WriteLine($"Fingerprint: {result.Fingerprint}");
        return 0;
    }
    catch (DiscoveryException exception)
    {
        Console.Error.WriteLine($"Discovery failed: {exception.Message}");
        return 2;
    }
}

static string ReadOptionValue(string[] args, ref int index, string option)
{
    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
    {
        throw new ArgumentException($"{option} requires a value.");
    }

    index++;
    return args[index];
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet run --project src/ThroneForge.Discovery -- inspect --game-path <absolute-path> [--output-root <path>] [--overwrite]");
}
