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

            return Normalize(settings);
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
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await using FileStream stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, Normalize(settings), _jsonOptions, cancellationToken);
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();
        settings.MasterVolume = Math.Clamp(settings.MasterVolume, 0.0, 1.0);
        settings.PitchVariation = Math.Clamp(settings.PitchVariation, 0.0, 0.12);
        settings.ActiveSoundPackId = string.IsNullOrWhiteSpace(settings.ActiveSoundPackId)
            ? "classic-typewriter"
            : settings.ActiveSoundPackId;
        settings.ExcludedKeys ??= AppSettings.DefaultExcludedKeys();
        settings.AppRules ??= [];
        settings.GroupVolumes ??= new SoundGroupVolumeSettings();
        settings.GroupVolumes.Clamp();
        settings.Eq ??= new EqSettings();
        settings.Eq.Normalize();
        settings.Pan ??= new PanSettings();
        settings.Pan.Normalize();
        return settings;
    }
}
