using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SoundType.App.Controls;
using SoundType.Audio;
using SoundType.Core.Models;
using SoundType.Core.Rules;
using SoundType.Core.Services;
using SoundType.Core.Settings;
using SoundType.Input;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.Brush;

namespace SoundType.App;

public partial class MainWindow : Window
{
    private const int ToggleHotkeyId = 0x534B;
    private readonly SettingsService _settingsService = new();
    private readonly SoundPackLoader _packLoader = new();
    private readonly SoundPackArchiveService _archiveService = new();
    private readonly RuleEngine _ruleEngine = new();
    private readonly RecentAppTracker _recentApps = new();
    private readonly KeyboardHookService _keyboardHook = new();
    private readonly GlobalHotkeyService _globalHotkey = new();
    private readonly ActiveWindowService _activeWindow = new();
    private readonly StartupService _startup = new();
    private readonly DispatcherTimer _activeAppTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly string _packsRoot;
    private readonly Forms.NotifyIcon _trayIcon = new();
    private readonly List<Slider> _eqBandSliders = [];
    private readonly List<TextBlock> _eqBandValueTexts = [];
    private readonly List<string> _startupWarnings = [];
    private AudioEngine? _audio;
    private AppSettings _settings = new();
    private IReadOnlyList<SoundPackMetadata> _packs = [];
    private IReadOnlyDictionary<string, SoundPackMetadata> _packsById = new Dictionary<string, SoundPackMetadata>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _releasePackIds = new(StringComparer.OrdinalIgnoreCase);
    private SoundPackMetadata? _activePack;
    private HwndSource? _hotkeySource;
    private string? _currentProcessName;
    private string? _lastRecordedProcessName;
    private bool _loading = true;
    private bool _exitRequested;
    private bool _packFiltersConfigured;
    private bool _refreshingPackLibrary;

    public MainWindow()
    {
        InitializeComponent();
        BuildEqBandControls();
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
        ConfigureAppRuleEditors();
        ConfigurePanControls();
        ConfigurePackFilters();
        TryStartAudio();
        LoadPacks();
        BuildKeyRules();
        ConfigureTray();
        BindSettingsToUi();
        KeyboardHookStartResult keyboardHookStart = _keyboardHook.Start();
        if (!keyboardHookStart.Started)
        {
            AddStartupWarning(keyboardHookStart.ErrorMessage ?? "Keyboard hook unavailable.");
        }
        _activeAppTimer.Start();
        _loading = false;
        RefreshStatus();
        RefreshCurrentApp();
        RegisterGlobalHotkey();
        RefreshStartupWarnings();
        if (ShouldStartHiddenInTray())
        {
            HideToTray(showBalloon: false);
        }
    }

    private void KeyboardHook_KeyPressed(object? sender, KeyPressedEvent e)
    {
        if (!e.IsRelease && _settings.IgnoreKeyRepeats && e.IsRepeat)
        {
            return;
        }

        if (e.IsRelease && _releasePackIds.Count == 0)
        {
            return;
        }

        string? processName = _currentProcessName;
        PlaybackDecision decision = _ruleEngine.Decide(e.Key, processName, _settings, _activePack);
        if (!decision.ShouldPlay || decision.SoundGroup is null)
        {
            return;
        }

        SoundPackMetadata? decisionPack = ResolveDecisionPack(decision.SoundPackId);
        if (e.IsRelease && !HasAnyReleaseGroup(decisionPack))
        {
            return;
        }

        string? soundGroup = ResolveSoundGroupForEvent(decisionPack, decision.SoundGroup, e.IsRelease);
        if (soundGroup is null)
        {
            return;
        }

        _audio?.TryPlay(new PlaybackRequest
        {
            Key = e.Key,
            SoundGroup = soundGroup,
            SoundPackId = decision.SoundPackId,
            VolumeMultiplier = decision.VolumeMultiplier * _settings.GroupVolumes.GetVolumeForGroup(decision.SoundGroup),
            ActiveProcessName = processName
        });
    }

    private SoundPackMetadata? ResolveDecisionPack(string? soundPackId)
    {
        if (!string.IsNullOrWhiteSpace(soundPackId) && _packsById.TryGetValue(soundPackId, out SoundPackMetadata? pack))
        {
            return pack;
        }

        return _activePack;
    }

    private static string? ResolveSoundGroupForEvent(SoundPackMetadata? pack, string baseGroup, bool isRelease)
    {
        if (!isRelease)
        {
            return baseGroup;
        }

        if (HasGroup(pack, $"{baseGroup}-release"))
        {
            return $"{baseGroup}-release";
        }

        return HasGroup(pack, "normal-release") ? "normal-release" : null;
    }

