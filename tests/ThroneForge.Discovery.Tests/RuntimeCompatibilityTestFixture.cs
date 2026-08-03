using System.Buffers.Binary;
using ThroneForge.Discovery;

namespace ThroneForge.Discovery.Tests;

internal sealed class RuntimeCompatibilityTestFixture : IDisposable
{
    private readonly string outputPrefix = $"throneforge-runtime-output-{Guid.NewGuid():N}";

    public RuntimeCompatibilityTestFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), $"throneforge-runtime-{Guid.NewGuid():N}");
        OutputRoot = Path.Combine(Path.GetTempPath(), outputPrefix);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string OutputRoot { get; }

    public string DataRoot => Path.Combine(Root, "Game_Data");

    public string WriteCandidate(string relativePath, string content)
    {
        var path = PreparePath(relativePath);
        File.WriteAllText(path, content);
        return path;
    }

    public string WriteCandidate(string relativePath, byte[] content)
    {
        var path = PreparePath(relativePath);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void CreateDirectory(string relativePath)
        => Directory.CreateDirectory(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public void CreateMonoLayout()
    {
        Directory.CreateDirectory(Path.Combine(DataRoot, "Managed"));
        Directory.CreateDirectory(Path.Combine(DataRoot, "MonoBleedingEdge"));
        WriteCandidate("Game_Data/Managed/Assembly-CSharp.dll", [0x4D, 0x5A, 0x00]);
        WriteMinimalPe(Path.Combine(Root, "Thronefall.exe"), 0x8664);
    }

    public RuntimeCompatibilityResult Inspect(
        string fingerprint,
        DateTimeOffset? timestamp = null,
        Func<string, string?>? versionResourceReader = null)
        => new RuntimeCompatibilityEngine(versionResourceReader).Inspect(new RuntimeCompatibilityRequest(
            Root,
            fingerprint,
            OutputRoot,
            DiscoveryTimestampUtc: timestamp ?? new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));

    public static ManagedAssemblyEvidence ManagedAssembly(
        string name,
        Version version,
        string? targetFramework)
        => new(
            $"Game_Data/Managed/{name}.dll",
            true,
            name,
            version,
            targetFramework,
            [],
            null);

    public static void WriteMinimalPe(string path, ushort machine)
    {
        var bytes = new byte[0x100];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x3C), 0x80);
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), machine);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }

        if (Directory.Exists(OutputRoot))
        {
            Directory.Delete(OutputRoot, recursive: true);
        }
    }

    private string PreparePath(string relativePath)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        return path;
    }
}
