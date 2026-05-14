using SoundType.Core.Models;

namespace SoundType.MechvibesImporter;

public sealed record MechvibesImportResult(SoundPackMetadata Metadata, string ManifestPath, IReadOnlyList<string> Warnings);
