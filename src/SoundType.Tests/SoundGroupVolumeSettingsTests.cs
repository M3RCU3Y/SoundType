using SoundType.Core.Models;

namespace SoundType.Tests;

public sealed class SoundGroupVolumeSettingsTests
{
    [Theory]
    [InlineData("normal", 0.7)]
    [InlineData("enter", 0.8)]
    [InlineData("space", 0.9)]
    [InlineData("backspace", 1.1)]
    [InlineData("tab", 0.6)]
    [InlineData("missing", 0.7)]
    [InlineData(null, 0.7)]
    public void GetVolumeForGroup_ReturnsConfiguredGroupVolume(string? group, double expected)
    {
        SoundGroupVolumeSettings settings = new()
        {
            Normal = 0.7,
            Enter = 0.8,
            Space = 0.9,
            Backspace = 1.1,
            Tab = 0.6
        };

        Assert.Equal(expected, settings.GetVolumeForGroup(group));
    }

    [Fact]
    public void Clamp_ConstrainsAllVolumesToSafeRange()
    {
        SoundGroupVolumeSettings settings = new()
        {
            Normal = -1,
            Enter = 2,
            Space = 0.5,
            Backspace = 1.25,
            Tab = 1.75
        };

        settings.Clamp();

        Assert.Equal(0, settings.Normal);
        Assert.Equal(1.5, settings.Enter);
        Assert.Equal(0.5, settings.Space);
        Assert.Equal(1.25, settings.Backspace);
        Assert.Equal(1.5, settings.Tab);
    }
}
