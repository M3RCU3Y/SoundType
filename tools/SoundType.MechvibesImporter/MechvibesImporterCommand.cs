using System.Text.Json;

namespace SoundType.MechvibesImporter;

public static class MechvibesImporterCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is 1 && IsHelp(args[0]) || args.Length is < 2 or > 3)
        {
            output.WriteLine("Usage: SoundType.MechvibesImporter <mechvibes-pack-folder> <output-pack-folder> [--overwrite]");
            return args.Length == 1 && IsHelp(args[0]) ? 0 : 2;
        }

        bool overwrite = false;
        if (args.Length == 3)
        {
            if (!string.Equals(args[2], "--overwrite", StringComparison.OrdinalIgnoreCase))
            {
                error.WriteLine($"Unknown option: {args[2]}");
                return 2;
            }

            overwrite = true;
        }

        try
        {
            MechvibesImportResult result = new MechvibesPackImporter().Import(
                args[0],
                args[1],
                new MechvibesImportOptions(overwrite));

            output.WriteLine($"Converted Mechvibes pack: {result.Metadata.Name} ({result.Metadata.Id})");
            output.WriteLine($"Wrote {result.ManifestPath}");

            foreach (string warning in result.Warnings)
            {
                output.WriteLine($"Warning: {warning}");
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            error.WriteLine("Mechvibes import failed:");
            error.WriteLine($"- {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "/?";
}
