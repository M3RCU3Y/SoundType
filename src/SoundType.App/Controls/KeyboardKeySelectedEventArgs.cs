namespace SoundType.App.Controls;

public sealed class KeyboardKeySelectedEventArgs(string code, string displayName, bool isExcluded) : EventArgs
{
    public string Code { get; } = code;
    public string DisplayName { get; } = displayName;
    public bool IsExcluded { get; } = isExcluded;
}
