using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class SoundPackLoader
{
    private static readonly WaveFormat PlaybackWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private static readonly IReadOnlyDictionary<string, SoundSampleFormat> SupportedSampleFormats =
        new Dictionary<string, SoundSampleFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".wav"] = SoundSampleFormat.Wav,
            [".mp3"] = SoundSampleFormat.Mp3
        };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public IReadOnlyList<SoundPackMetadata> DiscoverPacks(string packsRoot)
    {
        if (!Directory.Exists(packsRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(packsRoot)
            .Select(TryLoadMetadata)
            .Where(pack => pack is not null)
            .Cast<SoundPackMetadata>()
            .OrderBy(pack => pack.Name)
            .ToList();
    }

    public SoundPackMetadata? TryLoadMetadata(string folderPath)
    {
        string metadataPath = Path.Combine(folderPath, "pack.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(metadataPath);
            SoundPackMetadata? metadata = JsonSerializer.Deserialize<SoundPackMetadata>(json, JsonOptions);
            if (metadata is null)
            {
                return null;
            }

            metadata.FolderPath = folderPath;
            return metadata;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public SoundPackValidationResult Validate(SoundPackMetadata? metadata)
    {
        SoundPackValidationResult result = new();
        if (metadata is null)
        {
            result.Errors.Add("Missing or invalid pack.json.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            result.Errors.Add("Pack id is required.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            result.Errors.Add("Pack name is required.");
        }

        if (!metadata.Groups.TryGetValue("normal", out List<string>? normalFiles) || normalFiles.Count == 0)
        {
            result.Errors.Add("Pack must include at least one normal sound.");
        }

        foreach ((string group, List<string> files) in metadata.Groups)
        {
            foreach (string relativePath in files)
            {
                if (!TryGetSampleFormat(relativePath, out _))
                {
                    result.Errors.Add($"{group}: {relativePath} has unsupported audio format. Supported formats: .wav, .mp3.");
                    continue;
                }

                string absolutePath = Path.Combine(metadata.FolderPath, relativePath);
                if (!File.Exists(absolutePath))
                {
                    result.Errors.Add($"{group}: {relativePath} was not found.");
                }
            }
        }

        return result;
    }

    public LoadedSoundPack Load(SoundPackMetadata metadata)
    {
        SoundPackValidationResult validation = Validate(metadata);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        Dictionary<string, IReadOnlyList<LoadedSoundSample>> samples = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string group, List<string> files) in metadata.Groups)
        {
            samples[group] = files
                .Select(path => LoadSample(metadata.FolderPath, path))
                .ToList();
        }

        return new LoadedSoundPack(metadata, samples);
    }

    private static LoadedSoundSample LoadSample(string folderPath, string relativePath)
    {
        string absolutePath = Path.Combine(folderPath, relativePath);
        SoundSampleFormat format = GetSampleFormat(relativePath);
        byte[] data = File.ReadAllBytes(absolutePath);
        float[] decoded = DecodeToPlaybackFormat(format, data);
        return new LoadedSoundSample(relativePath, format, data, decoded, PlaybackWaveFormat);
    }

    private static float[] DecodeToPlaybackFormat(SoundSampleFormat format, byte[] data)
    {
        try
        {
            using MemoryStream stream = new(data, writable: false);
            using WaveStream reader = format switch
            {
                SoundSampleFormat.Wav => new WaveFileReader(stream),
                SoundSampleFormat.Mp3 => new Mp3FileReader(stream),
                _ => throw new InvalidOperationException("Unsupported audio format.")
            };

            ISampleProvider provider = reader.ToSampleProvider();
            provider = EnsureStereo(provider);
            if (provider.WaveFormat.SampleRate != PlaybackWaveFormat.SampleRate)
            {
                provider = new WdlResamplingSampleProvider(provider, PlaybackWaveFormat.SampleRate);
            }

            List<float> samples = [];
            float[] buffer = new float[PlaybackWaveFormat.SampleRate / 10 * PlaybackWaveFormat.Channels];
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(buffer.Take(read));
            }

            return AudioSampleTrimmer.TrimLeadingSilence(samples.ToArray(), PlaybackWaveFormat.Channels);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException)
        {
            return [];
        }
    }

    private static ISampleProvider EnsureStereo(ISampleProvider provider)
    {
        return provider.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(provider),
            2 => provider,
            _ => throw new InvalidOperationException("SoundType supports mono or stereo samples.")
        };
    }

    private static bool TryGetSampleFormat(string relativePath, out SoundSampleFormat format) =>
        SupportedSampleFormats.TryGetValue(Path.GetExtension(relativePath), out format);

    private static SoundSampleFormat GetSampleFormat(string relativePath)
    {
        if (TryGetSampleFormat(relativePath, out SoundSampleFormat format))
        {
            return format;
        }

        throw new InvalidOperationException($"{relativePath} has unsupported audio format. Supported formats: .wav, .mp3.");
    }
}
