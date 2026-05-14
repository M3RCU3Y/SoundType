namespace SoundType.Core.Models;

public sealed class PlaybackDecision
{
    public bool ShouldPlay { get; init; }
    public string? SoundGroup { get; init; }
    public double VolumeMultiplier { get; init; } = 1.0;
    public string? Reason { get; init; }

    public static PlaybackDecision Play(string soundGroup, double volumeMultiplier = 1.0) =>
        new() { ShouldPlay = true, SoundGroup = soundGroup, VolumeMultiplier = volumeMultiplier };

    public static PlaybackDecision Skip(string reason) =>
        new() { ShouldPlay = false, Reason = reason };
}
