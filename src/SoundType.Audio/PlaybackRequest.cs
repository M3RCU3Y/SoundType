using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class PlaybackRequest
{
    public required string SoundGroup { get; init; }
    public required KeyIdentity Key { get; init; }
    public double VolumeMultiplier { get; init; } = 1.0;
    public string? ActiveProcessName { get; init; }
}
