namespace SoundType.Core.Models;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowStartupNotification { get; set; }
    public double MasterVolume { get; set; } = 0.75;
    public double PitchVariation { get; set; } = 0.02;
    public string ActiveSoundPackId { get; set; } = "classic-typewriter";
    public string GlobalToggleHotkey { get; set; } = "Ctrl+Alt+K";
    public bool IgnoreKeyRepeats { get; set; } = true;
    public bool PauseInFullscreenApps { get; set; } = true;
    public HashSet<string> ExcludedKeys { get; set; } = DefaultExcludedKeys();
    public List<AppRule> AppRules { get; set; } = [];
    public SoundGroupVolumeSettings GroupVolumes { get; set; } = new();
    public EqSettings Eq { get; set; } = new();

    public static HashSet<string> DefaultExcludedKeys() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            "LeftShift",
            "RightShift",
            "LeftCtrl",
            "RightCtrl",
            "LeftAlt",
            "RightAlt",
            "LeftWindows",
            "RightWindows",
            "CapsLock",
            "Escape"
        };
}
