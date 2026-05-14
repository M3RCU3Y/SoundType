using System.Text.Json.Serialization;

namespace SoundType.Core.Models;

public sealed class SoundPackMetadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "";
    public string License { get; set; } = "";
    public string? PreviewImage { get; set; }
    public Dictionary<string, List<string>> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> KeyOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public SoundPackDefaults Defaults { get; set; } = new();

    [JsonIgnore]
    public string FolderPath { get; set; } = "";
}
