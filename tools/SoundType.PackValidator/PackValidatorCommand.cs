using SoundType.Audio;
using SoundType.Core.Models;

namespace SoundType.PackValidator;

public static class PackValidatorCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error, string? tempRoot = null)
    {
        if (args.Length is not 1 || IsHelp(args[0]))
        {
            output.WriteLine("Usage: SoundType.PackValidator <pack-folder|pack.soundpack|pack.zip>");
            return args.Length == 1 && IsHelp(args[0]) ? 0 : 2;
        }

        string packPath = Path.GetFullPath(args[0]);
        SoundPackLoader loader = new();

        try
        {
            ValidationOutcome outcome = Directory.Exists(packPath)
                ? ValidateFolder(packPath, loader)
                : ValidateArchive(packPath, loader, tempRoot);

            SoundPackValidationResult validation = outcome.Validation;
            SoundPackMetadata? metadata = outcome.Metadata;
            if (!validation.IsValid || metadata is null)
            {
                WriteInvalid(error, validation);
                return 1;
            }

            output.WriteLine($"Valid sound pack: {metadata.Name} ({metadata.Id})");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            error.WriteLine("Invalid sound pack:");
            error.WriteLine($"- {ex.Message}");
            return 1;
        }
    }

    private static ValidationOutcome ValidateFolder(string folderPath, SoundPackLoader loader)
    {
        SoundPackMetadata? metadata = loader.TryLoadMetadata(folderPath);
        return new ValidationOutcome(metadata, loader.Validate(metadata));
    }

    private static ValidationOutcome ValidateArchive(string archivePath, SoundPackLoader loader, string? tempRoot)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Pack path was not found.", archivePath);
        }

        string extension = Path.GetExtension(archivePath);
        if (!extension.Equals(".soundpack", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pack path must be a folder, .soundpack, or .zip archive.");
        }

        string parentTempRoot = tempRoot ?? Path.GetTempPath();
        Directory.CreateDirectory(parentTempRoot);

        string importRoot = Path.Combine(parentTempRoot, $"soundtype-pack-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(importRoot);

        try
        {
            SoundPackArchiveService archiveService = new(loader);
            SoundPackMetadata metadata = archiveService.ImportPack(archivePath, importRoot, overwrite: false);
            return new ValidationOutcome(metadata, loader.Validate(metadata));
        }
        finally
        {
            if (Directory.Exists(importRoot))
            {
                Directory.Delete(importRoot, recursive: true);
            }
        }
    }

    private static void WriteInvalid(TextWriter error, SoundPackValidationResult validation)
    {
        error.WriteLine("Invalid sound pack:");
        foreach (string validationError in validation.Errors)
        {
            error.WriteLine($"- {validationError}");
        }
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help" or "/?";
    }

    private sealed record ValidationOutcome(SoundPackMetadata? Metadata, SoundPackValidationResult Validation);
}
