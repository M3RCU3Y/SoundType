using System.Text.Json;
using System.Text.RegularExpressions;
using SoundType.Core.Models;

namespace SoundType.MechvibesImporter;

public sealed class MechvibesPackImporter
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly Regex RangePattern = new(@"\{(?<start>\d+)-(?<end>\d+)\}", RegexOptions.Compiled);

    public MechvibesImportResult Import(string inputFolder, string outputFolder, MechvibesImportOptions? options = null)
    {
        options ??= new MechvibesImportOptions();

        string inputRoot = Path.GetFullPath(inputFolder);
        string outputRoot = Path.GetFullPath(outputFolder);
        ValidateOutputIsSeparate(inputRoot, outputRoot);
        if (!Directory.Exists(inputRoot))
        {
            throw new DirectoryNotFoundException($"Mechvibes pack folder was not found: {inputRoot}");
        }

        string configPath = Path.Combine(inputRoot, "config.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException("Mechvibes pack folder must contain config.json.");
        }

        using JsonDocument config = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonElement root = config.RootElement;
        string keyDefineType = GetString(root, "key_define_type") ?? "";
        if (string.Equals(keyDefineType, "single", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("single/sprite Mechvibes packs are not supported because SoundType pack conversion cannot safely slice sprite timing data yet.");
        }

        if (!string.Equals(keyDefineType, "multi", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only multi-file Mechvibes packs are supported.");
        }

        List<string> warnings = [];
        List<SampleReference> accepted = [];
        List<string> unsupported = [];

        AddSampleReferences(inputRoot, "normal", GetString(root, "sound"), accepted, unsupported, warnings);
        if (root.TryGetProperty("defines", out JsonElement defines) && defines.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in defines.EnumerateObject())
            {
                if (property.Name.EndsWith("-up", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string group = MapMechvibesKeyCodeToGroup(property.Name);
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    warnings.Add($"Skipped key {property.Name}: expected a sample file path.");
                    continue;
                }

                AddSampleReferences(inputRoot, group, property.Value.GetString(), accepted, unsupported, warnings);
            }
        }

        if (!accepted.Any(reference => string.Equals(reference.Group, "normal", StringComparison.OrdinalIgnoreCase)))
        {
            string details = unsupported.Count == 0
                ? "No supported normal .wav or .mp3 samples were found."
                : $"SoundType supports imported .wav and .mp3 samples only. Unsupported referenced samples: {string.Join(", ", unsupported.Distinct(StringComparer.OrdinalIgnoreCase))}.";
            throw new InvalidOperationException(details);
        }

        if (unsupported.Count > 0)
        {
            warnings.Add($"Skipped unsupported audio files because SoundType imports .wav and .mp3 samples only: {string.Join(", ", unsupported.Distinct(StringComparer.OrdinalIgnoreCase))}.");
        }

        PrepareOutputFolder(outputRoot, options.Overwrite);

        Dictionary<string, string> copiedSamples = CopySamples(outputRoot, accepted);
        Dictionary<string, List<string>> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (SampleReference reference in accepted)
        {
            string relativeOutput = copiedSamples[reference.FullPath];
            if (!groups.TryGetValue(reference.Group, out List<string>? groupSamples))
            {
                groupSamples = [];
                groups[reference.Group] = groupSamples;
            }

            if (!groupSamples.Contains(relativeOutput, StringComparer.OrdinalIgnoreCase))
            {
                groupSamples.Add(relativeOutput);
            }
        }

        SoundPackMetadata metadata = new()
        {
            Id = BuildPackId(GetString(root, "id"), GetString(root, "name"), inputRoot),
            Name = GetString(root, "name") ?? Path.GetFileName(inputRoot),
            Author = "Mechvibes import",
            Version = "1.0.0",
            Description = "Imported from Mechvibes config.json.",
            License = "Check the original Mechvibes pack license before redistributing.",
            Groups = groups,
            KeyOverrides = BuildKeyOverrides(groups),
            Defaults = new SoundPackDefaults
            {
                Randomize = true
            },
            FolderPath = outputRoot
        };

        string manifestPath = Path.Combine(outputRoot, "pack.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(metadata, ManifestJsonOptions));

        return new MechvibesImportResult(metadata, manifestPath, warnings);
    }

    private static void AddSampleReferences(
        string inputRoot,
        string group,
        string? rawPath,
        List<SampleReference> accepted,
        List<string> unsupported,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return;
        }

        foreach (string samplePath in ExpandSamplePath(rawPath))
        {
            string absolutePath = ResolvePackPath(inputRoot, samplePath);
            if (!File.Exists(absolutePath))
            {
                warnings.Add($"Referenced sample was not found and was skipped: {samplePath}");
                continue;
            }

            if (!IsSupportedSampleExtension(absolutePath))
            {
                unsupported.Add(samplePath);
                continue;
            }

            accepted.Add(new SampleReference(group, absolutePath));
        }
    }

    private static IReadOnlyList<string> ExpandSamplePath(string samplePath)
    {
        Match match = RangePattern.Match(samplePath);
        if (!match.Success)
        {
            return [samplePath];
        }

        int start = int.Parse(match.Groups["start"].Value);
        int end = int.Parse(match.Groups["end"].Value);
        if (end < start)
        {
            return [samplePath];
        }

        List<string> expanded = [];
        for (int value = start; value <= end; value++)
        {
            expanded.Add(RangePattern.Replace(samplePath, value.ToString(), 1));
        }

        return expanded;
    }

    private static void ValidateOutputIsSeparate(string inputRoot, string outputRoot)
    {
        string normalizedInput = NormalizeDirectoryForComparison(inputRoot);
        string normalizedOutput = NormalizeDirectoryForComparison(outputRoot);

        bool sameDirectory = string.Equals(normalizedInput, normalizedOutput, StringComparison.OrdinalIgnoreCase);
        bool outputInsideInput = normalizedOutput.StartsWith(normalizedInput + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        bool inputInsideOutput = normalizedInput.StartsWith(normalizedOutput + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (sameDirectory || outputInsideInput || inputInsideOutput)
        {
            throw new InvalidOperationException("Output folder must be separate from the source Mechvibes pack.");
        }
    }

    private static string NormalizeDirectoryForComparison(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string ResolvePackPath(string inputRoot, string samplePath)
    {
        string normalizedSamplePath = samplePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedSamplePath))
        {
            throw new InvalidOperationException($"Sample path must be relative to the Mechvibes pack: {samplePath}");
        }

        string fullPath = Path.GetFullPath(Path.Combine(inputRoot, normalizedSamplePath));
        string inputRootWithSeparator = inputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(inputRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Sample path escapes the Mechvibes pack folder: {samplePath}");
        }

        return fullPath;
    }

    private static bool IsSupportedSampleExtension(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".wav" or ".mp3";

    private static void PrepareOutputFolder(string outputRoot, bool overwrite)
    {
        if (!Directory.Exists(outputRoot))
        {
            Directory.CreateDirectory(outputRoot);
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(outputRoot).Any())
        {
            return;
        }

        if (!overwrite)
        {
            throw new InvalidOperationException("Output folder already contains files. Pass --overwrite to replace its contents.");
        }

        foreach (string file in Directory.EnumerateFiles(outputRoot))
        {
            File.Delete(file);
        }

        foreach (string directory in Directory.EnumerateDirectories(outputRoot))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Dictionary<string, string> CopySamples(string outputRoot, IReadOnlyList<SampleReference> accepted)
    {
        string samplesRoot = Path.Combine(outputRoot, "samples");
        Directory.CreateDirectory(samplesRoot);

        Dictionary<string, string> copiedSamples = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SampleReference reference in accepted)
        {
            if (copiedSamples.ContainsKey(reference.FullPath))
            {
                continue;
            }

            string destinationFileName = BuildUniqueFileName(reference.FullPath, usedRelativePaths);
            string relativeOutput = $"samples/{destinationFileName}";
            string destinationPath = Path.Combine(samplesRoot, destinationFileName);
            File.Copy(reference.FullPath, destinationPath, overwrite: false);
            copiedSamples[reference.FullPath] = relativeOutput;
            usedRelativePaths.Add(relativeOutput);
        }

        return copiedSamples;
    }

    private static string BuildUniqueFileName(string sourcePath, HashSet<string> usedRelativePaths)
    {
        string safeBaseName = Slug(Path.GetFileNameWithoutExtension(sourcePath));
        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "sample";
        }

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        string fileName = $"{safeBaseName}{extension}";
        int suffix = 2;
        while (usedRelativePaths.Contains($"samples/{fileName}"))
        {
            fileName = $"{safeBaseName}-{suffix}{extension}";
            suffix++;
        }

        return fileName;
    }

    private static Dictionary<string, string> BuildKeyOverrides(Dictionary<string, List<string>> groups)
    {
        Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase);
        AddOverrideIfGroupExists(groups, overrides, "Backspace", "backspace");
        AddOverrideIfGroupExists(groups, overrides, "Enter", "enter");
        AddOverrideIfGroupExists(groups, overrides, "Space", "space");
        AddOverrideIfGroupExists(groups, overrides, "Tab", "tab");
        return overrides;
    }

    private static void AddOverrideIfGroupExists(
        Dictionary<string, List<string>> groups,
        Dictionary<string, string> overrides,
        string key,
        string group)
    {
        if (groups.TryGetValue(group, out List<string>? files) && files.Count > 0)
        {
            overrides[key] = group;
        }
    }

    private static string MapMechvibesKeyCodeToGroup(string keyCode) => keyCode switch
    {
        "14" => "backspace",
        "28" => "enter",
        "57" => "space",
        "15" => "tab",
        _ => "normal"
    };

    private static string BuildPackId(string? id, string? name, string inputRoot)
    {
        string source = !string.IsNullOrWhiteSpace(id)
            ? id
            : !string.IsNullOrWhiteSpace(name)
                ? name
                : Path.GetFileName(inputRoot);

        string slug = Slug(source);
        return slug.StartsWith("mechvibes-", StringComparison.OrdinalIgnoreCase)
            ? slug
            : $"mechvibes-{slug}";
    }

    private static string Slug(string value)
    {
        string lower = value.Trim().ToLowerInvariant();
        char[] chars = lower
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        string slug = Regex.Replace(new string(chars), "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "pack" : slug;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private sealed record SampleReference(string Group, string FullPath);
}
