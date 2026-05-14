using SoundType.Core.Models;

namespace SoundType.Input;

public sealed class KeyPressedEvent : EventArgs
{
    public required KeyIdentity Key { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required bool IsRepeat { get; init; }
}
