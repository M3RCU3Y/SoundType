using System.IO.Compression;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class SoundPackArchiveService
{
    private readonly SoundPackLoader loader;

    public SoundPackArchiveService()
        : this(new SoundPackLoader())
    {
    }

    public SoundPackArchiveService(SoundPackLoader loader)
    {
        this.loader = loader;
    }

    public void ExportPack(string packFolder, string archivePath)
    {
        SoundPackMetadata? metadata = loader.TryLoadMetadata(packFolder);
        SoundPackValidationResult validation = loader.Validate(metadata);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        string? archiveDirectory = Path.GetDirectoryName(Path.GetFullPath(archivePath));
        if (!string.IsNullOrEmpty(archiveDirectory))
        {
            Directory.CreateDirectory(archiveDirectory);
        }

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        ZipFile.CreateFromDirectory(packFolder, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    public SoundPackMetadata ImportPack(string archivePath, string packsRoot, bool overwrite)
    {
        Directory.CreateDirectory(packsRoot);
        string stagingFolder = Path.Combine(packsRoot, $".import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingFolder);

        try
        {
            ExtractArchiveSafely(archivePath, stagingFolder);

            SoundPackMetadata? metadata = loader.TryLoadMetadata(stagingFolder);
            SoundPackValidationResult validation = loader.Validate(metadata);
            if (!validation.IsValid || metadata is null)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
            }

            ValidatePackIdForFolderName(metadata.Id);
            string targetFolder = Path.Combine(packsRoot, metadata.Id);
            EnsurePathIsInsideDirectory(targetFolder, packsRoot);

            if (Directory.Exists(targetFolder))
            {
                if (!overwrite)
                {
                    throw new IOException($"Sound pack '{metadata.Id}' already exists.");
                }

                Directory.Delete(targetFolder, recursive: true);
            }

            Directory.Move(stagingFolder, targetFolder);
            metadata.FolderPath = targetFolder;
            return metadata;
        }
        catch
        {
            if (Directory.Exists(stagingFolder))
            {
                Directory.Delete(stagingFolder, recursive: true);
            }

            throw;
        }
    }

    private static void ExtractArchiveSafely(string archivePath, string destination)
    {
        string destinationRoot = EnsureTrailingSeparator(Path.GetFullPath(destination));
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string entryDestination = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!entryDestination.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Archive contains unsafe entry '{entry.FullName}'.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(entryDestination);
                continue;
            }

            string? entryDirectory = Path.GetDirectoryName(entryDestination);
            if (!string.IsNullOrEmpty(entryDirectory))
            {
                Directory.CreateDirectory(entryDirectory);
            }

            entry.ExtractToFile(entryDestination, overwrite: false);
        }
    }

    private static void ValidatePackIdForFolderName(string packId)
    {
        if (packId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            packId.Contains(Path.DirectorySeparatorChar) ||
            packId.Contains(Path.AltDirectorySeparatorChar) ||
            packId is "." or "..")
        {
            throw new InvalidOperationException("Pack id must be a single folder name.");
        }
    }

    private static void EnsurePathIsInsideDirectory(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
        if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pack target folder is outside the packs root.");
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
