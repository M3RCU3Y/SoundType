namespace SoundType.App.ViewModels;

internal sealed class EnterDingSoundListItem(string soundGroup, string displayName)
{
    public string SoundGroup { get; } = soundGroup;

    public override string ToString() => displayName;
}