    private void LoadPacks()
    {
        _packs = _packLoader.DiscoverPacks(_packsRoot);
        _packsById = _packs.ToDictionary(pack => pack.Id, StringComparer.OrdinalIgnoreCase);
        _releasePackIds = _packs
            .Where(HasAnyReleaseGroup)
            .Select(pack => pack.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RulePackComboBox.Items.Clear();
        foreach (SoundPackMetadata pack in _packs)
        {
            RulePackComboBox.Items.Add(new PackListItem(pack));
        }

        RefreshPackLibrary(_settings.ActiveSoundPackId);

        PackListItem? selected = PacksList.SelectedItem as PackListItem;

        if (selected is not null)
        {
            ActivatePack(selected.Metadata);
            RulePackComboBox.SelectedItem ??= RulePackComboBox.Items
                .OfType<PackListItem>()
                .FirstOrDefault(item => item.Metadata.Id.Equals(selected.Metadata.Id, StringComparison.OrdinalIgnoreCase));
            PreloadRulePacksInBackground(selected.Metadata.Id);
        }
        else
        {
            PackValidationText.Text = "No sound packs were found. Run tools/generate-placeholder-sounds.ps1 from the repo root.";
            PackCountText.Text = "No packs available.";
            RefreshSelectedPackDetails(null);
        }
    }

    private void TryStartAudio()
    {
        try
        {
            _audio = new AudioEngine
            {
                MasterVolume = _settings.MasterVolume,
                PitchVariation = _settings.PitchVariation,
                Eq = _settings.Eq,
                Pan = _settings.Pan
            };
        }
        catch (Exception ex)
        {
            _audio = null;
            AddStartupWarning($"Audio unavailable: {ex.Message}");
        }
    }

    private void AddStartupWarning(string message)
    {
        if (_startupWarnings.Contains(message, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _startupWarnings.Add(message);
        RefreshStartupWarnings();
        RefreshStatus();
    }

    private void RefreshStartupWarnings()
    {
        if (_startupWarnings.Count == 0 || PackValidationText is null)
        {
            return;
        }

        PackValidationText.Foreground = (MediaBrush)FindResource("WarningBrush");
        PackValidationText.Text = string.Join(Environment.NewLine, _startupWarnings);

        if (_trayIcon.Visible)
        {
            ShowTrayBalloon(_startupWarnings[^1]);
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
        if (_audio is null)
        {
            PackValidationText.Foreground = (MediaBrush)FindResource("WarningBrush");
            PackValidationText.Text = $"{pack.Name} is selected, but audio is unavailable.";
            RefreshStatus();
            _ = SaveSettingsAsync();
            return;
        }

        if (!_audio.SetActivePack(pack.Id))
        {
            _audio.LoadPack(_packLoader.Load(pack));
        }
        PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
        PackValidationText.Text = $"{pack.Name} by {pack.Author} is active. {pack.Description}";
        RefreshStatus();
        _ = SaveSettingsAsync();
    }

    private void TryPreloadPack(SoundPackMetadata pack)
    {
        if (_audio is null)
        {
            return;
        }

        try
        {
            if (_audio.TryGetLoadedPack(pack.Id, out _) || !_packLoader.Validate(pack).IsValid)
            {
                return;
            }

            _audio.LoadPack(_packLoader.Load(pack), makeActive: false);
        }
        catch
        {
            // Invalid packs stay visible with validation feedback when selected.
        }
    }

    private void PreloadRulePacksInBackground(string? activePackId)
    {
        HashSet<string> rulePackIds = _settings.AppRules
            .Where(rule => rule.Mode == AppRuleMode.UseSpecificPack && !string.IsNullOrWhiteSpace(rule.SoundPackId))
            .Select(rule => rule.SoundPackId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<SoundPackMetadata> rulePacks = _packs
            .Where(pack =>
                !pack.Id.Equals(activePackId, StringComparison.OrdinalIgnoreCase) &&
                rulePackIds.Contains(pack.Id))
            .ToList();

        _ = Task.Run(() =>
        {
            foreach (SoundPackMetadata pack in rulePacks)
            {
                TryPreloadPack(pack);
            }
        });
    }

    private void ConfigureAppRuleEditors()
    {
        RuleModeComboBox.Items.Clear();
        RuleModeComboBox.Items.Add(AppRuleMode.Disabled);
        RuleModeComboBox.Items.Add(AppRuleMode.Default);
        RuleModeComboBox.Items.Add(AppRuleMode.EnabledOnly);
        RuleModeComboBox.Items.Add(AppRuleMode.UseSpecificPack);
        RuleModeComboBox.SelectedItem = AppRuleMode.Disabled;
        RuleVolumeSlider.Value = 1.0;
        RuleVolumeText.Text = "100%";
    }

    private void ConfigurePanControls()
    {
        PanModeComboBox.Items.Clear();
        PanModeComboBox.Items.Add(PanMode.KeyPosition);
        PanModeComboBox.Items.Add(PanMode.Random);
    }

    private void BuildEqBandControls()
    {
        EqBandsPanel.Children.Clear();
        _eqBandSliders.Clear();
        _eqBandValueTexts.Clear();

        for (int i = 0; i < EqSettings.BandCount; i++)
        {
            int bandIndex = i;
            TextBlock valueText = new()
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Foreground = (MediaBrush)FindResource("AccentHoverBrush"),
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };

            Slider slider = new()
            {
                Style = (Style)FindResource("EqVerticalSliderStyle"),
                Minimum = -12,
                Maximum = 12,
                TickFrequency = 1,
                Margin = new Thickness(0, 10, 0, 10),
                Tag = bandIndex
            };
            slider.ValueChanged += EqSliderChanged;

            TextBlock labelText = new()
            {
                Text = FormatFrequency(EqSettings.Frequencies[i]),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Foreground = (MediaBrush)FindResource("MutedTextBrush"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };

            StackPanel stack = new()
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            stack.Children.Add(valueText);
            stack.Children.Add(slider);
            stack.Children.Add(labelText);
            EqBandsPanel.Children.Add(stack);
            _eqBandSliders.Add(slider);
            _eqBandValueTexts.Add(valueText);
        }
    }

    private void ConfigurePackFilters()
    {
        if (_packFiltersConfigured)
        {
            return;
        }

        PackTypeComboBox.Items.Clear();
        PackTypeComboBox.Items.Add(PackFilter.All);
        PackTypeComboBox.Items.Add(PackFilter.Switches);
        PackTypeComboBox.Items.Add(PackFilter.Typewriters);
        PackTypeComboBox.Items.Add(PackFilter.Quiet);
        PackTypeComboBox.Items.Add(PackFilter.Digital);
        PackTypeComboBox.SelectedItem = PackFilter.All;
        PackSearchTextBox.Text = "";
        _packFiltersConfigured = true;
        RefreshPackCategoryButtons();
    }

    private void BindSettingsToUi()
    {
        EnabledToggle.IsChecked = _settings.Enabled;
        MasterVolumeSlider.Value = _settings.MasterVolume;
        PitchVariationSlider.Value = _settings.PitchVariation;
        IgnoreRepeatsCheck.IsChecked = _settings.IgnoreKeyRepeats;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        StartHiddenInTrayCheck.IsChecked = _settings.StartHiddenInTray;
        EqEnabledCheck.IsChecked = _settings.Eq.Enabled;
        NormalVolumeSlider.Value = _settings.GroupVolumes.Normal;
        EnterVolumeSlider.Value = _settings.GroupVolumes.Enter;
        SpaceVolumeSlider.Value = _settings.GroupVolumes.Space;
        BackspaceVolumeSlider.Value = _settings.GroupVolumes.Backspace;
        _settings.Eq.Normalize();
        for (int i = 0; i < _eqBandSliders.Count; i++)
        {
            _eqBandSliders[i].Value = _settings.Eq.GetBandGainDb(i);
        }

        PanEnabledCheck.IsChecked = _settings.Pan.Enabled;
        PanModeComboBox.SelectedItem = _settings.Pan.Mode;
        PanStrengthSlider.Value = _settings.Pan.Strength;
        if (_audio is not null)
        {
            _audio.MasterVolume = _settings.MasterVolume;
            _audio.PitchVariation = _settings.PitchVariation;
            _audio.Eq = _settings.Eq;
            _audio.Pan = _settings.Pan;
        }
        RefreshAppRules();
        RefreshGroupVolumeText();
        RefreshEqText();
        RefreshPanText();
        RefreshTrayStatus();
        RefreshStartupStatus();
        RefreshStatus();
    }

    private void BuildKeyRules()
    {
        VisualKeyboard.SetExcludedKeys(_settings.ExcludedKeys);
        RefreshExcludedKeysText();
    }

    private void RefreshAppRules()
    {
        AppRulesList.Items.Clear();
        foreach (AppRule rule in _settings.AppRules.OrderBy(rule => rule.ProcessName))
        {
            AppRulesList.Items.Add(new AppRuleListItem(rule));
        }
    }

    private void RefreshCurrentApp()
    {
        string? processName = _activeWindow.GetActiveProcessName();
        _currentProcessName = processName;
        if (!string.Equals(processName, _lastRecordedProcessName, StringComparison.OrdinalIgnoreCase))
        {
            _lastRecordedProcessName = processName;
            _recentApps.Record(processName);
            RefreshRecentApps();
        }

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
        bool degraded = _settings.Enabled && _startupWarnings.Count > 0;
        StatusText.Text = !_settings.Enabled
            ? "Muted"
            : degraded
                ? "Needs attention"
                : "Listening";
        StatusDot.Fill = (MediaBrush)FindResource(!_settings.Enabled || degraded ? "DangerBrush" : "AccentBrush");
        UpdateEnableButton();
        VolumeText.Text = $"{Math.Round(_settings.MasterVolume * 100)}%";
        PitchVariationText.Text = $"+/- {Math.Round(_settings.PitchVariation * 100)}%";
        _trayIcon.Text = $"SoundType - {StatusText.Text}";
        if (_trayIcon.ContextMenuStrip?.Items["enabled"] is Forms.ToolStripMenuItem enabledItem)
        {
            enabledItem.Checked = _settings.Enabled;
        }

        if (_trayIcon.ContextMenuStrip?.Items["pack"] is Forms.ToolStripMenuItem packItem)
        {
            packItem.Text = $"Pack: {_activePack?.Name ?? "None"}";
        }
    }

    private void RefreshTrayStatus()
    {
        TrayStatusText.Text = _settings.MinimizeToTray
            ? "Closing the window hides SoundType, keeps sounds active, and leaves controls in the tray."
            : "Closing the window exits SoundType. Use Hide to tray when you want it running quietly.";
    }

    private void RefreshStartupStatus()
    {
        StartupStatusText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
        if (!_settings.StartWithWindows)
        {
            StartupStatusText.Text = "Off. SoundType only starts when you open it.";
            return;
        }

        StartupStatusText.Text = _settings.StartHiddenInTray
            ? "Enabled. Windows will launch SoundType directly into the system tray."
            : "Enabled. Windows will launch SoundType after you sign in.";
    }

    private static bool ShouldStartHiddenInTray() =>
        Environment.GetCommandLineArgs().Any(arg =>
            arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--hidden", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

    private void UpdateEnableButton()
    {
        EnabledToggle.IsChecked = _settings.Enabled;
        EnabledToggle.Content = _settings.Enabled ? "||  DISABLE" : ">  ENABLE";
        EnabledToggle.Background = (MediaBrush)FindResource(_settings.Enabled ? "DangerBrush" : "AccentBrush");
        EnabledToggle.BorderBrush = (MediaBrush)FindResource(_settings.Enabled ? "DangerBrush" : "AccentHoverBrush");
        EnabledToggle.Foreground = (MediaBrush)FindResource("TextBrush");
    }

    private void ShowLibrary_Click(object sender, RoutedEventArgs e) => ShowPage(LibraryPage);
    private void ShowAudio_Click(object sender, RoutedEventArgs e) => ShowPage(AudioPage);
    private void ShowKeyboard_Click(object sender, RoutedEventArgs e) => ShowPage(KeyboardPage);
    private void ShowRules_Click(object sender, RoutedEventArgs e) => ShowPage(RulesPage);
    private void ShowSettings_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage);
    private void FocusNewRule_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(RulesPage);
        ProcessRuleTextBox.Focus();
    }

    private void ShowPage(FrameworkElement activePage)
    {
        FrameworkElement[] pages =
        [
            LibraryPage,
            AudioPage,
            KeyboardPage,
            RulesPage,
            SettingsPage,
        ];

        foreach (FrameworkElement page in pages)
        {
            page.Visibility = ReferenceEquals(page, activePage) ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateNavigationState(activePage);
    }

    private void UpdateNavigationState(FrameworkElement activePage)
    {
        (System.Windows.Controls.Button Button, FrameworkElement Page)[] nav =
        [
            (LibraryNavButton, LibraryPage),
            (AudioNavButton, AudioPage),
            (KeyboardNavButton, KeyboardPage),
            (RulesNavButton, RulesPage),
            (SettingsNavButton, SettingsPage)
        ];

        foreach ((System.Windows.Controls.Button button, FrameworkElement page) in nav)
        {
            bool selected = ReferenceEquals(page, activePage);
            button.Background = selected ? (MediaBrush)FindResource("AccentBrush") : System.Windows.Media.Brushes.Transparent;
            button.BorderBrush = selected ? (MediaBrush)FindResource("AccentHoverBrush") : System.Windows.Media.Brushes.Transparent;
            button.Foreground = (MediaBrush)FindResource(selected ? "TextBrush" : "MutedTextBrush");
        }
    }

    private void ShellDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowMaximized();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseDot_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeDot_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeDot_Click(object sender, RoutedEventArgs e) => ToggleWindowMaximized();

    private void ToggleWindowMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase or
                System.Windows.Controls.Primitives.TextBoxBase or
                Selector or
                System.Windows.Controls.Primitives.RangeBase or
                System.Windows.Controls.Primitives.ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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
            AddStartupWarning("Invalid global hotkey. Using Ctrl+Alt+K.");
            _ = SaveSettingsAsync();
        }

        IntPtr windowHandle = new WindowInteropHelper(this).Handle;
        _hotkeySource = HwndSource.FromHwnd(windowHandle);
        _hotkeySource?.AddHook(WndProc);

        if (!_globalHotkey.TryRegister(windowHandle, ToggleHotkeyId, gesture, out string? errorMessage))
        {
            AddStartupWarning(errorMessage ?? "Global hotkey unavailable.");
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
        Forms.ToolStripMenuItem pack = new("Pack: None") { Name = "pack", Enabled = false };
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

        Forms.ToolStripMenuItem hide = new("Hide to tray");
        hide.Click += (_, _) => HideToTray(showBalloon: false);

        Forms.ToolStripMenuItem exit = new("Exit");
        exit.Click += (_, _) =>
        {
            _exitRequested = true;
            Close();
        };

        menu.Items.Add(title);
        menu.Items.Add(pack);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(open);
        menu.Items.Add(hide);
        menu.Items.Add(enabled);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        RefreshTrayStatus();
    }

    private void HideToTray(bool showBalloon = true)
    {
        ShowInTaskbar = false;
        Hide();
        RefreshTrayStatus();
        if (showBalloon)
        {
            ShowTrayBalloon("SoundType is still running in the tray.");
        }
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
        if (_audio is not null)
        {
            _audio.MasterVolume = _settings.MasterVolume;
        }
        RefreshStatus();
        _ = SaveSettingsAsync();
    }

    private void PitchVariationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.PitchVariation = Math.Clamp(PitchVariationSlider.Value, 0.0, 0.12);
        if (_audio is not null)
        {
            _audio.PitchVariation = _settings.PitchVariation;
        }
        RefreshStatus();
        _ = SaveSettingsAsync();
    }

    private void PacksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedPackDetails((PacksList.SelectedItem as PackListItem)?.Metadata);
        if (_loading || _refreshingPackLibrary) return;
        if (PacksList.SelectedItem is PackListItem item)
        {
            ActivatePack(item.Metadata);
        }
    }

    private void PackSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        RefreshPackLibrary();
    }

    private void PackTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        RefreshPackCategoryButtons();
        RefreshPackLibrary();
    }

