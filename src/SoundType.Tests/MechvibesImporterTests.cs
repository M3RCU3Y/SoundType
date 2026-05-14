using System.Text.Json;
using SoundType.Core.Models;
using SoundType.MechvibesImporter;

namespace SoundType.Tests;

public sealed class MechvibesImporterTests
{
    [Fact]
    public void Import_ConvertsMultiFilePackToSoundTypeManifest()
    {
        string source = CreateMechvibesPack("""
            {
              "id": "custom-sound-pack-42",
              "name": "Test Switches",
              "key_define_type": "multi",
              "includes_numpad": false,
              "sound": "normal.wav",
              "defines": {
                "14": "backspace.wav",
                "14-up": "release/backspace.wav",
                "15": "tab.wav",
                "28": "enter.wav",
                "57": "space.wav",
                "30": "a.wav",
                "31": "a.wav",
                "32": "nested/d.wav"
              },
              "version": 2
            }
            """,
            "normal.wav",
            "backspace.wav",
            "tab.wav",
            "enter.wav",
            "space.wav",
            "a.wav",
            "nested/d.wav",
            "release/backspace.wav");
        string output = CreateEmptyDirectory();

        MechvibesImportResult result = new MechvibesPackImporter().Import(source, output);

        Assert.Empty(result.Warnings);
        Assert.Equal("mechvibes-custom-sound-pack-42", result.Metadata.Id);
        Assert.Equal("Test Switches", result.Metadata.Name);
        Assert.Equal("Imported from Mechvibes config.json.", result.Metadata.Description);
        Assert.Equal("Check the original Mechvibes pack license before redistributing.", result.Metadata.License);
        Assert.Equal(["samples/normal.wav", "samples/a.wav", "samples/d.wav"], result.Metadata.Groups["normal"]);
        Assert.Equal(["samples/backspace.wav"], result.Metadata.Groups["backspace"]);
        Assert.Equal(["samples/enter.wav"], result.Metadata.Groups["enter"]);
        Assert.Equal(["samples/space.wav"], result.Metadata.Groups["space"]);
        Assert.Equal(["samples/tab.wav"], result.Metadata.Groups["tab"]);
        Assert.Equal("backspace", result.Metadata.KeyOverrides["Backspace"]);
        Assert.Equal("enter", result.Metadata.KeyOverrides["Enter"]);
        Assert.Equal("space", result.Metadata.KeyOverrides["Space"]);
        Assert.Equal("tab", result.Metadata.KeyOverrides["Tab"]);

        SoundPackMetadata manifest = ReadManifest(output);
        Assert.Equal(result.Metadata.Id, manifest.Id);
        Assert.True(File.Exists(Path.Combine(output, "samples", "normal.wav")));
        Assert.True(File.Exists(Path.Combine(output, "samples", "a.wav")));
        Assert.True(File.Exists(Path.Combine(output, "samples", "d.wav")));
        Assert.False(File.Exists(Path.Combine(output, "samples", "release", "backspace.wav")));
    }

    [Fact]
    public void Import_FailsWithUsefulErrorWhenOnlyUnsupportedAudioIsAvailable()
    {
        string source = CreateMechvibesPack("""
            {
              "id": "mp3-pack",
              "name": "MP3 Pack",
              "key_define_type": "multi",
              "sound": "GENERIC_R{0-1}.mp3",
              "defines": {
                "14": "BACKSPACE.mp3"
              },
              "version": 2
            }
            """,
            "GENERIC_R0.mp3",
            "GENERIC_R1.mp3",
            "BACKSPACE.mp3");
        string output = CreateEmptyDirectory();

        var ex = Assert.Throws<InvalidOperationException>(() => new MechvibesPackImporter().Import(source, output));

        Assert.Contains("SoundType currently validates imported packs as .wav-only", ex.Message);
        Assert.Contains("GENERIC_R0.mp3", ex.Message);
        Assert.Contains("BACKSPACE.mp3", ex.Message);
        Assert.False(File.Exists(Path.Combine(output, "pack.json")));
    }

    [Fact]
    public void Import_WarnsAndSkipsUnsupportedAudioWhenWavSamplesRemain()
    {
        string source = CreateMechvibesPack("""
            {
              "id": "mixed-pack",
              "name": "Mixed Pack",
              "key_define_type": "multi",
              "sound": "normal.wav",
              "defines": {
                "30": "a.wav",
                "31": "b.mp3"
              },
              "version": 2
            }
            """,
            "normal.wav",
            "a.wav",
            "b.mp3");
        string output = CreateEmptyDirectory();

        MechvibesImportResult result = new MechvibesPackImporter().Import(source, output);

        string warning = Assert.Single(result.Warnings);
        Assert.Contains("SoundType currently validates imported packs as .wav-only", warning);
        Assert.Contains("b.mp3", warning);
        Assert.DoesNotContain(result.Metadata.Groups["normal"], sample => sample.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Import_FailsForSingleSpritePack()
    {
        string source = CreateMechvibesPack("""
            {
              "id": "sprite-pack",
              "name": "Sprite Pack",
              "key_define_type": "single",
              "sound": "sound.ogg",
              "defines": {
                "30": [100, 200]
              }
            }
            """,
            "sound.ogg");
        string output = CreateEmptyDirectory();

        var ex = Assert.Throws<InvalidOperationException>(() => new MechvibesPackImporter().Import(source, output));

        Assert.Contains("single/sprite Mechvibes packs are not supported", ex.Message);
    }

    [Fact]
    public void Import_FailsWhenOutputFolderWouldOverwriteSource()
    {
        string source = CreateMechvibesPack("""
            {
              "id": "source-pack",
              "name": "Source Pack",
              "key_define_type": "multi",
              "sound": "normal.wav",
              "defines": {},
              "version": 2
            }
            """,
            "normal.wav");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new MechvibesPackImporter().Import(source, source, new MechvibesImportOptions(Overwrite: true)));

        Assert.Contains("Output folder must be separate from the source Mechvibes pack", ex.Message);
        Assert.True(File.Exists(Path.Combine(source, "config.json")));
    }

    private static SoundPackMetadata ReadManifest(string output)
    {
        string json = File.ReadAllText(Path.Combine(output, "pack.json"));
        return JsonSerializer.Deserialize<SoundPackMetadata>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("pack.json did not deserialize.");
    }

    private static string CreateMechvibesPack(string configJson, params string[] audioFiles)
    {
        string root = CreateEmptyDirectory();
        File.WriteAllText(Path.Combine(root, "config.json"), configJson);

        foreach (string audioFile in audioFiles)
        {
            string path = Path.Combine(root, audioFile.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [1, 2, 3, 4]);
        }

        return root;
    }

    private static string CreateEmptyDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
