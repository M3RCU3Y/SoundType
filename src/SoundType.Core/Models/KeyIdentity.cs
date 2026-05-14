namespace SoundType.Core.Models;

public sealed record KeyIdentity(
    string Code,
    string DisplayName,
    KeyCategory Category);
