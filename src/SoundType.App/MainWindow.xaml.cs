using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private const string EnterDingPackId = "soundtype-enter-ding";
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
    private readonly DebouncedAsyncAction _settingsSaveQueue;
    private readonly WaveformPeakCache _waveformPeakCache = new();
    private AudioEngine? _audio;
    private AppSettings _settings = new();
    private RuntimePlaybackProfile _playbackProfile = RuntimePlaybackProfile.FromSettings(new AppSettings());
    private IReadOnlyList<SoundPackMetadata> _packs = [];
    private IReadOnlyDictionary<string, SoundPackMetadata> _packsById = new Dictionary<string, SoundPackMetadata>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _releasePackIds = new(StringComparer.OrdinalIgnoreCase);
    private SoundPackMetadata? _enterDingPack;
    private SoundPackMetadata? _activePack;
    private HwndSource? _hotkeySource;
    private string? _currentProcessName;
    private string? _lastRecordedProcessName;
    private bool _loading = true;
    private bool _exitRequested;
    private bool _packFiltersConfigured;
    private bool _refreshingPackLibrary;
    private bool _updatingAppRuleEditor;
    private bool _updatingKeyboardInspector;
    private string _selectedKeyboardCode = "Space";
    private KeyboardKeyFilter _keyboardFilter = KeyboardKeyFilter.All;
    private int _packActivationVersion;

    public MainWindow()
    {
        InitializeComponent();
        _settingsSaveQueue = new DebouncedAsyncAction(
            TimeSpan.FromMilliseconds(400),
            cancellationToken => _settingsService.SaveAsync(_settings, cancellationToken));
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
        RebuildPlaybackProfile();
        ConfigureAppRuleEditors();
        ConfigurePanControls();
        ConfigureKeyboardRuleEditors();
        ConfigurePackFilters();
        TryStartAudio();
        await LoadPacksAsync();
        BuildKeyRules();
        ConfigureTray();
        BindSettingsToUi();
        ShowPage(LibraryPage);
        KeyboardHookStartResult keyboardHookStart = _keyboardHook.Start();
        if (!keyboardHookStart.Started)
        {
            AddStartupWarning(keyboardHookStart.ErrorMessage ?? "Keyboard hook unavailable.");
        }
        _activeAppTimer.Start();
        _loading = false;
        ShowPage(LibraryPage);
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
        RuntimePlaybackProfile profile = _playbackProfile;
        if (!e.IsRelease && profile.IgnoreKeyRepeats && e.IsRepeat)
        {
            return;
        }

        if (e.IsRelease && _releasePackIds.Count == 0)
        {
            return;
        }

        string? processName = _currentProcessName;
        PlaybackDecision decision = _ruleEngine.Decide(e.Key, processName, profile, _activePack);
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
            VolumeMultiplier = decision.VolumeMultiplier * profile.GetVolumeForGroup(decision.SoundGroup),
            ActiveProcessName = processName
        });

        if (!e.IsRelease && _settings.EnterDingEnabled && e.Key.Code.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            TryPlayEnterDing(e.Key, processName, decision.VolumeMultiplier);
        }
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

    private async Task LoadPacksAsync()
    {
        _waveformPeakCache.Clear();
        IReadOnlyList<SoundPackMetadata> discoveredPacks = _packLoader.DiscoverPacks(_packsRoot);
        _enterDingPack = discoveredPacks.FirstOrDefault(pack =>
            pack.Id.Equals(EnterDingPackId, StringComparison.OrdinalIgnoreCase));
        TryLoadEnterDingPack();
        _packs = discoveredPacks.Where(pack => !HasTag(pack, "hidden")).ToList();
        _packsById = _packs.ToDictionary(pack => pack.Id, StringComparer.OrdinalIgnoreCase);
        _releasePackIds = _packs
            .Where(HasAnyReleaseGroup)
            .Select(pack => pack.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RulePackComboBox.Items.Clear();
        SelectedKeyPackOverrideComboBox.Items.Clear();
        SelectedKeyPackOverrideComboBox.Items.Add("Default Pack");
        foreach (SoundPackMetadata pack in _packs)
        {
            RulePackComboBox.Items.Add(new PackListItem(pack));
            SelectedKeyPackOverrideComboBox.Items.Add(pack.Name);
        }
        SelectedKeyPackOverrideComboBox.SelectedIndex = 0;

        RefreshPackLibrary(_settings.ActiveSoundPackId);

        PackListItem? selected = PacksList.SelectedItem as PackListItem;

        if (selected is not null)
        {
            await ActivatePackAsync(selected.Metadata);
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

    private async Task ActivatePackAsync(SoundPackMetadata pack)
    {
        int activationVersion = Interlocked.Increment(ref _packActivationVersion);
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
            RefreshSettingsOverview();
            _ = SaveSettingsAsync();
            return;
        }

        if (!_audio.SetActivePack(pack.Id))
        {
            PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
            PackValidationText.Text = $"Loading {pack.Name}...";
            LoadedSoundPack loadedPack;
            try
            {
                loadedPack = await Task.Run(() => _packLoader.Load(pack));
            }
            catch (Exception ex)
            {
                if (activationVersion == Volatile.Read(ref _packActivationVersion))
                {
                    ShowPackError(ex.Message);
                }

                return;
            }

            if (activationVersion != Volatile.Read(ref _packActivationVersion))
            {
                return;
            }

            _audio.LoadPack(loadedPack);
        }

        PackValidationText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
        PackValidationText.Text = $"{pack.Name} by {pack.Author} is active. {pack.Description}";
        RefreshWaveformPreview(pack);
        RefreshStatus();
        RefreshSelectedKeyInspector();
        RefreshSettingsOverview();
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

    private bool TryLoadEnterDingPack()
    {
        if (_audio is null || _enterDingPack is null)
        {
            return false;
        }

        if (_audio.TryGetLoadedPack(EnterDingPackId, out _))
        {
            return true;
        }

        try
        {
            if (!_packLoader.Validate(_enterDingPack).IsValid)
            {
                return false;
            }

            _audio.LoadPack(_packLoader.Load(_enterDingPack), makeActive: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            return false;
        }
    }

    private void TryPlayEnterDing(KeyIdentity key, string? processName, double volumeMultiplier)
    {
        if (!TryLoadEnterDingPack())
        {
            return;
        }

        _audio?.TryPlay(new PlaybackRequest
        {
            Key = key,
            SoundGroup = "enter",
            SoundPackId = EnterDingPackId,
            VolumeMultiplier = Math.Clamp(volumeMultiplier * _settings.EnterDingVolume, 0.0, 1.0),
            ActiveProcessName = processName,
            BypassSoundShaping = true
        });
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
        _updatingAppRuleEditor = true;
        try
        {
            RuleModeComboBox.Items.Clear();
            RuleModeComboBox.Items.Add(AppRuleMode.Disabled);
            RuleModeComboBox.Items.Add(AppRuleMode.Default);
            RuleModeComboBox.Items.Add(AppRuleMode.EnabledOnly);
            RuleModeComboBox.Items.Add(AppRuleMode.UseSpecificPack);
            RuleModeComboBox.SelectedItem = AppRuleMode.Disabled;
            RuleVolumeSlider.Value = 1.0;
            RuleVolumeText.Text = "100%";
            RuleEnabledCheckBox.IsChecked = false;
        }
        finally
        {
            _updatingAppRuleEditor = false;
        }
    }

    private void ConfigurePanControls()
    {
        PanModeComboBox.Items.Clear();
        PanModeComboBox.Items.Add(new PanModeListItem(PanMode.KeyPosition, "Stereo"));
        PanModeComboBox.Items.Add(new PanModeListItem(PanMode.Random, "Random"));
    }

    private void ConfigureKeyboardRuleEditors()
    {
        SelectedKeyGroupComboBox.Items.Clear();
        SelectedKeySoundSlotComboBox.Items.Clear();
        foreach (string group in new[] { "Normal", "Enter", "Space", "Backspace", "Tab" })
        {
            SelectedKeyGroupComboBox.Items.Add(group);
            SelectedKeySoundSlotComboBox.Items.Add(group);
        }

        SelectedKeyGroupComboBox.SelectedItem = "Space";
        SelectedKeySoundSlotComboBox.SelectedItem = "Space";
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
                FontSize = 12,
                Visibility = Visibility.Collapsed
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
        EnterDingEnabledCheck.IsChecked = _settings.EnterDingEnabled;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        StartHiddenInTrayCheck.IsChecked = _settings.StartHiddenInTray;
        EqEnabledCheck.IsChecked = _settings.Eq.Enabled;
        NormalVolumeSlider.Value = _settings.GroupVolumes.Normal;
        EnterVolumeSlider.Value = _settings.GroupVolumes.Enter;
        SpaceVolumeSlider.Value = _settings.GroupVolumes.Space;
        BackspaceVolumeSlider.Value = _settings.GroupVolumes.Backspace;
        TabVolumeSlider.Value = _settings.GroupVolumes.Tab;
        _settings.Eq.Normalize();
        for (int i = 0; i < _eqBandSliders.Count; i++)
        {
            _eqBandSliders[i].Value = _settings.Eq.GetBandGainDb(i);
        }

        PanEnabledCheck.IsChecked = _settings.Pan.Enabled;
        PanModeComboBox.SelectedItem = PanModeComboBox.Items
            .OfType<PanModeListItem>()
            .FirstOrDefault(item => item.Mode == _settings.Pan.Mode);
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
        RefreshSettingsOverview();
        RefreshStatus();
        RefreshSelectedKeyInspector();
    }

    private void BuildKeyRules()
    {
        VisualKeyboard.SetExcludedKeys(_settings.ExcludedKeys);
        VisualKeyboard.SelectKey(_selectedKeyboardCode);
        ApplyKeyboardFilter();
        RefreshExcludedKeysText();
        RefreshSelectedKeyInspector();
    }

    private void RefreshAppRules()
    {
        AppRulesList.Items.Clear();
        foreach (AppRule rule in _settings.AppRules.OrderBy(rule => rule.ProcessName))
        {
            AppRulesList.Items.Add(new AppRuleListItem(rule, _packsById));
        }

        int enabledRules = _settings.AppRules.Count(rule => rule.Mode != AppRuleMode.Disabled);
        RuleCountText.Text = _settings.AppRules.Count.ToString();
        ActiveRulesText.Text = $"{enabledRules} active";
        AppRuleSelectionText.Text = $"{_settings.AppRules.Count} rules total";
        DefaultProfileText.Text = _activePack?.Name ?? "No pack selected";
        DefaultProfileTagText.Text = _activePack is null
            ? "Default"
            : new PackListItem(_activePack).TypeLabel;

        if (AppRulesList.SelectedItem is AppRuleListItem selected)
        {
            RuleEditorProcessText.Text = selected.ProcessName;
        }
        else if (string.IsNullOrWhiteSpace(ProcessRuleTextBox.Text))
        {
            RuleEditorProcessText.Text = "New rule";
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

        string displayName = string.IsNullOrWhiteSpace(processName) ? "Unknown" : processName;
        CurrentAppText.Text = displayName;
        CurrentAppStatusText.Text = string.IsNullOrWhiteSpace(processName) ? "Waiting for focus" : "Active now";
        LastDetectedAppText.Text = displayName;
        LastDetectedTimeText.Text = string.IsNullOrWhiteSpace(processName) ? "No app detected" : "Updated just now";
        if (string.IsNullOrWhiteSpace(ProcessRuleTextBox.Text) && processName is not null)
        {
            ProcessRuleTextBox.Text = processName;
            RuleEditorProcessText.Text = processName;
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

        if (KeyboardActivePackText is not null)
        {
            KeyboardActivePackText.Text = _activePack?.Name ?? "No pack";
        }
    }

    private void RefreshTrayStatus()
    {
        TrayStatusText.Text = _settings.MinimizeToTray
            ? "Don't exit when the window is closed"
            : "Closing the window exits SoundType";
    }

    private void RefreshStartupStatus()
    {
        StartupStatusText.Foreground = (MediaBrush)FindResource("MutedTextBrush");
        if (!_settings.StartWithWindows)
        {
            StartupStatusText.Text = "Disabled";
            return;
        }

        StartupStatusText.Foreground = (MediaBrush)FindResource("AccentHoverBrush");
        StartupStatusText.Text = _settings.StartHiddenInTray
            ? "Enabled, hidden"
            : "Enabled";
    }

    private void RefreshSettingsOverview()
    {
        if (SettingsActivePackNameText is null)
        {
            return;
        }

        SettingsActivePackNameText.Text = _activePack?.Name ?? "No pack selected";
        SettingsActivePackSizeText.Text = _activePack is null ? "--" : FormatBytes(GetDirectorySize(_activePack.FolderPath));
        SettingsActivePackPreviewImage.Source = _activePack is null ? null : CreatePackPreviewImageSource(_activePack);
        PacksFolderPathText.Text = _packsRoot;
        SettingsPacksInstalledText.Text = _packs.Count.ToString();
    }

    private static long GetDirectorySize(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    private void SelectPackInLibrary(string packId)
    {
        PackListItem? selected = PacksList.Items
            .OfType<PackListItem>()
            .FirstOrDefault(item => item.Metadata.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            PacksList.SelectedItem = selected;
        }
    }

    private static bool ShouldStartHiddenInTray() =>
        Environment.GetCommandLineArgs().Any(arg =>
            arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--hidden", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

    private void UpdateEnableButton()
    {
        EnabledToggle.IsChecked = _settings.Enabled;
        EnabledToggle.Content = _settings.Enabled ? "Listening" : "Muted";
        EnabledToggle.Background = (MediaBrush)FindResource(_settings.Enabled ? "AccentSoftBrush" : "DisabledBackgroundBrush");
        EnabledToggle.BorderBrush = (MediaBrush)FindResource(_settings.Enabled ? "AccentBrush" : "ControlBorderBrush");
        EnabledToggle.Foreground = (MediaBrush)FindResource(_settings.Enabled ? "AccentHoverBrush" : "MutedTextBrush");
    }

    private void ShowLibrary_Click(object sender, RoutedEventArgs e) => ShowPage(LibraryPage);
    private void ShowAudio_Click(object sender, RoutedEventArgs e) => ShowPage(AudioPage);
    private void ShowKeyboard_Click(object sender, RoutedEventArgs e) => ShowPage(KeyboardPage);
    private void ShowRules_Click(object sender, RoutedEventArgs e) => ShowPage(RulesPage);
    private void ShowSettings_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage);
    private void FocusNewRule_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(RulesPage);
        AppRulesList.SelectedItem = null;
        RuleEditorProcessText.Text = string.IsNullOrWhiteSpace(ProcessRuleTextBox.Text)
            ? "New rule"
            : ProcessRuleTextBox.Text.Trim();
        ProcessRuleTextBox.Focus();
    }

    private void DetectCurrentApp_Click(object sender, RoutedEventArgs e)
    {
        RefreshCurrentApp();
        if (!string.IsNullOrWhiteSpace(_currentProcessName))
        {
            ProcessRuleTextBox.Text = _currentProcessName;
            RuleEditorProcessText.Text = _currentProcessName;
        }

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
        UpdateHeaderText(activePage);
    }

    private void UpdateHeaderText(FrameworkElement activePage)
    {
        if (ReferenceEquals(activePage, LibraryPage))
        {
            PageTitleText.Text = "Library";
            PageSubtitleText.Text = "Browse packs";
            return;
        }

        if (ReferenceEquals(activePage, AudioPage))
        {
            PageTitleText.Text = "Audio Effects";
            PageSubtitleText.Text = "Tune playback";
            return;
        }

        if (ReferenceEquals(activePage, KeyboardPage))
        {
            PageTitleText.Text = "Keyboard Rules";
            PageSubtitleText.Text = "Per-key controls";
            return;
        }

        if (ReferenceEquals(activePage, RulesPage))
        {
            PageTitleText.Text = "App Rules";
            PageSubtitleText.Text = "Foreground profiles";
            return;
        }

        PageTitleText.Text = "Settings";
        PageSubtitleText.Text = "Startup and privacy";
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
            button.Background = selected ? (MediaBrush)FindResource("PanelElevatedBrush") : System.Windows.Media.Brushes.Transparent;
            button.BorderBrush = selected ? (MediaBrush)FindResource("ControlBorderBrush") : System.Windows.Media.Brushes.Transparent;
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

        RebuildPlaybackProfile();
        _settingsSaveQueue.Schedule();
        await Task.CompletedTask;
    }

    private void RebuildPlaybackProfile() =>
        _playbackProfile = RuntimePlaybackProfile.FromSettings(_settings);

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

        _trayIcon.Icon = CreateTrayIcon();
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "SoundType.ico");
        return File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;
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
            _ = ActivatePackAsync(item.Metadata);
        }
    }

    private void PackSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePackSearchPlaceholder();

        if (_loading) return;
        RefreshPackLibrary();
    }

    private void PackSearchTextBox_FocusChanged(object sender, RoutedEventArgs e) =>
        UpdatePackSearchPlaceholder();

    private void UpdatePackSearchPlaceholder()
    {
        PackSearchPlaceholder.Visibility =
            string.IsNullOrWhiteSpace(PackSearchTextBox.Text) && !PackSearchTextBox.IsKeyboardFocused
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ClearPackSearch_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        PackSearchTextBox.Clear();
        PackSearchTextBox.Focus();
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

    private void QuietCategory_Click(object sender, RoutedEventArgs e) =>
        SetPackFilter(PackFilter.Quiet);

    private void DigitalCategory_Click(object sender, RoutedEventArgs e) =>
        SetPackFilter(PackFilter.Digital);

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

    private void PreviewEnterDing_Click(object sender, RoutedEventArgs e) =>
        TryPlayEnterDing(new KeyIdentity("Enter", "Enter", KeyCategory.Special), _currentProcessName, 1.0);
    private void PreviewSpace_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("space");
    private void PreviewBackspace_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("backspace");
    private void PreviewTab_Click(object sender, RoutedEventArgs e) => PreviewPackGroup("tab");

    private async void PreviewPackGroup(string group)
    {
        if (PacksList.SelectedItem is PackListItem item &&
            (_activePack is null || !item.Metadata.Id.Equals(_activePack.Id, StringComparison.OrdinalIgnoreCase)))
        {
            await ActivatePackAsync(item.Metadata);
        }

        _audio?.Preview(group);
    }

    private void HeaderMore_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        System.Windows.Controls.ContextMenu menu = new();
        menu.Items.Add(CreateMenuItem("Open Packs Folder", (_, _) => OpenFolder(_packsRoot)));
        menu.Items.Add(CreateMenuItem("Open Settings", (_, _) => ShowPage(SettingsPage)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Reload Library", (_, _) => RefreshPackLibrary(_activePack?.Id)));
        OpenContextMenu(menu, sender as FrameworkElement);
    }

    private void PackRowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PackListItem item)
        {
            return;
        }

        System.Windows.Controls.ContextMenu menu = new();
        menu.Items.Add(CreateMenuItem("Use This Pack", async (_, _) => await SelectAndActivatePackAsync(item)));
        menu.Items.Add(CreateMenuItem("Preview Normal", async (_, _) =>
        {
            await SelectAndActivatePackAsync(item);
            _audio?.Preview("normal");
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Open Pack Folder", (_, _) => OpenFolder(item.Metadata.FolderPath)));
        OpenContextMenu(menu, button);
    }

    private async Task SelectAndActivatePackAsync(PackListItem item)
    {
        PacksList.SelectedItem = item;
        await ActivatePackAsync(item.Metadata);
    }

    private static MenuItem CreateMenuItem(string header, RoutedEventHandler click)
    {
        MenuItem item = new() { Header = header };
        item.Click += click;
        return item;
    }

    private static void OpenContextMenu(System.Windows.Controls.ContextMenu menu, FrameworkElement? placementTarget)
    {
        menu.PlacementTarget = placementTarget;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private async void ImportSoundPack_Click(object sender, RoutedEventArgs e)
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
            await ReloadPacksAndSelectAsync(metadata.Id);
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
                await ReloadPacksAndSelectAsync(metadata.Id);
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

    private async Task ReloadPacksAndSelectAsync(string packId)
    {
        _settings.ActiveSoundPackId = packId;
        await LoadPacksAsync();
        PackListItem? selected = PacksList.Items
            .OfType<PackListItem>()
            .FirstOrDefault(item => item.Metadata.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            PacksList.SelectedItem = selected;
        }
    }

    private void ShowPackError(string message)
    {
        PackValidationText.Foreground = (MediaBrush)FindResource("DangerBrush");
        PackValidationText.Text = message;
    }

    private void OpenPackFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_activePack?.FolderPath ?? _packsRoot);
    }

    private static void OpenFolder(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
    }

    private void VisualKeyboard_KeyToggled(object sender, KeyboardKeyToggledEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _selectedKeyboardCode = e.Code;
        if (e.IsExcluded)
        {
            _settings.ExcludedKeys.Add(e.Code);
        }
        else
        {
            _settings.ExcludedKeys.Remove(e.Code);
        }

        RefreshExcludedKeysText();
        RefreshSelectedKeyInspector();
        _ = SaveSettingsAsync();
    }

    private void VisualKeyboard_KeySelected(object sender, KeyboardKeySelectedEventArgs e)
    {
        _selectedKeyboardCode = e.Code;
        RefreshSelectedKeyInspector();
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

    private void KeyboardSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyKeyboardFilter();
    }

    private void KeyboardShowAll_Click(object sender, RoutedEventArgs e) =>
        SetKeyboardFilter(KeyboardKeyFilter.All);

    private void KeyboardShowEnabled_Click(object sender, RoutedEventArgs e) =>
        SetKeyboardFilter(KeyboardKeyFilter.Enabled);

    private void KeyboardShowExcluded_Click(object sender, RoutedEventArgs e) =>
        SetKeyboardFilter(KeyboardKeyFilter.Excluded);

    private void SetKeyboardFilter(KeyboardKeyFilter filter)
    {
        _keyboardFilter = filter;
        KeyboardShowAllButton.Style = (Style)FindResource(filter == KeyboardKeyFilter.All
            ? "KeyboardSelectedToolbarButtonStyle"
            : "KeyboardToolbarButtonStyle");
        KeyboardShowEnabledButton.Style = (Style)FindResource(filter == KeyboardKeyFilter.Enabled
            ? "KeyboardSelectedToolbarButtonStyle"
            : "KeyboardToolbarButtonStyle");
        KeyboardShowExcludedButton.Style = (Style)FindResource(filter == KeyboardKeyFilter.Excluded
            ? "KeyboardSelectedToolbarButtonStyle"
            : "KeyboardToolbarButtonStyle");
        ApplyKeyboardFilter();
    }

    private void ApplyKeyboardFilter()
    {
        if (VisualKeyboard is null || KeyboardSearchTextBox is null)
        {
            return;
        }

        VisualKeyboard.ApplyFilter(KeyboardSearchTextBox.Text, _keyboardFilter);
    }

    private void SelectedKeyEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _updatingKeyboardInspector)
        {
            return;
        }

        bool isExcluded = SelectedKeyEnabledCheck.IsChecked != true;
        if (isExcluded)
        {
            _settings.ExcludedKeys.Add(_selectedKeyboardCode);
        }
        else
        {
            _settings.ExcludedKeys.Remove(_selectedKeyboardCode);
        }

        VisualKeyboard.SetKeyExcluded(_selectedKeyboardCode, isExcluded);
        RefreshExcludedKeysText();
        RefreshSelectedKeyInspector();
        _ = SaveSettingsAsync();
    }

    private void PreviewSelectedKey_Click(object sender, RoutedEventArgs e) =>
        PreviewPackGroup(ResolveSoundGroupForKey(_selectedKeyboardCode));

    private void ResetSelectedKey_Click(object sender, RoutedEventArgs e)
    {
        HashSet<string> defaults = AppSettings.DefaultExcludedKeys();
        bool isExcluded = defaults.Contains(_selectedKeyboardCode);
        if (isExcluded)
        {
            _settings.ExcludedKeys.Add(_selectedKeyboardCode);
        }
        else
        {
            _settings.ExcludedKeys.Remove(_selectedKeyboardCode);
        }

        VisualKeyboard.SetKeyExcluded(_selectedKeyboardCode, isExcluded);
        RefreshExcludedKeysText();
        RefreshSelectedKeyInspector();
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
            RefreshKeyboardStats();
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
        RefreshKeyboardStats();
    }

    private void RefreshKeyboardStats()
    {
        if (KeyboardEnabledCountText is null)
        {
            return;
        }

        KeyboardEnabledCountText.Text = VisualKeyboard.EnabledCount.ToString();
        KeyboardExcludedCountText.Text = VisualKeyboard.ExcludedCount.ToString();
        KeyboardActivePackText.Text = _activePack?.Name ?? "No pack";
        KeyboardPreviewKeyText.Text = KeyIdentityMapper.GetDisplayName(_selectedKeyboardCode);
    }

    private void RefreshSelectedKeyInspector()
    {
        if (SelectedKeyNameText is null)
        {
            return;
        }

        string displayName = KeyIdentityMapper.GetDisplayName(_selectedKeyboardCode);
        string soundGroup = ResolveSoundGroupForKey(_selectedKeyboardCode);
        bool isExcluded = _settings.ExcludedKeys.Contains(_selectedKeyboardCode);

        _updatingKeyboardInspector = true;
        try
        {
            SelectedKeyTokenText.Text = displayName;
            SelectedKeyNameText.Text = displayName;
            SelectedKeyEnabledCheck.IsChecked = !isExcluded;
            SelectedKeyGroupComboBox.SelectedItem = ToTitleCase(soundGroup);
            SelectedKeySoundSlotComboBox.SelectedItem = ToTitleCase(soundGroup);
            KeyboardPreviewSelectedButton.Content = $"Preview {displayName}";
            KeyboardPreviewKeyText.Text = displayName;
            SelectedKeyWaveformTitleText.Text = $"Waveform ({displayName})";
        }
        finally
        {
            _updatingKeyboardInspector = false;
        }

        RefreshSelectedKeyWaveform(soundGroup);
        RefreshKeyboardStats();
    }

    private void RefreshSelectedKeyWaveform(string soundGroup)
    {
        if (SelectedKeyWaveformPreview is null)
        {
            return;
        }

        if (_audio is null ||
            _activePack is null ||
            !_audio.TryGetLoadedPack(_activePack.Id, out LoadedSoundPack? loadedPack) ||
            loadedPack is null)
        {
            SelectedKeyWaveformPreview.Peaks = [];
            return;
        }

        LoadedSoundSample? sample = loadedPack.Samples
            .Where(group => group.Key.Equals(soundGroup, StringComparison.OrdinalIgnoreCase))
            .SelectMany(group => group.Value)
            .FirstOrDefault(candidate => candidate.DecodedSamples.Length > 0);
        if (sample is null)
        {
            sample = loadedPack.Samples
                .OrderBy(group => group.Key.Equals("normal", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .SelectMany(group => group.Value)
                .FirstOrDefault(candidate => candidate.DecodedSamples.Length > 0);
        }

        SelectedKeyWaveformPreview.Peaks = sample is null
            ? []
            : _waveformPeakCache.GetPeaks(sample);
    }

    private static string ResolveSoundGroupForKey(string code) =>
        code switch
        {
            "Enter" => "enter",
            "Space" => "space",
            "Backspace" => "backspace",
            "Tab" => "tab",
            _ => "normal"
        };

    private static string ToTitleCase(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

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
        if (RuleEnabledCheckBox.IsChecked != true)
        {
            mode = AppRuleMode.Disabled;
        }

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
        RuleEditorProcessText.Text = processName;
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
        IReadOnlyList<RecentAppEntry> recentApps = _recentApps.ListRecentApps();
        foreach (RecentAppEntry app in recentApps.Take(8))
        {
            RecentAppsList.Items.Add(app.ProcessName);
        }

        if (selected is not null && RecentAppsList.Items.Contains(selected))
        {
            RecentAppsList.SelectedItem = selected;
        }

        RecentAppSummaryText.Text = recentApps.Count == 0
            ? "No recent apps detected yet."
            : string.Join(Environment.NewLine, recentApps.Take(4).Select(FormatRecentAppSummary));
    }

    private void RecentAppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentAppsList.SelectedItem is string processName)
        {
            ProcessRuleTextBox.Text = processName;
            RuleEditorProcessText.Text = processName;
        }
    }

    private static string FormatRecentAppSummary(RecentAppEntry app)
    {
        TimeSpan age = DateTimeOffset.UtcNow - app.LastSeenUtc;
        string seen = age.TotalSeconds < 30
            ? "Now"
            : age.TotalMinutes < 60
                ? $"{Math.Max(1, Math.Round(age.TotalMinutes))}m ago"
                : $"{Math.Round(age.TotalHours)}h ago";

        return $"{app.ProcessName}    {seen}";
    }

    private void AppRulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppRulesList.SelectedItem is not AppRuleListItem selected)
        {
            return;
        }

        AppRule rule = selected.Rule;
        _updatingAppRuleEditor = true;
        try
        {
            ProcessRuleTextBox.Text = rule.ProcessName;
            RuleEditorProcessText.Text = rule.ProcessName;
            RuleModeComboBox.SelectedItem = rule.Mode;
            RuleVolumeSlider.Value = rule.VolumeOverride ?? 1.0;
            RuleEnabledCheckBox.IsChecked = rule.Mode != AppRuleMode.Disabled;

            if (!string.IsNullOrWhiteSpace(rule.SoundPackId))
            {
                RulePackComboBox.SelectedItem = RulePackComboBox.Items
                    .OfType<PackListItem>()
                    .FirstOrDefault(item => item.Metadata.Id.Equals(rule.SoundPackId, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                RulePackComboBox.SelectedItem = null;
            }
        }
        finally
        {
            _updatingAppRuleEditor = false;
        }
    }

    private void RuleEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingAppRuleEditor)
        {
            return;
        }

        if (RuleEnabledCheckBox.IsChecked == true)
        {
            if (RuleModeComboBox.SelectedItem is AppRuleMode.Disabled)
            {
                RuleModeComboBox.SelectedItem = AppRuleMode.Default;
            }

            return;
        }

        RuleModeComboBox.SelectedItem = AppRuleMode.Disabled;
    }

    private void SettingsCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.IgnoreKeyRepeats = IgnoreRepeatsCheck.IsChecked == true;
        _settings.EnterDingEnabled = EnterDingEnabledCheck.IsChecked == true;
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

    private async void ApplySettings_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync();
        RefreshSettingsOverview();
    }

    private async void ResetSettingsToDefaults_Click(object sender, RoutedEventArgs e)
    {
        bool wasLoading = _loading;
        _loading = true;
        _settings = new AppSettings
        {
            StartWithWindows = _startup.IsEnabled()
        };
        BindSettingsToUi();
        SelectPackInLibrary(_settings.ActiveSoundPackId);
        _loading = wasLoading;

        if (PacksList.SelectedItem is PackListItem selected)
        {
            await ActivatePackAsync(selected.Metadata);
        }

        await SaveSettingsAsync();
        RefreshSettingsOverview();
    }

    private void ClearWaveformCache_Click(object sender, RoutedEventArgs e)
    {
        _waveformPeakCache.Clear();
        if (_activePack is not null)
        {
            RefreshWaveformPreview(_activePack);
        }
    }

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
        if (PanModeComboBox.SelectedItem is PanModeListItem item)
        {
            _settings.Pan.Mode = item.Mode;
        }
        else if (PanModeComboBox.SelectedItem is PanMode mode)
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
        _settings.GroupVolumes.Tab = TabVolumeSlider.Value;
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
        TabVolumeText.Text = $"{Math.Round(_settings.GroupVolumes.Tab * 100)}%";
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

        PanStatusText.Text = $"{Math.Round(_settings.Pan.Strength * 100)}%";
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
        RebuildPlaybackProfile();
        await _settingsSaveQueue.DisposeAsync();
        await _settingsService.SaveAsync(_settings);
    }

    private static string? ResolvePackPreviewImagePath(SoundPackMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.PreviewImage) ||
            string.IsNullOrWhiteSpace(metadata.FolderPath))
        {
            return null;
        }

        string previewPath = Path.GetFullPath(Path.Combine(metadata.FolderPath, metadata.PreviewImage));
        string packFolder = Path.GetFullPath(metadata.FolderPath);
        if (!previewPath.StartsWith(packFolder, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(previewPath))
        {
            return null;
        }

        string extension = Path.GetExtension(previewPath);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? previewPath
                : null;
    }

    private static ImageSource? CreatePackPreviewImageSource(SoundPackMetadata metadata)
    {
        string? previewPath = ResolvePackPreviewImagePath(metadata);
        if (previewPath is null)
        {
            return null;
        }

        try
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(previewPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
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
        public string TraitLabel => ResolveTraitLabel(Metadata);
        public string DetailLine => FormatBytes(GetDirectorySize(Metadata.FolderPath));
        public int SampleCount => Metadata.Groups.Values.Sum(files => files.Count);
        public string KeyCountText => Metadata.KeyOverrides.Count == 0
            ? "104 keys"
            : $"{Metadata.KeyOverrides.Count:N0} custom";
        public string? PreviewImagePath => ResolvePackPreviewImagePath(Metadata);

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

        private static string ResolveTraitLabel(SoundPackMetadata metadata)
        {
            string? tag = metadata.Tags.FirstOrDefault(tag =>
                !tag.Equals("switch", StringComparison.OrdinalIgnoreCase) &&
                !tag.Equals("typewriter", StringComparison.OrdinalIgnoreCase) &&
                !tag.Equals("keyboard", StringComparison.OrdinalIgnoreCase) &&
                !tag.Equals("mechanical", StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(tag)
                ? "Linear"
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tag.Replace('-', ' '));
        }
    }

    private void RefreshPackLibrary(string? preferredPackId = null)
    {
        string? previousSelection = preferredPackId
            ?? (PacksList.SelectedItem as PackListItem)?.Metadata.Id
            ?? _activePack?.Id;

        string? priorityPackId = preferredPackId
            ?? _activePack?.Id
            ?? _settings.ActiveSoundPackId;

        List<PackListItem> visiblePacks = _packs
            .Where(PackMatchesCurrentFilters)
            .Select(pack => new PackListItem(pack))
            .OrderBy(item => !string.IsNullOrWhiteSpace(priorityPackId) &&
                item.Metadata.Id.Equals(priorityPackId, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
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
            if (selected is not null)
            {
                PacksList.ScrollIntoView(selected);
            }
            RefreshSelectedPackDetails(selected?.Metadata);
        }
        finally
        {
            _refreshingPackLibrary = false;
        }

        PackCountText.Text = visiblePacks.Count == _packs.Count
            ? (_packs.Count == 1 ? "1 pack" : $"{_packs.Count} packs")
            : $"{visiblePacks.Count} of {_packs.Count} packs";
        RefreshPackCategoryButtons();
    }

    private void RefreshPackCategoryButtons()
    {
        string filter = PackTypeComboBox.SelectedItem as string ?? PackFilter.All;
        ApplyCategoryButtonState(AllPacksCategoryButton, filter == PackFilter.All);
        ApplyCategoryButtonState(MechanicalCategoryButton, filter == PackFilter.Switches);
        ApplyCategoryButtonState(TypewriterCategoryButton, filter == PackFilter.Typewriters);
        ApplyCategoryButtonState(QuietCategoryButton, filter == PackFilter.Quiet);
        ApplyCategoryButtonState(DigitalCategoryButton, filter == PackFilter.Digital);
    }

    private void ApplyCategoryButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Background = selected
            ? (MediaBrush)FindResource("AccentSoftBrush")
            : (MediaBrush)FindResource("PanelBrush");
        button.BorderBrush = selected
            ? (MediaBrush)FindResource("AccentBrush")
            : (MediaBrush)FindResource("ControlBorderBrush");
        button.Foreground = (MediaBrush)FindResource(selected ? "AccentHoverBrush" : "TextBrush");
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
            SelectedPackAuthorText.Text = "";
            SelectedPackTypeText.Text = "";
            SelectedPackTraitText.Text = "";
            SelectedPackDescriptionText.Text = "Try another search or category.";
            SelectedPackVersionText.Text = "";
            SelectedPackReleasedText.Text = "";
            SelectedPackSizeText.Text = "";
            SelectedPackSamplesText.Text = "";
            SelectedPackKeysText.Text = "";
            SelectedPackCompatibilityText.Text = "";
            SelectedPackNotesText.Text = "";
            SelectedPackPreviewImage.Source = null;
            PackWaveformPreview.Peaks = [];
            AudioWaveformPreview.Peaks = [];
            return;
        }

        PackListItem item = new(pack);
        SelectedPackNameText.Text = pack.Name;
        SelectedPackAuthorText.Text = string.IsNullOrWhiteSpace(pack.Author) ? "Unknown author" : pack.Author;
        SelectedPackTypeText.Text = item.TypeLabel;
        SelectedPackTraitText.Text = item.TraitLabel;
        SelectedPackDescriptionText.Text = pack.Description;
        SelectedPackPreviewImage.Source = CreatePackPreviewImageSource(pack);
        SelectedPackVersionText.Text = string.IsNullOrWhiteSpace(pack.Version) ? "1.0.0" : pack.Version;
        SelectedPackReleasedText.Text = GetPackReleasedDate(pack);
        SelectedPackSizeText.Text = item.DetailLine;
        SelectedPackSamplesText.Text = item.SampleCount.ToString("N0");
        SelectedPackKeysText.Text = item.KeyCountText;
        SelectedPackCompatibilityText.Text = "All keyboards";
        SelectedPackNotesText.Text = BuildPackNotes(pack);
        RefreshWaveformPreview(pack);
    }

    private static string GetPackReleasedDate(SoundPackMetadata pack)
    {
        if (!Directory.Exists(pack.FolderPath))
        {
            return "--";
        }

        DateTime timestamp = Directory.GetLastWriteTime(pack.FolderPath);
        return timestamp == DateTime.MinValue ? "--" : timestamp.ToString("MMM d, yyyy");
    }

    private static string BuildPackNotes(SoundPackMetadata pack)
    {
        List<string> notes = [];
        if (!string.IsNullOrWhiteSpace(pack.License))
        {
            notes.Add($"{pack.License} license.");
        }

        int groupCount = pack.Groups.Count(group => group.Value.Count > 0);
        if (groupCount > 0)
        {
            notes.Add($"{groupCount:N0} sound groups available.");
        }

        if (notes.Count == 0)
        {
            notes.Add("No additional notes.");
        }

        return string.Join(Environment.NewLine, notes);
    }

    private void RefreshWaveformPreview(SoundPackMetadata pack)
    {
        if (_audio is null || !_audio.TryGetLoadedPack(pack.Id, out LoadedSoundPack? loadedPack) || loadedPack is null)
        {
            PackWaveformPreview.Peaks = [];
            AudioWaveformPreview.Peaks = [];
            return;
        }

        LoadedSoundSample? sample = loadedPack.Samples
            .OrderBy(group => group.Key.Equals("normal", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .SelectMany(group => group.Value)
            .FirstOrDefault(candidate => candidate.DecodedSamples.Length > 0);

        IReadOnlyList<double> peaks = sample is null
            ? []
            : _waveformPeakCache.GetPeaks(sample);
        PackWaveformPreview.Peaks = peaks;
        AudioWaveformPreview.Peaks = peaks;
    }

    private static class PackFilter
    {
        public const string All = "All";
        public const string Switches = "Mechanical switches";
        public const string Typewriters = "Typewriters";
        public const string Quiet = "Quiet";
        public const string Digital = "Digital";
    }

    private sealed class AppRuleListItem(AppRule rule, IReadOnlyDictionary<string, SoundPackMetadata> packsById)
    {
        public AppRule Rule { get; } = rule;
        public string ProcessName => Rule.ProcessName;
        public string ProcessInitial => string.IsNullOrWhiteSpace(Rule.ProcessName)
            ? "?"
            : Rule.ProcessName.Trim()[0].ToString().ToUpperInvariant();
        public string ModeLabel => Rule.Mode switch
        {
            AppRuleMode.Default => "Default",
            AppRuleMode.Disabled => "Disabled",
            AppRuleMode.EnabledOnly => "Enabled Only",
            AppRuleMode.UseSpecificPack => "Use Specific Pack",
            _ => Rule.Mode.ToString()
        };

        public string PackDisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Rule.SoundPackId))
                {
                    return "(Default)";
                }

                return packsById.TryGetValue(Rule.SoundPackId, out SoundPackMetadata? pack)
                    ? pack.Name
                    : Rule.SoundPackId;
            }
        }

        public int VolumePercent => (int)Math.Round(Math.Clamp(Rule.VolumeOverride ?? 1.0, 0.0, 1.5) * 100);
        public string VolumeText => $"{VolumePercent}%";
        public string LastSeenText => "Now";

        public override string ToString()
        {
            string pack = string.IsNullOrWhiteSpace(Rule.SoundPackId) ? "" : $" | Pack: {PackDisplayName}";
            string volume = Rule.VolumeOverride is double value ? $" | Volume: {Math.Round(value * 100)}%" : "";
            return $"{Rule.ProcessName} | {ModeLabel}{pack}{volume}";
        }
    }

    private sealed class PanModeListItem(PanMode mode, string displayName)
    {
        public PanMode Mode { get; } = mode;

        public override string ToString() => displayName;
    }
}
