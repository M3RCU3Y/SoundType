using System.Text.Json;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class SoundPackLoader
{
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
                .Select(path => new LoadedSoundSample(
                    path,
                    GetSampleFormat(path),
                    File.ReadAllBytes(Path.Combine(metadata.FolderPath, path))))
                .ToList();
        }

        return new LoadedSoundPack(metadata, samples);
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
