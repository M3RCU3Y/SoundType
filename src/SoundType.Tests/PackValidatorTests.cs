using System.IO.Compression;
using SoundType.PackValidator;

namespace SoundType.Tests;

public sealed class PackValidatorTests
{
    [Fact]
    public void Run_ReturnsSuccessForValidFolderPack()
    {
        string pack = CreateValidPack("test-pack", "Test Pack");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = PackValidatorCommand.Run([pack], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Valid sound pack", output.ToString());
        Assert.Contains("test-pack", output.ToString());
        Assert.Contains("Test Pack", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_ReturnsFailureAndErrorsForInvalidFolderPack()
    {
        string pack = CreateTempDirectory();
        File.WriteAllText(Path.Combine(pack, "pack.json"), """
            {
              "id": "",
              "name": "Broken",
              "groups": {}
            }
            """);
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = PackValidatorCommand.Run([pack], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid sound pack", error.ToString());
        Assert.Contains("Pack id is required", error.ToString());
        Assert.Contains("normal", error.ToString());
    }

    [Fact]
    public void Run_ValidatesArchiveInTemporaryFolderAndCleansItUp()
    {
        string pack = CreateValidPack("archive-pack", "Archive Pack");
        string archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.soundpack");
        ZipFile.CreateFromDirectory(pack, archivePath);
        string tempRoot = CreateTempDirectory();
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = PackValidatorCommand.Run([archivePath], output, error, tempRoot);

        Assert.Equal(0, exitCode);
        Assert.Contains("archive-pack", output.ToString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(tempRoot));
        Assert.Equal(string.Empty, error.ToString());
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
