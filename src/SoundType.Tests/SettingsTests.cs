using SoundType.Core.Models;
using SoundType.Core.Settings;

namespace SoundType.Tests;

public sealed class SettingsTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileIsMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        SettingsService service = new(path);

        AppSettings settings = await service.LoadAsync();

        Assert.True(settings.Enabled);
        Assert.Equal(0.75, settings.MasterVolume);
        Assert.Equal(0.02, settings.PitchVariation);
        Assert.Contains("LeftShift", settings.ExcludedKeys);
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsCoreSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "settings.json");
        SettingsService service = new(path);
        AppSettings original = new()
        {
            Enabled = false,
            MasterVolume = 0.25,
            PitchVariation = 0.08,
            ActiveSoundPackId = "soft-laptop"
        };
        original.ExcludedKeys.Add("Tab");
        original.AppRules.Add(new AppRule { ProcessName = "Code.exe", Mode = AppRuleMode.Disabled });

        await service.SaveAsync(original);
        AppSettings restored = await service.LoadAsync();

        Assert.False(restored.Enabled);
        Assert.Equal(0.25, restored.MasterVolume);
        Assert.Equal(0.08, restored.PitchVariation);
        Assert.Equal("soft-laptop", restored.ActiveSoundPackId);
        Assert.Contains("Tab", restored.ExcludedKeys);
        Assert.Contains(restored.AppRules, rule => rule.ProcessName == "Code.exe");
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenJsonIsInvalid()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(path, "{ not-json");
        SettingsService service = new(path);

        AppSettings settings = await service.LoadAsync();

        Assert.True(settings.Enabled);
        Assert.Equal("classic-typewriter", settings.ActiveSoundPackId);
    }

    [Fact]
    public async Task LoadAsync_ClampsPitchVariation()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.json");
        await File.WriteAllTextAsync(path, """
            {
              "pitchVariation": 0.4
            }
            """);
        SettingsService service = new(path);

        AppSettings settings = await service.LoadAsync();

        Assert.Equal(0.12, settings.PitchVariation);
    }
}
