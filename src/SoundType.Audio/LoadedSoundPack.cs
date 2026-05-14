using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class LoadedSoundPack
{
    public LoadedSoundPack(SoundPackMetadata metadata, IReadOnlyDictionary<string, IReadOnlyList<LoadedSoundSample>> samples)
    {
        Metadata = metadata;
        Samples = samples;
    }

    public SoundPackMetadata Metadata { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<LoadedSoundSample>> Samples { get; }
}
