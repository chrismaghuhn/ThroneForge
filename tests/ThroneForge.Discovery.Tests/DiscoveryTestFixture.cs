using System.Buffers.Binary;

namespace ThroneForge.Discovery.Tests;

internal sealed class DiscoveryTestFixture : IDisposable
{
    public DiscoveryTestFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), $"throneforge-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        OutputRoot = Path.Combine(Root, "reports");
    }

    public string Root { get; }

    public string OutputRoot { get; }

    public string DataRoot => Path.Combine(Root, "Thronefall_Data");

    public string MainExecutable => Path.Combine(Root, "Thronefall.exe");

    public void CreateMonoLayout(bool includeAssembly = true)
    {
        var managed = Path.Combine(DataRoot, "Managed");
        var monoRuntime = Path.Combine(DataRoot, "MonoBleedingEdge");
        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(monoRuntime);
        if (includeAssembly)
        {
            File.WriteAllText(Path.Combine(managed, "Assembly-CSharp.dll"), "synthetic mono assembly");
        }

        WriteMinimalPe(MainExecutable, 0x8664);
    }

    public void CreateIl2CppLayout()
    {
        var metadata = Path.Combine(DataRoot, "il2cpp_data", "Metadata");
        Directory.CreateDirectory(metadata);
        File.WriteAllBytes(Path.Combine(metadata, "global-metadata.dat"), [1, 2, 3, 4]);
        WriteMinimalPe(Path.Combine(Root, "GameAssembly.dll"), 0x8664);
    }

    public void CreateUnityVersion(string version = "2022.3.12f1")
    {
        File.WriteAllText(Path.Combine(DataRoot, "UnityVersion.txt"), version);
    }

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

    public bool TryCreateDirectoryLink(string linkName, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(Root, linkName), target);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
