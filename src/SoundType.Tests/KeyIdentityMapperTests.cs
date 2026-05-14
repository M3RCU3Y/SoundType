using System.Windows.Input;
using SoundType.Input;

namespace SoundType.Tests;

public sealed class KeyIdentityMapperTests
{
    [Fact]
    public void CommonKeys_include_full_visual_keyboard_codes()
    {
        HashSet<string> codes = KeyIdentityMapper.CommonKeys
            .Select(key => key.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string expected in new[]
                 {
                     "Oem3", "OemMinus", "OemPlus", "OemOpenBrackets", "Oem6", "Oem5",
                     "Oem1", "OemQuotes", "OemComma", "OemPeriod", "OemQuestion",
                     "PrintScreen", "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
                     "NumLock", "NumPad0", "NumPad9", "Divide", "Multiply", "Subtract", "Add", "Decimal"
                 })
        {
            Assert.Contains(expected, codes);
        }
    }

    [Fact]
    public void FromVirtualKey_maps_print_screen_to_soundtype_code()
    {
        int virtualKey = KeyInterop.VirtualKeyFromKey(Key.Snapshot);

        Assert.Equal("PrintScreen", KeyIdentityMapper.FromVirtualKey(virtualKey).Code);
    }

    [Theory]
    [InlineData("OemQuestion", "/")]
    [InlineData("PageDown", "Page Down")]
    [InlineData("NumPad4", "Numpad 4")]
    public void GetDisplayName_returns_keyboard_labels(string code, string label)
    {
        Assert.Equal(label, KeyIdentityMapper.GetDisplayName(code));
    }
}
