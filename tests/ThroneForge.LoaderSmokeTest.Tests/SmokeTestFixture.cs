using System.IO.Compression;

namespace ThroneForge.LoaderSmokeTest.Tests;

internal sealed class SmokeTestFixture : IDisposable
{
    public SmokeTestFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), $"throneforge-loader-test-{Guid.NewGuid():N}");
        RepositoryRoot = Path.Combine(Root, "repository");
        GameRoot = Path.Combine(Root, "game");
        ExperimentRoot = Path.Combine(Root, "experiments", "profile");
        Directory.CreateDirectory(RepositoryRoot);
        Directory.CreateDirectory(GameRoot);
        File.WriteAllText(Path.Combine(GameRoot, "game.txt"), "game");
    }

    public string Root { get; }

    public string RepositoryRoot { get; }

    public string GameRoot { get; }

    public string ExperimentRoot { get; }

    public string ArchivePath => Path.Combine(Root, "loader.zip");

    public void WriteArchive(params (string Name, string Content)[] entries)
    {
        using var archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry.Name).Open());
            writer.Write(entry.Content);
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
