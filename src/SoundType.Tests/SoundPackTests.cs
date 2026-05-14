using SoundType.Audio;

namespace SoundType.Tests;

public sealed class SoundPackTests
{
    [Fact]
    public void Validate_Fails_WhenNormalGroupIsMissing()
    {
        string root = CreatePackRoot("""
            {
              "id": "broken",
              "name": "Broken",
              "groups": {}
            }
            """);
        SoundPackLoader loader = new();

        var result = loader.Validate(loader.TryLoadMetadata(root));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("normal"));
    }

    [Fact]
    public void Validate_Fails_WhenAudioFileIsMissing()
    {
        string root = CreatePackRoot("""
            {
              "id": "missing-audio",
              "name": "Missing Audio",
              "groups": {
                "normal": [ "normal/key01.wav" ]
              }
            }
            """);
        SoundPackLoader loader = new();

        var result = loader.Validate(loader.TryLoadMetadata(root));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("was not found"));
    }

    [Fact]
    public void DiscoverPacks_ReturnsValidMetadataFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "PackOne");
        Directory.CreateDirectory(Path.Combine(pack, "normal"));
        File.WriteAllText(Path.Combine(pack, "normal", "key.wav"), "not a real wav but present for metadata validation");
        File.WriteAllText(Path.Combine(pack, "pack.json"), """
            {
              "id": "pack-one",
              "name": "Pack One",
              "groups": {
                "normal": [ "normal/key.wav" ]
              }
            }
            """);
        SoundPackLoader loader = new();

        var packs = loader.DiscoverPacks(root);

        Assert.Single(packs);
        Assert.Equal("pack-one", packs[0].Id);
    }

    private static string CreatePackRoot(string json)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "pack.json"), json);
        return root;
    }
}
