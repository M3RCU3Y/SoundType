using SoundType.Input;

namespace SoundType.Tests;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void TryParse_CtrlAltL_ReturnsGesture()
    {
        bool parsed = HotkeyGesture.TryParse("Ctrl+Alt+L", out HotkeyGesture gesture);

        Assert.True(parsed);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, gesture.Modifiers);
        Assert.Equal(0x4C, gesture.VirtualKey);
        Assert.Equal("Ctrl+Alt+L", HotkeyGesture.DefaultText);
        Assert.Equal(0x4C, HotkeyGesture.Default.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+NotAKey")]
    [InlineData("K")]
    public void TryParse_InvalidStrings_ReturnsFalse(string value)
    {
        bool parsed = HotkeyGesture.TryParse(value, out HotkeyGesture gesture);

        Assert.False(parsed);
        Assert.Equal(default, gesture);
    }
}
