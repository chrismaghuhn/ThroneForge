using System.Buffers.Binary;

namespace ThroneForge.Discovery.Tests;

internal sealed class DiscoveryTestFixture : IDisposable
{
    private readonly List<string> externalDirectories = [];
    private readonly List<string> externalLinks = [];

    public DiscoveryTestFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), $"throneforge-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        OutputRoot = CreateExternalOutputRoot("reports");
    }

    public string Root { get; }

    public string OutputRoot { get; }

    public string DataRoot => Path.Combine(Root, "Thronefall_Data");

    public string MainExecutable => Path.Combine(Root, "Thronefall.exe");

    public string CreateExternalOutputRoot(string name)
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"throneforge-discovery-output-{name}-{Guid.NewGuid():N}");
        externalDirectories.Add(outputRoot);
        return outputRoot;
    }

    public bool TryCreateExternalDirectoryLink(out string linkPath)
    {
        var target = CreateExternalOutputRoot("symlink-target");
        Directory.CreateDirectory(target);
        linkPath = Path.Combine(Path.GetTempPath(), $"throneforge-discovery-output-link-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateSymbolicLink(linkPath, target);
            externalLinks.Add(linkPath);
            return true;
        }
        catch (IOException)
        {
            linkPath = string.Empty;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            linkPath = string.Empty;
            return false;
        }
    }

    public void CreateMonoLayout(
        bool includeAssembly = true,
        string executableName = "Thronefall.exe",
        string dataDirectoryName = "Thronefall_Data")
    {
        var dataRoot = Path.Combine(Root, dataDirectoryName);
        var managed = Path.Combine(dataRoot, "Managed");
        var monoRuntime = Path.Combine(dataRoot, "MonoBleedingEdge");
        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(monoRuntime);
        if (includeAssembly)
        {
            File.WriteAllText(Path.Combine(managed, "Assembly-CSharp.dll"), "synthetic mono assembly");
        }

        WriteMinimalPe(Path.Combine(Root, executableName), 0x8664);
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
        foreach (var link in externalLinks)
        {
            if (Directory.Exists(link) || File.Exists(link))
            {
                Directory.Delete(link);
            }
        }

        foreach (var directory in externalDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
