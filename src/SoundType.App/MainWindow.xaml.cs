using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SoundType.Audio;
using SoundType.Core.Models;
using SoundType.Core.Rules;
using SoundType.Core.Settings;
using SoundType.Input;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.Brush;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace SoundType.App;

public partial class MainWindow : Window
{
    private const int ToggleHotkeyId = 0x534B;
    private readonly SettingsService _settingsService = new();
    private readonly SoundPackLoader _packLoader = new();
    private readonly RuleEngine _ruleEngine = new();
    private readonly KeyboardHookService _keyboardHook = new();
    private readonly GlobalHotkeyService _globalHotkey = new();
    private readonly ActiveWindowService _activeWindow = new();
    private readonly AudioEngine _audio = new();
    private readonly StartupService _startup = new();
    private readonly DispatcherTimer _activeAppTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly string _packsRoot;
    private readonly Forms.NotifyIcon _trayIcon = new();
    private AppSettings _settings = new();
    private IReadOnlyList<SoundPackMetadata> _packs = [];
    private SoundPackMetadata? _activePack;
    private HwndSource? _hotkeySource;
    private bool _loading = true;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();
        _packsRoot = Path.Combine(AppContext.BaseDirectory, "assets", "packs");
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _keyboardHook.KeyPressed += KeyboardHook_KeyPressed;
        _activeAppTimer.Tick += (_, _) => RefreshCurrentApp();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        _settings.StartWithWindows = _startup.IsEnabled();
        LoadPacks();
        BuildKeyRules();
        ConfigureTray();
        BindSettingsToUi();
        _keyboardHook.Start();
        _activeAppTimer.Start();
        _loading = false;
        RefreshStatus();
        RefreshCurrentApp();
        RegisterGlobalHotkey();
    }

    private void KeyboardHook_KeyPressed(object? sender, KeyPressedEvent e)
    {
        if (_settings.IgnoreKeyRepeats && e.IsRepeat)
        {
            return;
        }

        string? processName = _activeWindow.GetActiveProcessName();
        PlaybackDecision decision = _ruleEngine.Decide(e.Key, processName, _settings, _activePack);
        if (!decision.ShouldPlay || decision.SoundGroup is null)
        {
            return;
        }

        _audio.TryEnqueue(new PlaybackRequest
        {
            Key = e.Key,
            SoundGroup = decision.SoundGroup,
            VolumeMultiplier = decision.VolumeMultiplier,
            ActiveProcessName = processName
        });
    }

    private void LoadPacks()
    {
        _packs = _packLoader.DiscoverPacks(_packsRoot);
        PacksList.Items.Clear();
        foreach (SoundPackMetadata pack in _packs)
        {
            PacksList.Items.Add(new PackListItem(pack));
        }

        PackListItem? selected = PacksList.Items
            .OfType<PackListItem>()
            .FirstOrDefault(item => item.Metadata.Id.Equals(_settings.ActiveSoundPackId, StringComparison.OrdinalIgnoreCase))
            ?? PacksList.Items.OfType<PackListItem>().FirstOrDefault();

        if (selected is not null)
        {
            PacksList.SelectedItem = selected;
            ActivatePack(selected.Metadata);
        }
        else
        {
            PackValidationText.Text = "No sound packs were found. Run tools/generate-placeholder-sounds.ps1 from the repo root.";
        }
    }

    private void ActivatePack(SoundPackMetadata pack)
    {
        SoundPackValidationResult validation = _packLoader.Validate(pack);
        if (!validation.IsValid)
        {
            PackValidationText.Text = string.Join(Environment.NewLine, validation.Errors);
            PackValidationText.Foreground = (MediaBrush)FindResource("DangerBrush");
            return;
        }

        _activePack = pack;
        _settings.ActiveSoundPackId = pack.Id;
        _audio.LoadPack(_packLoader.Load(pack));
        PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
        PackValidationText.Text = $"{pack.Name} by {pack.Author} is active. {pack.Description}";
        _ = SaveSettingsAsync();
    }

    private void BindSettingsToUi()
    {
        EnabledToggle.IsChecked = _settings.Enabled;
        EnabledToggle.Content = _settings.Enabled ? "ON" : "OFF";
        MasterVolumeSlider.Value = _settings.MasterVolume;
        IgnoreRepeatsCheck.IsChecked = _settings.IgnoreKeyRepeats;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        EqEnabledCheck.IsChecked = _settings.Eq.Enabled;
        BassSlider.Value = _settings.Eq.BassGainDb;
        MidSlider.Value = _settings.Eq.MidGainDb;
        TrebleSlider.Value = _settings.Eq.TrebleGainDb;
        _audio.MasterVolume = _settings.MasterVolume;
        _audio.Eq = _settings.Eq;
        RefreshAppRules();
        RefreshStatus();
    }

    private void BuildKeyRules()
    {
        KeyRulesPanel.Children.Clear();
        foreach (KeyIdentity key in KeyIdentityMapper.CommonKeys)
        {
            WpfCheckBox checkBox = new()
            {
                Content = key.DisplayName,
                Tag = key.Code,
                Width = 112,
                IsChecked = !_settings.ExcludedKeys.Contains(key.Code)
            };
            checkBox.Checked += KeyRuleChanged;
            checkBox.Unchecked += KeyRuleChanged;
            KeyRulesPanel.Children.Add(checkBox);
        }
    }

    private void RefreshAppRules()
    {
        AppRulesList.Items.Clear();
        foreach (AppRule rule in _settings.AppRules.OrderBy(rule => rule.ProcessName))
        {
            AppRulesList.Items.Add(rule.ProcessName);
        }
    }

    private void RefreshCurrentApp()
    {
        string? processName = _activeWindow.GetActiveProcessName();
        CurrentAppText.Text = string.IsNullOrWhiteSpace(processName)
            ? "Current app: unknown"
            : $"Current app: {processName}";
        if (string.IsNullOrWhiteSpace(ProcessRuleTextBox.Text) && processName is not null)
        {
            ProcessRuleTextBox.Text = processName;
        }
    }

    private void RefreshStatus()
    {
        StatusText.Text = _settings.Enabled ? "Listening" : "Muted";
        StatusDot.Fill = (MediaBrush)FindResource(_settings.Enabled ? "AccentBrush" : "DangerBrush");
        EnabledToggle.Content = _settings.Enabled ? "ON" : "OFF";
        VolumeText.Text = $"{Math.Round(_settings.MasterVolume * 100)}%";
        _trayIcon.Text = $"SoundType - {StatusText.Text}";
        if (_trayIcon.ContextMenuStrip?.Items["enabled"] is Forms.ToolStripMenuItem enabledItem)
        {
            enabledItem.Checked = _settings.Enabled;
        }
    }

    private void RegisterGlobalHotkey()
    {
        string hotkeyText = string.IsNullOrWhiteSpace(_settings.GlobalToggleHotkey)
            ? HotkeyGesture.DefaultText
            : _settings.GlobalToggleHotkey;

        if (!HotkeyGesture.TryParse(hotkeyText, out HotkeyGesture gesture))
        {
            _settings.GlobalToggleHotkey = HotkeyGesture.DefaultText;
            gesture = HotkeyGesture.Default;
            ShowHotkeyStatus("Invalid global hotkey. Using Ctrl+Alt+K.");
            _ = SaveSettingsAsync();
        }

        IntPtr windowHandle = new WindowInteropHelper(this).Handle;
        _hotkeySource = HwndSource.FromHwnd(windowHandle);
        _hotkeySource?.AddHook(WndProc);

        if (!_globalHotkey.TryRegister(windowHandle, ToggleHotkeyId, gesture, out string? errorMessage))
        {
            ShowHotkeyStatus(errorMessage ?? "Global hotkey unavailable.");
            ShowTrayBalloon("Global hotkey unavailable");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.WmHotkey && wParam.ToInt32() == ToggleHotkeyId)
        {
            handled = true;
            ToggleEnabledFromHotkey();
        }

        return IntPtr.Zero;
    }

    private async void ToggleEnabledFromHotkey()
    {
        _settings.Enabled = !_settings.Enabled;
        EnabledToggle.IsChecked = _settings.Enabled;
        RefreshStatus();
        ShowTrayBalloon(_settings.Enabled ? "SoundType enabled" : "SoundType muted");
        await SaveSettingsAsync();
    }

    private void ShowHotkeyStatus(string message)
    {
        PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
        PackValidationText.Text = message;
    }

    private void ShowTrayBalloon(string message)
    {
        if (_trayIcon.Visible)
        {
            _trayIcon.ShowBalloonTip(1200, "SoundType", message, Forms.ToolTipIcon.Info);
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (_loading)
        {
            return;
        }

        await _settingsService.SaveAsync(_settings);
    }

    private void ConfigureTray()
    {
        Forms.ContextMenuStrip menu = new();
        Forms.ToolStripMenuItem title = new("SoundType") { Enabled = false };
        Forms.ToolStripMenuItem enabled = new("Enabled") { Name = "enabled", Checked = _settings.Enabled, CheckOnClick = true };
        enabled.Click += async (_, _) =>
        {
            _settings.Enabled = enabled.Checked;
            EnabledToggle.IsChecked = _settings.Enabled;
            RefreshStatus();
            await SaveSettingsAsync();
        };

        Forms.ToolStripMenuItem open = new("Open SoundType");
        open.Click += (_, _) => ShowFromTray();

        Forms.ToolStripMenuItem exit = new("Exit");
        exit.Click += (_, _) =>
        {
            _exitRequested = true;
            Close();
        };

        menu.Items.Add(title);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(enabled);
        menu.Items.Add(open);
        menu.Items.Add(exit);

        _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void EnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Enabled = EnabledToggle.IsChecked == true;
        RefreshStatus();
        _ = SaveSettingsAsync();
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.MasterVolume = Math.Clamp(MasterVolumeSlider.Value, 0.0, 1.0);
        _audio.MasterVolume = _settings.MasterVolume;
        RefreshStatus();
        _ = SaveSettingsAsync();
    }

    private void PacksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (PacksList.SelectedItem is PackListItem item)
        {
            ActivatePack(item.Metadata);
        }
    }

    private void PreviewPack_Click(object sender, RoutedEventArgs e) => _audio.Preview();

    private void OpenPackFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_packsRoot);
        Process.Start(new ProcessStartInfo(_packsRoot) { UseShellExecute = true });
    }

    private void KeyRuleChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not WpfCheckBox checkBox || checkBox.Tag is not string code)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            _settings.ExcludedKeys.Remove(code);
        }
        else
        {
            _settings.ExcludedKeys.Add(code);
        }

        _ = SaveSettingsAsync();
    }

    private void AddAppRule_Click(object sender, RoutedEventArgs e)
    {
        string processName = ProcessRuleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName += ".exe";
        }

        AppRule? existing = _settings.AppRules.FirstOrDefault(rule =>
            rule.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _settings.AppRules.Add(new AppRule { ProcessName = processName, Mode = AppRuleMode.Disabled });
        }
        else
        {
            existing.Mode = AppRuleMode.Disabled;
        }

        RefreshAppRules();
        _ = SaveSettingsAsync();
    }

    private void RemoveAppRule_Click(object sender, RoutedEventArgs e)
    {
        if (AppRulesList.SelectedItem is not string selected)
        {
            return;
        }

        _settings.AppRules.RemoveAll(rule => rule.ProcessName.Equals(selected, StringComparison.OrdinalIgnoreCase));
        RefreshAppRules();
        _ = SaveSettingsAsync();
    }

    private void SettingsCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.IgnoreKeyRepeats = IgnoreRepeatsCheck.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        _ = SaveSettingsAsync();
    }

    private void StartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _startup.SetEnabled(_settings.StartWithWindows);
        _ = SaveSettingsAsync();
    }

    private void EqChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Eq.Enabled = EqEnabledCheck.IsChecked == true;
        _audio.Eq = _settings.Eq;
        _ = SaveSettingsAsync();
    }

    private void EqSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.Eq.BassGainDb = BassSlider.Value;
        _settings.Eq.MidGainDb = MidSlider.Value;
        _settings.Eq.TrebleGainDb = TrebleSlider.Value;
        _settings.Eq.Enabled = EqEnabledCheck.IsChecked == true;
        _audio.Eq = _settings.Eq;
        _ = SaveSettingsAsync();
    }

    private void PresetFlat_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Flat", 0, 0, 0);
    private void PresetWarm_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Warm", 4, 1, -2);
    private void PresetCrisp_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Crisp", -1, 1, 4);
    private void PresetSoftNight_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Soft Night", -2, -1, -3);

    private void ApplyEqPreset(string name, double bass, double mid, double treble)
    {
        _settings.Eq.PresetName = name;
        _settings.Eq.BassGainDb = bass;
        _settings.Eq.MidGainDb = mid;
        _settings.Eq.TrebleGainDb = treble;
        _settings.Eq.Enabled = name != "Flat";
        _loading = true;
        EqEnabledCheck.IsChecked = _settings.Eq.Enabled;
        BassSlider.Value = bass;
        MidSlider.Value = mid;
        TrebleSlider.Value = treble;
        _loading = false;
        _audio.Eq = _settings.Eq;
        _ = SaveSettingsAsync();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.MinimizeToTray && !_exitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _keyboardHook.Dispose();
        _hotkeySource?.RemoveHook(WndProc);
        _globalHotkey.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        await _audio.DisposeAsync();
        await _settingsService.SaveAsync(_settings);
    }

    private sealed class PackListItem(SoundPackMetadata metadata)
    {
        public SoundPackMetadata Metadata { get; } = metadata;
        public override string ToString() => $"{Metadata.Name} - {Metadata.Description}";
    }
}
