using System.IO.Compression;
using SoundType.Audio;

namespace SoundType.Tests;

public sealed class SoundPackArchiveTests
{
    [Fact]
    public void ExportThenImport_RoundTripsFolderPack()
    {
        string sourcePack = CreateValidPack("rainy-typewriter", "Rainy Typewriter");
        string archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.soundpack");
        string packsRoot = CreateTempDirectory();
        SoundPackArchiveService service = new();

        service.ExportPack(sourcePack, archivePath);
        var metadata = service.ImportPack(archivePath, packsRoot, overwrite: false);

        Assert.True(File.Exists(archivePath));
        Assert.Equal("rainy-typewriter", metadata.Id);
        Assert.Equal("Rainy Typewriter", metadata.Name);
        Assert.Equal(Path.Combine(packsRoot, "rainy-typewriter"), metadata.FolderPath);
        Assert.True(File.Exists(Path.Combine(metadata.FolderPath, "pack.json")));
        Assert.True(File.Exists(Path.Combine(metadata.FolderPath, "normal", "key.wav")));
    }

    [Fact]
    public void ImportPack_RefusesToOverwriteExistingPack_WhenOverwriteIsFalse()
    {
        string sourcePack = CreateValidPack("clicks", "Clicks");
        string archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.soundpack");
        string packsRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(packsRoot, "clicks"));
        SoundPackArchiveService service = new();
        service.ExportPack(sourcePack, archivePath);

        var exception = Assert.Throws<IOException>(() =>
        {
            service.ImportPack(archivePath, packsRoot, overwrite: false);
        });

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportPack_RejectsPathTraversalEntries()
    {
        string archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        string packsRoot = CreateTempDirectory();
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("pack.json");
            archive.CreateEntry("../escape.wav");
        }
        SoundPackArchiveService service = new();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            service.ImportPack(archivePath, packsRoot, overwrite: false);
        });

        Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateValidPack(string id, string name)
    {
        string root = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "normal"));
        File.WriteAllText(Path.Combine(root, "normal", "key.wav"), "placeholder wav");
        File.WriteAllText(Path.Combine(root, "pack.json"), $$"""
            {
              "id": "{{id}}",
              "name": "{{name}}",
              "groups": {
                "normal": [ "normal/key.wav" ]
              }
            }
            """);
        return root;
    }

    private static string CreateTempDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