    private void AllPacksCategory_Click(object sender, RoutedEventArgs e) =>
        SetPackFilter(PackFilter.All);

    private void MechanicalCategory_Click(object sender, RoutedEventArgs e) =>
        SetPackFilter(PackFilter.Switches);

    private void TypewriterCategory_Click(object sender, RoutedEventArgs e) =>
        SetPackFilter(PackFilter.Typewriters);

    private void SetPackFilter(string filter)
    {
        if (PackTypeComboBox.SelectedItem as string == filter)
        {
            RefreshPackLibrary();
            return;
        }

        PackTypeComboBox.SelectedItem = filter;
    }

    private void PreviewNormal_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("normal");
    private void PreviewEnter_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("enter");
    private void PreviewSpace_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("space");
    private void PreviewBackspace_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("backspace");
    private void PreviewTab_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("tab");

    private void PreviewPackGroup(string group)
    {
        if (PacksList.SelectedItem is PackListItem item &&
            (_activePack is null || !item.Metadata.Id.Equals(_activePack.Id, StringComparison.OrdinalIgnoreCase)))
        {
            ActivatePack(item.Metadata);
        }

        _audio?.Preview(group);
    }

    private void ImportSoundPack_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Filter = "Sound packs (*.soundpack;*.zip)|*.soundpack;*.zip|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            SoundPackMetadata metadata = TryImportPack(dialog.FileName, overwrite: false);
            ReloadPacksAndSelect(metadata.Id);
            PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
            PackValidationText.Text = $"Imported {metadata.Name}.";
        }
        catch (IOException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            MessageBoxResult overwrite = System.Windows.MessageBox.Show(
                this,
                "A sound pack with this id already exists. Replace it?",
                "Replace sound pack",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (overwrite != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                SoundPackMetadata metadata = TryImportPack(dialog.FileName, overwrite: true);
                ReloadPacksAndSelect(metadata.Id);
                PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
                PackValidationText.Text = $"Replaced {metadata.Name}.";
            }
            catch (Exception retryException)
            {
                ShowPackError(retryException.Message);
            }
        }
        catch (Exception ex)
        {
            ShowPackError(ex.Message);
        }
    }

    private void ExportActivePack_Click(object sender, RoutedEventArgs e)
    {
        if (_activePack is null)
        {
            ShowPackError("No active pack selected.");
            return;
        }

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Filter = "Sound packs (*.soundpack)|*.soundpack|Zip archives (*.zip)|*.zip",
            FileName = $"{_activePack.Id}.soundpack",
            DefaultExt = ".soundpack"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _archiveService.ExportPack(_activePack.FolderPath, dialog.FileName);
            PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
            PackValidationText.Text = $"Exported {_activePack.Name} to {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            ShowPackError(ex.Message);
        }
    }

    private SoundPackMetadata TryImportPack(string archivePath, bool overwrite) =>
        _archiveService.ImportPack(archivePath, _packsRoot, overwrite);

    private void ReloadPacksAndSelect(string packId)
    {
        _settings.ActiveSoundPackId = packId;
        LoadPacks();
        PackListItem? selected = PacksList.Items
            .OfType<PackListItem>()
            .FirstOrDefault(item => item.Metadata.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            PacksList.SelectedItem = selected;
            ActivatePack(selected.Metadata);
        }
    }

    private void ShowPackError(string message)
    {
        PackValidationText.Foreground = (MediaBrush)FindResource("DangerBrush");
        PackValidationText.Text = message;
    }

    private void OpenPackFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_packsRoot);
        Process.Start(new ProcessStartInfo(_packsRoot) { UseShellExecute = true });
    }

    private void VisualKeyboard_KeyToggled(object sender, KeyboardKeyToggledEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (e.IsExcluded)
        {
            _settings.ExcludedKeys.Add(e.Code);
        }
        else
        {
            _settings.ExcludedKeys.Remove(e.Code);
        }

        RefreshExcludedKeysText();
        _ = SaveSettingsAsync();
    }

    private void EnableAllKeys_Click(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.ExcludedKeys.Clear();
        BuildKeyRules();
        _ = SaveSettingsAsync();
    }

    private void RestoreDefaultKeyRules_Click(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.ExcludedKeys = AppSettings.DefaultExcludedKeys();
        BuildKeyRules();
        _ = SaveSettingsAsync();
    }

    private void RefreshExcludedKeysText()
    {
        if (ExcludedKeysText is null)
        {
            return;
        }

        if (_settings.ExcludedKeys.Count == 0)
        {
            ExcludedKeysText.Text = "Every key is active.";
            return;
        }

        string mutedKeys = string.Join(", ",
            _settings.ExcludedKeys
                .Order(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(KeyIdentityMapper.GetDisplayName));
        int remaining = _settings.ExcludedKeys.Count - 8;
        ExcludedKeysText.Text = remaining > 0
            ? $"Muted: {mutedKeys}, +{remaining} more"
            : $"Muted: {mutedKeys}";
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

        AppRuleMode mode = RuleModeComboBox.SelectedItem is AppRuleMode selectedMode
            ? selectedMode
            : AppRuleMode.Disabled;
        string? packId = RulePackComboBox.SelectedItem is PackListItem packItem
            ? packItem.Metadata.Id
            : null;
        double volumeOverride = Math.Clamp(RuleVolumeSlider.Value, 0.0, 1.5);

        AppRule? existing = _settings.AppRules.FirstOrDefault(rule =>
            rule.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _settings.AppRules.Add(new AppRule
            {
                ProcessName = processName,
                Mode = mode,
                SoundPackId = mode == AppRuleMode.UseSpecificPack ? packId : null,
                VolumeOverride = Math.Abs(volumeOverride - 1.0) < 0.001 ? null : volumeOverride
            });
        }
        else
        {
            existing.Mode = mode;
            existing.SoundPackId = mode == AppRuleMode.UseSpecificPack ? packId : null;
            existing.VolumeOverride = Math.Abs(volumeOverride - 1.0) < 0.001 ? null : volumeOverride;
        }

        RefreshAppRules();
        _ = SaveSettingsAsync();
    }

    private void RemoveAppRule_Click(object sender, RoutedEventArgs e)
    {
        if (AppRulesList.SelectedItem is not AppRuleListItem selected)
        {
            return;
        }

        _settings.AppRules.RemoveAll(rule => rule.ProcessName.Equals(selected.Rule.ProcessName, StringComparison.OrdinalIgnoreCase));
        RefreshAppRules();
        _ = SaveSettingsAsync();
    }

    private void RefreshRecentApps()
    {
        if (RecentAppsList is null)
        {
            return;
        }

        string? selected = RecentAppsList.SelectedItem as string;
        RecentAppsList.Items.Clear();
        foreach (RecentAppEntry app in _recentApps.ListRecentApps())
        {
            RecentAppsList.Items.Add(app.ProcessName);
        }

        if (selected is not null && RecentAppsList.Items.Contains(selected))
        {
            RecentAppsList.SelectedItem = selected;
        }
    }

    private void RecentAppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentAppsList.SelectedItem is string processName)
        {
            ProcessRuleTextBox.Text = processName;
        }
    }

    private void AppRulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppRulesList.SelectedItem is not AppRuleListItem selected)
        {
            return;
        }

        AppRule rule = selected.Rule;
        ProcessRuleTextBox.Text = rule.ProcessName;
        RuleModeComboBox.SelectedItem = rule.Mode;
        RuleVolumeSlider.Value = rule.VolumeOverride ?? 1.0;

        if (!string.IsNullOrWhiteSpace(rule.SoundPackId))
        {
            RulePackComboBox.SelectedItem = RulePackComboBox.Items
                .OfType<PackListItem>()
                .FirstOrDefault(item => item.Metadata.Id.Equals(rule.SoundPackId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void SettingsCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.IgnoreKeyRepeats = IgnoreRepeatsCheck.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        RefreshTrayStatus();
        _ = SaveSettingsAsync();
    }

    private void StartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool requested = StartWithWindowsCheck.IsChecked == true;
        bool startHidden = StartHiddenInTrayCheck.IsChecked == true;
        if (!_startup.TrySetEnabled(requested, startHidden, out string? errorMessage))
        {
            _loading = true;
            _settings.StartWithWindows = _startup.IsEnabled();
            StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
            StartHiddenInTrayCheck.IsChecked = _settings.StartHiddenInTray;
            _loading = false;
            StartupStatusText.Foreground = (MediaBrush)FindResource("DangerBrush");
            StartupStatusText.Text = $"Windows startup could not be updated: {errorMessage}";
            return;
        }

        _settings.StartWithWindows = requested;
        _settings.StartHiddenInTray = startHidden;
        RefreshStartupStatus();
        _ = SaveSettingsAsync();
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void EqChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Eq.Enabled = EqEnabledCheck.IsChecked == true;
        if (_audio is not null)
        {
            _audio.Eq = _settings.Eq;
        }
        RefreshEqText();
        _ = SaveSettingsAsync();
    }

    private void EqSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        for (int i = 0; i < _eqBandSliders.Count; i++)
        {
            _settings.Eq.SetBandGainDb(i, _eqBandSliders[i].Value);
        }

        _settings.Eq.Enabled = EqEnabledCheck.IsChecked == true;
        _settings.Eq.PresetName = "Custom";
        if (_audio is not null)
        {
            _audio.Eq = _settings.Eq;
        }
        RefreshEqText();
        _ = SaveSettingsAsync();
    }

    private void PanChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Pan.Enabled = PanEnabledCheck.IsChecked == true;
        if (PanModeComboBox.SelectedItem is PanMode mode)
        {
            _settings.Pan.Mode = mode;
        }

        _settings.Pan.Strength = PanStrengthSlider.Value;
        _settings.Pan.Normalize();
        if (_audio is not null)
        {
            _audio.Pan = _settings.Pan;
        }
        RefreshPanText();
        _ = SaveSettingsAsync();
    }

    private void GroupVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.GroupVolumes.Normal = NormalVolumeSlider.Value;
        _settings.GroupVolumes.Enter = EnterVolumeSlider.Value;
        _settings.GroupVolumes.Space = SpaceVolumeSlider.Value;
        _settings.GroupVolumes.Backspace = BackspaceVolumeSlider.Value;
        _settings.GroupVolumes.Clamp();
        RefreshGroupVolumeText();
        _ = SaveSettingsAsync();
    }

    private void RefreshGroupVolumeText()
    {
        if (NormalVolumeText is null)
        {
            return;
        }

        NormalVolumeText.Text = $"{Math.Round(_settings.GroupVolumes.Normal * 100)}%";
        EnterVolumeText.Text = $"{Math.Round(_settings.GroupVolumes.Enter * 100)}%";
        SpaceVolumeText.Text = $"{Math.Round(_settings.GroupVolumes.Space * 100)}%";
        BackspaceVolumeText.Text = $"{Math.Round(_settings.GroupVolumes.Backspace * 100)}%";
    }

    private void RefreshEqText()
    {
        if (_eqBandValueTexts.Count == 0)
        {
            return;
        }

        _settings.Eq.Normalize();
        for (int i = 0; i < _eqBandValueTexts.Count; i++)
        {
            _eqBandValueTexts[i].Text = FormatDb(_settings.Eq.GetBandGainDb(i));
        }

        EqPresetText.Text = _settings.Eq.Enabled
            ? $"{_settings.Eq.PresetName} EQ"
            : "Flat, no EQ trim";
    }

    private void RefreshPanText()
    {
        if (PanStatusText is null)
        {
            return;
        }

        PanStatusText.Text = _settings.Pan.Enabled
            ? $"{FormatPanMode(_settings.Pan.Mode)} at {Math.Round(_settings.Pan.Strength * 100)}% width"
            : "Centered stereo playback";
    }

    private static string FormatDb(double value)
    {
        double rounded = Math.Round(value, 1);
        return rounded > 0 ? $"+{rounded:0.#} dB" : $"{rounded:0.#} dB";
    }

    private static string FormatFrequency(int hz) =>
        hz >= 1000 ? $"{hz / 1000.0:0.#}k" : hz.ToString();

    private static string FormatPanMode(PanMode mode) =>
        mode == PanMode.Random ? "Random pan" : "Key-position pan";

    private void RuleVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RuleVolumeText is not null)
        {
            RuleVolumeText.Text = $"{Math.Round(RuleVolumeSlider.Value * 100)}%";
        }
    }

    private void PresetFlat_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
    private void PresetWarm_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Warm", [4, 4, 2, 1, 0, -1, -2, -2, -2, -2]);
    private void PresetThock_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Thock", [6, 5, 3, 2, 1, 0, -1, -2, -2, -2]);
    private void PresetCrisp_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Crisp", [-2, -1, 0, 0, 1, 2, 4, 5, 4, 3]);
    private void PresetSoftNight_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Soft Night", [-2, -2, -1, -1, -1, -2, -3, -4, -4, -4]);

    private void ApplyEqPreset(string name, IReadOnlyList<double> gainsDb)
    {
        _settings.Eq.SetPreset(name, gainsDb);
        _loading = true;
        EqEnabledCheck.IsChecked = _settings.Eq.Enabled;
        for (int i = 0; i < _eqBandSliders.Count; i++)
        {
            _eqBandSliders[i].Value = _settings.Eq.GetBandGainDb(i);
        }

        _loading = false;
        if (_audio is not null)
        {
            _audio.Eq = _settings.Eq;
        }
        RefreshEqText();
        _ = SaveSettingsAsync();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.MinimizeToTray && !_exitRequested)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _keyboardHook.Dispose();
        _hotkeySource?.RemoveHook(WndProc);
        _globalHotkey.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        if (_audio is not null)
        {
            await _audio.DisposeAsync();
        }
        await _settingsService.SaveAsync(_settings);
    }

    private sealed class PackListItem(SoundPackMetadata metadata)
    {
        public SoundPackMetadata Metadata { get; } = metadata;
        public string Name => Metadata.Name;
        public string Description => Metadata.Description;
        public string TypeLabel => ResolveTypeLabel(Metadata);
        public string TagsText => Metadata.Tags.Count == 0
            ? TypeLabel
            : string.Join(" / ", Metadata.Tags.Take(3).Select(tag => tag.ToUpperInvariant()));
        public string DetailLine => $"{SampleCount} samples";
        private int SampleCount => Metadata.Groups.Values.Sum(files => files.Count);

        public override string ToString() => $"{Metadata.Name} - {Metadata.Description}";

        private static string ResolveTypeLabel(SoundPackMetadata metadata)
        {
            if (metadata.Tags.Any(tag => tag.Equals("typewriter", StringComparison.OrdinalIgnoreCase)))
            {
                return "Typewriter";
            }

            if (metadata.Tags.Any(tag => tag.Equals("switch", StringComparison.OrdinalIgnoreCase)))
            {
                return "Key Switch";
            }

            if (metadata.Tags.Any(tag => tag.Equals("laptop", StringComparison.OrdinalIgnoreCase)))
            {
                return "Laptop";
            }

            return "Pack";
        }
    }

    private void RefreshPackLibrary(string? preferredPackId = null)
    {
        string? previousSelection = preferredPackId
            ?? (PacksList.SelectedItem as PackListItem)?.Metadata.Id
            ?? _activePack?.Id;

        List<PackListItem> visiblePacks = _packs
            .Where(PackMatchesCurrentFilters)
            .Select(pack => new PackListItem(pack))
            .ToList();

        _refreshingPackLibrary = true;
        try
        {
            PacksList.Items.Clear();
            foreach (PackListItem item in visiblePacks)
            {
                PacksList.Items.Add(item);
            }

            PackListItem? selected = visiblePacks.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(previousSelection) &&
                item.Metadata.Id.Equals(previousSelection, StringComparison.OrdinalIgnoreCase))
                ?? visiblePacks.FirstOrDefault();
            PacksList.SelectedItem = selected;
            RefreshSelectedPackDetails(selected?.Metadata);
        }
        finally
        {
            _refreshingPackLibrary = false;
        }

        PackCountText.Text = visiblePacks.Count == _packs.Count
            ? (_packs.Count == 1 ? "1 pack available." : $"{_packs.Count} packs available.")
            : $"{visiblePacks.Count} of {_packs.Count} packs shown.";
        RefreshPackCategoryButtons();
    }

    private void RefreshPackCategoryButtons()
    {
        string filter = PackTypeComboBox.SelectedItem as string ?? PackFilter.All;
        ApplyCategoryButtonState(AllPacksCategoryButton, filter == PackFilter.All);
        ApplyCategoryButtonState(MechanicalCategoryButton, filter == PackFilter.Switches);
        ApplyCategoryButtonState(TypewriterCategoryButton, filter == PackFilter.Typewriters);
    }

    private void ApplyCategoryButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? (MediaBrush)FindResource("AccentBrush")
            : (MediaBrush)FindResource("PanelBrush");
        button.BorderBrush = selected
            ? (MediaBrush)FindResource("AccentHoverBrush")
            : (MediaBrush)FindResource("ControlBorderBrush");
        button.Foreground = (MediaBrush)FindResource("TextBrush");
        button.FontWeight = selected ? FontWeights.Bold : FontWeights.SemiBold;
    }

    private bool PackMatchesCurrentFilters(SoundPackMetadata pack)
    {
        string search = PackSearchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(search) && !PackMatchesSearch(pack, search))
        {
            return false;
        }

        string filter = PackTypeComboBox.SelectedItem as string ?? PackFilter.All;
        return filter switch
        {
            PackFilter.Switches => HasTag(pack, "switch"),
            PackFilter.Typewriters => HasTag(pack, "typewriter"),
            PackFilter.Quiet => HasTag(pack, "quiet") || HasTag(pack, "soft") || HasTag(pack, "laptop"),
            PackFilter.Digital => HasTag(pack, "digital") || HasTag(pack, "terminal"),
            _ => true
        };
    }

    private static bool PackMatchesSearch(SoundPackMetadata pack, string search) =>
        Contains(pack.Name, search) ||
        Contains(pack.Description, search) ||
        pack.Tags.Any(tag => Contains(tag, search));

    private static bool Contains(string value, string search) =>
        value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static bool HasTag(SoundPackMetadata pack, string tag) =>
        pack.Tags.Any(candidate => candidate.Equals(tag, StringComparison.OrdinalIgnoreCase));

    private static bool HasGroup(SoundPackMetadata? pack, string group) =>
        pack is not null &&
        pack.Groups.TryGetValue(group, out List<string>? files) &&
        files.Count > 0;

    private static bool HasAnyReleaseGroup(SoundPackMetadata? pack) =>
        pack is not null &&
        pack.Groups.Any(group =>
            group.Key.EndsWith("-release", StringComparison.OrdinalIgnoreCase) &&
            group.Value.Count > 0);

    private void RefreshSelectedPackDetails(SoundPackMetadata? pack)
    {
        if (pack is null)
        {
            SelectedPackNameText.Text = "No pack selected";
            SelectedPackTypeText.Text = "";
            SelectedPackDescriptionText.Text = "Try another search or category.";
            SelectedPackDetailsText.Text = "";
            PackWaveformPreview.Peaks = [];
            return;
        }

        PackListItem item = new(pack);
        SelectedPackNameText.Text = pack.Name;
        SelectedPackTypeText.Text = item.TypeLabel;
        SelectedPackDescriptionText.Text = pack.Description;
        SelectedPackDetailsText.Text =
            $"{item.TagsText} | {item.DetailLine} | {pack.Author} | {pack.License}";
        RefreshWaveformPreview(pack);
    }

    private void RefreshWaveformPreview(SoundPackMetadata pack)
    {
        if (_audio is null || !_audio.TryGetLoadedPack(pack.Id, out LoadedSoundPack? loadedPack) || loadedPack is null)
        {
            PackWaveformPreview.Peaks = [];
            return;
        }

        LoadedSoundSample? sample = loadedPack.Samples
            .OrderBy(group => group.Key.Equals("normal", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .SelectMany(group => group.Value)
            .FirstOrDefault(candidate => candidate.DecodedSamples.Length > 0);

        PackWaveformPreview.Peaks = sample is null
            ? []
            : WaveformPeakBuilder.BuildPeaks(sample);
    }

    private static class PackFilter
    {
        public const string All = "All";
        public const string Switches = "Mechanical switches";
        public const string Typewriters = "Typewriters";
        public const string Quiet = "Quiet";
        public const string Digital = "Digital";
    }

    private sealed class AppRuleListItem(AppRule rule)
    {
        public AppRule Rule { get; } = rule;

        public override string ToString()
        {
            string pack = string.IsNullOrWhiteSpace(Rule.SoundPackId) ? "" : $" | Pack: {Rule.SoundPackId}";
            string volume = Rule.VolumeOverride is double value ? $" | Volume: {Math.Round(value * 100)}%" : "";
            return $"{Rule.ProcessName} | {Rule.Mode}{pack}{volume}";
        }
    }
}
