using SoundType.Audio;
using SoundType.Core.Models;
using NAudio.Wave;

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
    public void Validate_AllowsWavAndMp3SampleFiles()
    {
        string root = CreatePackRoot("""
            {
              "id": "mixed-formats",
              "name": "Mixed Formats",
              "groups": {
                "normal": [ "normal/key01.wav", "normal/key02.mp3" ]
              }
            }
            """);
        Directory.CreateDirectory(Path.Combine(root, "normal"));
        File.WriteAllBytes(Path.Combine(root, "normal", "key01.wav"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(root, "normal", "key02.mp3"), [4, 5, 6]);
        SoundPackLoader loader = new();

        var result = loader.Validate(loader.TryLoadMetadata(root));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WithClearMessage_WhenAudioExtensionIsUnsupported()
    {
        string root = CreatePackRoot("""
            {
              "id": "unsupported-format",
              "name": "Unsupported Format",
              "groups": {
                "normal": [ "normal/key01.ogg" ]
              }
            }
            """);
        Directory.CreateDirectory(Path.Combine(root, "normal"));
        File.WriteAllBytes(Path.Combine(root, "normal", "key01.ogg"), [1, 2, 3]);
        SoundPackLoader loader = new();

        var result = loader.Validate(loader.TryLoadMetadata(root));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("normal/key01.ogg") &&
            error.Contains(".wav") &&
            error.Contains(".mp3"));
    }

    [Fact]
    public void Load_PreservesSampleFormatMetadata()
    {
        string root = CreatePackRoot("""
            {
              "id": "mixed-formats",
              "name": "Mixed Formats",
              "groups": {
                "normal": [ "normal/key01.wav", "normal/key02.mp3" ]
              }
            }
            """);
        Directory.CreateDirectory(Path.Combine(root, "normal"));
        File.WriteAllBytes(Path.Combine(root, "normal", "key01.wav"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(root, "normal", "key02.mp3"), [4, 5, 6]);
        SoundPackLoader loader = new();
        var metadata = loader.TryLoadMetadata(root)!;

        LoadedSoundPack pack = loader.Load(metadata);

        Assert.Collection(
            pack.Samples["normal"],
            sample =>
            {
                Assert.Equal("normal/key01.wav", sample.RelativePath);
                Assert.Equal(SoundSampleFormat.Wav, sample.Format);
                Assert.Equal([1, 2, 3], sample.Data);
            },
            sample =>
            {
                Assert.Equal("normal/key02.mp3", sample.RelativePath);
                Assert.Equal(SoundSampleFormat.Mp3, sample.Format);
                Assert.Equal([4, 5, 6], sample.Data);
            });
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

    [Fact]
    public void TryLoadMetadata_ReadsPackTags()
    {
        string root = CreatePackRoot("""
            {
              "id": "tagged-pack",
              "name": "Tagged Pack",
              "tags": [ "switch", "clicky" ],
              "groups": {
                "normal": [ "normal/key.wav" ]
              }
            }
            """);
        SoundPackLoader loader = new();

        var metadata = loader.TryLoadMetadata(root);

        Assert.NotNull(metadata);
        Assert.Equal(["switch", "clicky"], metadata.Tags);
    }

    [Fact]
    public void BuiltInSoundPacks_AllValidateAndDecodeAudio()
    {
        string packsRoot = Path.Combine(FindRepositoryRoot(), "assets", "packs");
        SoundPackLoader loader = new();

        IReadOnlyList<SoundPackMetadata> packs = loader.DiscoverPacks(packsRoot);

        Assert.NotEmpty(packs);
        foreach (SoundPackMetadata pack in packs)
        {
            var validation = loader.Validate(pack);
            Assert.True(validation.IsValid, $"{pack.Id}: {string.Join("; ", validation.Errors)}");

            LoadedSoundPack loaded = loader.Load(pack);
            Assert.True(
                loaded.Samples.Values.SelectMany(samples => samples).All(sample => sample.DecodedSamples.Length > 0),
                $"{pack.Id} has at least one sample that failed to decode.");
        }
    }

    [Fact]
    public void Load_CentersStereoSamplesBeforePlayback()
    {
        string root = CreatePackRoot("""
            {
              "id": "stereo-pack",
              "name": "Stereo Pack",
              "groups": {
                "normal": [ "normal/key.wav" ]
              }
            }
            """);
        string normalDir = Path.Combine(root, "normal");
        Directory.CreateDirectory(normalDir);
        string samplePath = Path.Combine(normalDir, "key.wav");
        using (WaveFileWriter writer = new(samplePath, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)))
        {
            writer.WriteSamples([0.8f, 0.2f, -0.4f, 0.2f, 0.6f, -0.2f], 0, 6);
        }

        SoundPackLoader loader = new();
        LoadedSoundPack pack = loader.Load(loader.TryLoadMetadata(root)!);

        float[] decoded = pack.Samples["normal"][0].DecodedSamples;
        Assert.NotEmpty(decoded);
        for (int i = 0; i + 1 < decoded.Length; i += 2)
        {
            Assert.Equal(decoded[i], decoded[i + 1], precision: 6);
        }
    }

    [Fact]
    public void BuiltInSwitchPacks_HavePreviewPngArtwork()
    {
        string packsRoot = Path.Combine(FindRepositoryRoot(), "assets", "packs");
        SoundPackLoader loader = new();

        IReadOnlyList<SoundPackMetadata> packs = loader.DiscoverPacks(packsRoot)
            .Where(pack => pack.Tags.Any(tag => tag.Equals("switch", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.NotEmpty(packs);
        foreach (SoundPackMetadata pack in packs)
        {
            Assert.Equal("preview.png", pack.PreviewImage);
            string previewPath = Path.Combine(pack.FolderPath, pack.PreviewImage!);
            Assert.True(File.Exists(previewPath), $"{pack.Id} is missing preview.png.");
            Assert.True(new FileInfo(previewPath).Length > 0, $"{pack.Id} preview.png is empty.");
        }
    }

    private static string CreatePackRoot(string json)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "pack.json"), json);
        return root;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SoundType.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SoundType repository root.");
    }
}
