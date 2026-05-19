using System.Text.Json;
using SoundType.Core.Models;

namespace SoundType.Core.Settings;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SoundType",
            "settings.json");
    }

    public string SettingsPath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            await using FileStream stream = File.OpenRead(SettingsPath);
            AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                _jsonOptions,
                cancellationToken);

            return Normalize(settings, resetLegacyDefaultPanning: true);
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string settingsDirectory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(settingsDirectory);
        string tempPath = Path.Combine(settingsDirectory, $"{Path.GetFileName(SettingsPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (FileStream stream = new(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, Normalize(settings, resetLegacyDefaultPanning: false), _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static AppSettings Normalize(AppSettings? settings, bool resetLegacyDefaultPanning)
    {
        settings ??= new AppSettings();
        settings.MasterVolume = Math.Clamp(settings.MasterVolume, 0.0, 1.0);
        settings.PitchVariation = Math.Clamp(settings.PitchVariation, 0.0, 0.12);
        settings.EnterDingVolume = Math.Clamp(settings.EnterDingVolume, 0.0, 1.0);
        settings.EnterDingSoundGroup = string.IsNullOrWhiteSpace(settings.EnterDingSoundGroup)
            ? "random"
            : settings.EnterDingSoundGroup;
        settings.ActiveSoundPackId = string.IsNullOrWhiteSpace(settings.ActiveSoundPackId)
            ? AppSettings.DefaultSoundPackId
            : settings.ActiveSoundPackId;
        settings.ExcludedKeys ??= AppSettings.DefaultExcludedKeys();
        settings.AppRules ??= [];
        settings.GroupVolumes ??= new SoundGroupVolumeSettings();
        settings.GroupVolumes.Clamp();
        settings.Eq ??= new EqSettings();
        settings.Eq.Normalize();
        settings.Pan ??= new PanSettings();
        if (resetLegacyDefaultPanning)
        {
            settings.Pan.NormalizeLegacyDefault();
        }
        else
        {
            settings.Pan.Normalize();
        }
        return settings;
    }
}
