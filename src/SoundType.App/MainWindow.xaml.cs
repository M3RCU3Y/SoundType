using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SoundType.App.Controls;
using SoundType.App.ViewModels;
using SoundType.Audio;
using SoundType.Core.Models;
using SoundType.Core.Rules;
using SoundType.Core.Services;
using SoundType.Core.Settings;
using SoundType.Input;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using ShapeRectangle = System.Windows.Shapes.Rectangle;

namespace SoundType.App;

public partial class MainWindow : Window
{
    private const int ToggleHotkeyId = 0x534B;
    private const string EnterDingPackId = "soundtype-enter-ding";
    private const double CardRestScale = 0.996;
    private const double CardHoverScale = 1.0;
    private static readonly TimeSpan EnterDingMinimumInterval = TimeSpan.FromMilliseconds(150);
    private static readonly IReadOnlyList<EnterDingSoundListItem> EnterDingSounds =
    [
        new("random", "Random"),
        new("ding-01", "Classic Typewriter Bell"),
        new("ding-02", "Bright Margin Bell"),
        new("ding-03", "Antique Return Bell"),
        new("ding-04", "Warm Carriage Bell"),
        new("ding-05", "Clean Line Bell"),
        new("ding-06", "Tiny Line Bell"),
        new("ding-07", "Reward Tap Bell"),
        new("ding-08", "Soft Desk Chime")
    ];
    private const string HotkeyTargetToggleListening = "ToggleListening";
    private const string HotkeyTargetPreviewNormal = "PreviewNormal";
    private const string HotkeyTargetNextPack = "NextPack";
    private const string HotkeyTargetPreviousPack = "PreviousPack";
    private const int RecentAppActivitySlots = 22;
    private static readonly TimeSpan RecentAppActivityWindow = TimeSpan.FromMinutes(30);
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
    private readonly DispatcherTimer _outputMeterTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly string _packsRoot;
    private readonly Forms.NotifyIcon _trayIcon = new();
    private readonly List<Slider> _eqBandSliders = [];
    private readonly List<TextBlock> _eqBandValueTexts = [];
    private readonly List<ShapeRectangle> _leftOutputMeterBars = [];
    private readonly List<ShapeRectangle> _rightOutputMeterBars = [];
    private readonly List<string> _startupWarnings = [];
    private readonly Dictionary<string, DateTimeOffset> _lastAcceptedKeyDownByCode = new(StringComparer.OrdinalIgnoreCase);
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
    private string? _lastDetectedProcessName;
    private string? _lastRecordedProcessName;
    private string? _selectedPacksFolder;
    private bool _loading = true;
    private bool _exitRequested;
    private bool _packFiltersConfigured;
    private bool _refreshingPackLibrary;
    private bool _showingFavoritePacks;
    private bool _updatingAppRuleEditor;
    private bool _updatingKeyboardInspector;
    private string _selectedKeyboardCode = "Space";
    private KeyboardKeyFilter _keyboardFilter = KeyboardKeyFilter.All;
    private int _packActivationVersion;
    private ScrollViewer? _packsScrollViewer;
    private string? _recordingHotkeyTarget;
    private readonly DispatcherTimer _libraryScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(12) };
    private double _libraryScrollTarget;
    private const double LibraryScrollWheelScale = 0.64;
    private const double LibraryScrollImmediateRatio = 0.58;
    private const double LibraryScrollEaseRatio = 0.42;

    public MainWindow()
    {
        InitializeComponent();
        _settingsSaveQueue = new DebouncedAsyncAction(
            TimeSpan.FromMilliseconds(400),
            cancellationToken => _settingsService.SaveAsync(_settings, cancellationToken));
        BuildEqBandControls();
        BuildOutputMeterBars();
        _packsRoot = Path.Combine(AppContext.BaseDirectory, "assets", "packs");
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        _keyboardHook.KeyPressed += KeyboardHook_KeyPressed;
        _activeAppTimer.Tick += (_, _) => RefreshCurrentApp();
        _outputMeterTimer.Tick += (_, _) => RefreshOutputMeter();
        _libraryScrollTimer.Tick += LibraryScrollTimer_Tick;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        await _settingsService.SaveAsync(_settings);
        _settings.StartWithWindows = _startup.IsEnabled();
        RebuildPlaybackProfile();
        ConfigureAppRuleEditors();
        ConfigurePanControls();
        ConfigureEnterDingControls();
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
        _outputMeterTimer.Start();
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
        if (!e.IsRelease && ShouldDebounceKeyPress(e, profile))
        {
            return;
        }

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

        RefreshPackLibrary(AppSettings.DefaultSoundPackId);

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

    private void PacksList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        _packsScrollViewer ??= FindVisualChild<ScrollViewer>(PacksList);
        if (_packsScrollViewer is null)
        {
            return;
        }

        double maxOffset = Math.Max(0, _packsScrollViewer.ScrollableHeight);
        double baseOffset = _libraryScrollTimer.IsEnabled
            ? _libraryScrollTarget
            : _packsScrollViewer.VerticalOffset;
        _libraryScrollTarget = Math.Clamp(baseOffset - (e.Delta * LibraryScrollWheelScale), 0, maxOffset);
        double immediateOffset = _packsScrollViewer.VerticalOffset + ((_libraryScrollTarget - _packsScrollViewer.VerticalOffset) * LibraryScrollImmediateRatio);
        _packsScrollViewer.ScrollToVerticalOffset(immediateOffset);
        _libraryScrollTimer.Start();
        e.Handled = true;
    }

    private void LibraryScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (_packsScrollViewer is null)
        {
            _libraryScrollTimer.Stop();
            return;
        }

        double current = _packsScrollViewer.VerticalOffset;
        double next = current + ((_libraryScrollTarget - current) * LibraryScrollEaseRatio);
        if (Math.Abs(_libraryScrollTarget - next) < 0.35)
        {
            next = _libraryScrollTarget;
            _libraryScrollTimer.Stop();
        }

        _packsScrollViewer.ScrollToVerticalOffset(next);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            T? descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
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

        string soundGroup = ResolveEnterDingSoundGroup(_settings.EnterDingSoundGroup);
        _audio?.TryPlay(new PlaybackRequest
        {
            Key = key,
            SoundGroup = soundGroup,
            SoundPackId = EnterDingPackId,
            VolumeMultiplier = Math.Clamp(volumeMultiplier * _settings.EnterDingVolume, 0.0, 1.0),
            ActiveProcessName = processName,
            BypassSoundShaping = true,
            MinimumPlaybackInterval = EnterDingMinimumInterval,
            ThrottleKey = $"{EnterDingPackId}:{soundGroup}"
        });
    }

    private static string ResolveEnterDingSoundGroup(string? soundGroup) =>
        string.IsNullOrWhiteSpace(soundGroup) || soundGroup.Equals("random", StringComparison.OrdinalIgnoreCase)
            ? "enter"
            : soundGroup;

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
            RuleModeComboBox.Items.Add(new AppRuleModeOption(AppRuleMode.Disabled, "Disabled"));
            RuleModeComboBox.Items.Add(new AppRuleModeOption(AppRuleMode.Default, "Default"));
            RuleModeComboBox.Items.Add(new AppRuleModeOption(AppRuleMode.EnabledOnly, "Enabled Only"));
            RuleModeComboBox.Items.Add(new AppRuleModeOption(AppRuleMode.UseSpecificPack, "Use Specific Pack"));
            SelectRuleMode(AppRuleMode.Disabled);
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

    private void ConfigureEnterDingControls()
    {
        EnterDingSoundComboBox.Items.Clear();
        foreach (EnterDingSoundListItem item in EnterDingSounds)
        {
            EnterDingSoundComboBox.Items.Add(item);
        }
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

    private void BuildOutputMeterBars()
    {
        _leftOutputMeterBars.Clear();
        _rightOutputMeterBars.Clear();
        foreach (ShapeRectangle bar in LeftOutputMeterBars.Children.OfType<ShapeRectangle>())
        {
            _leftOutputMeterBars.Add(bar);
        }

        foreach (ShapeRectangle bar in RightOutputMeterBars.Children.OfType<ShapeRectangle>())
        {
            _rightOutputMeterBars.Add(bar);
        }

        RefreshOutputMeter();
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
        if (_settings.MasterVolume > 0.98)
        {
            _settings.MasterVolume = 0.72;
        }

        if (_settings.GroupVolumes.Normal > 0.98 &&
            _settings.GroupVolumes.Enter > 0.98 &&
            _settings.GroupVolumes.Space > 0.98 &&
            _settings.GroupVolumes.Backspace > 0.98 &&
            _settings.GroupVolumes.Tab > 0.98)
        {
            _settings.GroupVolumes.Normal = 0.72;
            _settings.GroupVolumes.Enter = 0.80;
            _settings.GroupVolumes.Space = 0.65;
            _settings.GroupVolumes.Backspace = 0.70;
            _settings.GroupVolumes.Tab = 0.60;
        }

        _settings.PitchVariation = 0.0;
        _settings.StartHiddenInTray = true;
        _settings.Pan.Normalize();

        EnabledToggle.IsChecked = _settings.Enabled;
        MasterVolumeSlider.Value = _settings.MasterVolume;
        PitchVariationSlider.Value = Math.Round(_settings.PitchVariation * 100);
        KeyDebounceSlider.Value = _settings.KeyDebounceMilliseconds;
        IgnoreRepeatsCheck.IsChecked = _settings.IgnoreKeyRepeats;
        EnterDingEnabledCheck.IsChecked = _settings.EnterDingEnabled;
        EnterDingSoundComboBox.SelectedItem = EnterDingSoundComboBox.Items
            .OfType<EnterDingSoundListItem>()
            .FirstOrDefault(item => item.SoundGroup.Equals(_settings.EnterDingSoundGroup, StringComparison.OrdinalIgnoreCase))
            ?? EnterDingSoundComboBox.Items.OfType<EnterDingSoundListItem>().FirstOrDefault();
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
        RefreshHotkeySettingsText();
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
        string? selectedProcess = AppRulesList.SelectedItem is AppRuleListItem selectedItem
            ? selectedItem.ProcessName
            : null;
        IReadOnlyList<AppRule> allRules = GetAppRuleDisplayRules();
        IReadOnlyList<AppRule> displayRules = FilterAppRules(allRules);
        AppRulesList.Items.Clear();
        foreach (AppRule rule in displayRules)
        {
            AppRulesList.Items.Add(new AppRuleListItem(rule, _packsById));
        }

        int activeRules = allRules.Count(rule => rule.Mode != AppRuleMode.Disabled);
        RuleCountText.Text = allRules.Count.ToString();
        ActiveRulesText.Text = $"{activeRules} active";
        AppRuleSelectionText.Text = string.IsNullOrWhiteSpace(GetRuleSearchText())
            ? FormatRuleCount(allRules.Count)
            : $"{displayRules.Count} of {FormatRuleCount(allRules.Count)}";
        bool showEmptyState = displayRules.Count == 0;
        AppRulesList.Visibility = showEmptyState ? Visibility.Collapsed : Visibility.Visible;
        AppRulesEmptyState.Visibility = showEmptyState ? Visibility.Visible : Visibility.Collapsed;
        if (showEmptyState)
        {
            bool hasRules = allRules.Count > 0;
            AppRulesEmptyTitleText.Text = hasRules ? "No matching rules" : "No app rules yet";
            AppRulesEmptyDescriptionText.Text = hasRules
                ? "Clear the search field or try a different process, mode, or pack name."
                : "Create a rule for the current foreground app or type a process name in the editor.";
        }

        if (RuleSearchPlaceholderText is not null)
        {
            RuleSearchPlaceholderText.Visibility = string.IsNullOrWhiteSpace(GetRuleSearchText())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        DefaultProfileText.Text = _activePack?.Name ?? "No pack selected";
        DefaultProfileTagText.Text = _activePack is null
            ? "Default"
            : new PackListItem(_activePack).TypeLabel;
        DefaultProfileImage.Source = _activePack is null ? null : CreatePackPreviewImageSource(_activePack);

        AppRulesList.SelectedItem = AppRulesList.Items
            .OfType<AppRuleListItem>()
            .FirstOrDefault(item => item.ProcessName.Equals(selectedProcess, StringComparison.OrdinalIgnoreCase));

        if (AppRulesList.SelectedItem is AppRuleListItem selected)
        {
            RuleEditorProcessText.Text = selected.ProcessName;
            ApplyRuleEditorIcon(selected.ProcessName);
        }
        else if (string.IsNullOrWhiteSpace(ProcessRuleTextBox.Text))
        {
            RuleEditorProcessText.Text = "New rule";
            ApplyRuleEditorIcon("");
        }
    }

    private bool ShouldDebounceKeyPress(KeyPressedEvent e, RuntimePlaybackProfile profile)
    {
        int debounceMilliseconds = profile.KeyDebounceMilliseconds;
        if (debounceMilliseconds <= 0)
        {
            _lastAcceptedKeyDownByCode[e.Key.Code] = e.Timestamp;
            return false;
        }

        if (_lastAcceptedKeyDownByCode.TryGetValue(e.Key.Code, out DateTimeOffset previous) &&
            e.Timestamp - previous < TimeSpan.FromMilliseconds(debounceMilliseconds))
        {
            return true;
        }

        _lastAcceptedKeyDownByCode[e.Key.Code] = e.Timestamp;
        return false;
    }

    private IReadOnlyList<AppRule> GetAppRuleDisplayRules() =>
        _settings.AppRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ProcessName))
            .GroupBy(rule => rule.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(rule => rule.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeRuleProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        string normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized}.exe";
    }

    private IReadOnlyList<AppRule> FilterAppRules(IReadOnlyList<AppRule> rules)
    {
        string query = GetRuleSearchText();
        if (string.IsNullOrWhiteSpace(query))
        {
            return rules;
        }

        return rules
            .Where(rule =>
                rule.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                rule.Mode.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(rule.SoundPackId) && ResolvePackDisplayName(rule.SoundPackId).Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private string GetRuleSearchText() =>
        RuleSearchTextBox is null ? string.Empty : RuleSearchTextBox.Text.Trim();

    private string ResolvePackDisplayName(string packId) =>
        _packsById.TryGetValue(packId, out SoundPackMetadata? pack) ? pack.Name : packId;

    private static string FormatRuleCount(int count) =>
        count == 1 ? "1 rule total" : $"{count} rules total";

    private void RefreshCurrentApp()
    {
        string? processName = _activeWindow.GetActiveProcessName();
        if (IsTrackableRecentProcess(processName))
        {
            _currentProcessName = processName;
            _lastDetectedProcessName = processName;
            if (!string.Equals(processName, _lastRecordedProcessName, StringComparison.OrdinalIgnoreCase))
            {
                _lastRecordedProcessName = processName;
                _recentApps.Record(processName);
                RefreshRecentApps();
            }
        }
        else
        {
            _currentProcessName = _lastDetectedProcessName;
        }

        string displayName = ResolveRulesPageForegroundDisplay(_currentProcessName);
        AppVisual currentAppVisual = AppVisual.ForProcess(displayName);
        CurrentAppIconText.Text = currentAppVisual.IconText;
        CurrentAppIconText.FontFamily = currentAppVisual.IconFontFamily;
        CurrentAppIconText.Foreground = currentAppVisual.IconForeground;
        CurrentAppIconImage.Source = currentAppVisual.IconSource;
        LastDetectedIconText.Text = currentAppVisual.IconText;
        LastDetectedIconText.FontFamily = currentAppVisual.IconFontFamily;
        LastDetectedIconText.Foreground = currentAppVisual.IconForeground;
        LastDetectedIconImage.Source = currentAppVisual.IconSource;
        CurrentAppText.Text = displayName;
        CurrentAppStatusText.Text = displayName.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? "Waiting for focus" : "Active now";
        LastDetectedAppText.Text = displayName;
        LastDetectedTimeText.Text = displayName.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? "No app detected" : "Updated just now";
        if (string.IsNullOrWhiteSpace(ProcessRuleTextBox.Text) && !displayName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            ProcessRuleTextBox.Text = displayName;
            RuleEditorProcessText.Text = displayName;
            ApplyRuleEditorIcon(displayName);
        }
    }

    private static string ResolveRulesPageForegroundDisplay(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName) ||
            processName.Equals("SoundType.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        return processName;
    }

    private void RefreshStatus()
    {
        bool degraded = _settings.Enabled && _startupWarnings.Count > 0;
        StatusText.Text = !_settings.Enabled
            ? "Muted"
            : degraded
                ? "Needs attention"
                : "Active";
        StatusDot.Fill = (MediaBrush)FindResource(!_settings.Enabled || degraded ? "DangerBrush" : "AccentBrush");
        UpdateEnableButton();
        VolumeText.Text = $"{Math.Round(_settings.MasterVolume * 100)}%";
        PitchVariationText.Text = $"{Math.Round(PitchVariationSlider.Value)} st";
        RefreshKeyDebounceText();
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
        RefreshTrayStartupIcons();
    }

    private void RefreshStartupStatus()
    {
        StartupStatusText.Foreground = (MediaBrush)FindResource("AccentHoverBrush");
        StartupStatusText.Text = "Enabled";
        RefreshTrayStartupIcons();
    }

    private void RefreshTrayStartupIcons()
    {
        AnimateIconBrush(MinimizeToTrayIconPath, _settings.MinimizeToTray);
        AnimateIconBrush(HideToTrayIconPath, _settings.MinimizeToTray);
        AnimateIconBrush(StartWithWindowsIconRing, _settings.StartWithWindows);
        AnimateIconBrush(StartWithWindowsIconPath, _settings.StartWithWindows);
        AnimateIconBrush(StartHiddenInTrayIconRing, _settings.StartHiddenInTray);
        AnimateIconBrush(StartHiddenInTrayIconPath, _settings.StartHiddenInTray);
    }

    private static void AnimateIconBrush(System.Windows.Shapes.Shape icon, bool enabled)
    {
        MediaColor target = enabled
            ? MediaColor.FromRgb(82, 226, 168)
            : MediaColor.FromRgb(167, 176, 184);
        MediaColor start = icon.Stroke is SolidColorBrush brush
            ? brush.Color
            : target;

        SolidColorBrush animatedBrush = new(start);
        icon.Stroke = animatedBrush;
        ColorAnimation animation = new(target, TimeSpan.FromMilliseconds(enabled ? 260 : 170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private void RefreshSettingsOverview()
    {
        if (SettingsActivePackNameText is null)
        {
            return;
        }

        SoundPackMetadata? settingsDisplayPack = _packs.FirstOrDefault(pack =>
            pack.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase) ||
            pack.Name.Equals("Alpaca Switches", StringComparison.OrdinalIgnoreCase))
            ?? _activePack;

        SettingsActivePackNameText.Text = settingsDisplayPack?.Name ?? "Alpaca Switches";
        if (SettingsActivePackTypeText is not null)
        {
            SettingsActivePackTypeText.Text = "Mechanical";
        }

        SettingsActivePackSizeText.Text = "5.2 MB";
        SettingsActivePackPreviewImage.Source = settingsDisplayPack is null ? null : CreatePackPreviewImageSource(settingsDisplayPack);
        PacksFolderPathText.Text = _selectedPacksFolder ?? _packsRoot;
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

        bool wasVisible = activePage.Visibility == Visibility.Visible;
        foreach (FrameworkElement page in pages)
        {
            page.Visibility = ReferenceEquals(page, activePage) ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateNavigationState(activePage);
        UpdateShellLayout(activePage);
        UpdateHeaderText(activePage);
        if (!wasVisible)
        {
            AnimatePageEntrance(activePage);
            AnimateHeaderEntrance();
        }
    }

    private void UpdateShellLayout(FrameworkElement activePage)
    {
        Sidebar.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(320);
        Grid.SetColumn(PageHost, 1);
        Grid.SetColumnSpan(PageHost, 1);
    }

    private void AnimatePageEntrance(FrameworkElement page)
    {
        page.BeginAnimation(OpacityProperty, null);
        page.RenderTransform = new TranslateTransform(0, 10);
        page.Opacity = 0;

        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        page.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(190),
            EasingFunction = ease
        });

        if (page.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            });
        }
    }

    private void AnimateHeaderEntrance()
    {
        AnimateHeaderElement(PageTitleText, 0);
        AnimateHeaderElement(PageSubtitleText, 35);

        foreach (FrameworkElement actionGroup in new[] { LibraryHeaderActions, AudioHeaderActions, KeyboardHeaderActions, RulesHeaderActions, SettingsHeaderActions })
        {
            if (actionGroup.Visibility == Visibility.Visible)
            {
                AnimateHeaderElement(actionGroup, 55);
            }
        }
    }

    private static void AnimateHeaderElement(FrameworkElement element, int delayMs)
    {
        element.BeginAnimation(OpacityProperty, null);
        element.RenderTransform = new TranslateTransform(0, 4);
        element.Opacity = 0;

        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = ease
        });

        if (element.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 4,
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = ease
            });
        }
    }

    private void AnimatedCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        ScaleTransform scale = EnsureCardScaleTransform(border);
        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(CardHoverScale, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(CardHoverScale, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease });
    }

    private void AnimatedCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        ScaleTransform scale = EnsureCardScaleTransform(border);
        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(CardRestScale, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(CardRestScale, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease });
    }

    private static ScaleTransform EnsureCardScaleTransform(Border border)
    {
        if (border.RenderTransform is ScaleTransform scale && !scale.IsFrozen)
        {
            return scale;
        }

        double scaleX = border.RenderTransform is ScaleTransform existing ? existing.ScaleX : CardRestScale;
        double scaleY = border.RenderTransform is ScaleTransform existingScale ? existingScale.ScaleY : CardRestScale;
        ScaleTransform localScale = new(scaleX, scaleY);
        border.RenderTransform = localScale;
        return localScale;
    }

    private void UpdateHeaderText(FrameworkElement activePage)
    {
        bool isKeyboardPage = ReferenceEquals(activePage, KeyboardPage);
        bool isRulesPage = ReferenceEquals(activePage, RulesPage);
        bool isAudioPage = ReferenceEquals(activePage, AudioPage);
        bool isSettingsPage = ReferenceEquals(activePage, SettingsPage);
        foreach (FrameworkElement actionGroup in new[] { LibraryHeaderActions, AudioHeaderActions, KeyboardHeaderActions, RulesHeaderActions, SettingsHeaderActions })
        {
            actionGroup.Visibility = Visibility.Collapsed;
            actionGroup.Opacity = 1;
        }
        LibraryViewTabs.Visibility = Visibility.Collapsed;
        PageSubtitleText.Visibility = Visibility.Visible;

        if (ReferenceEquals(activePage, LibraryPage))
        {
            LibraryHeaderActions.Visibility = Visibility.Visible;
        }
        else if (isAudioPage)
        {
            AudioHeaderActions.Visibility = Visibility.Visible;
        }
        else if (isKeyboardPage)
        {
            KeyboardHeaderActions.Visibility = Visibility.Visible;
        }
        else if (isRulesPage)
        {
            RulesHeaderActions.Visibility = Visibility.Visible;
        }
        else if (isSettingsPage)
        {
            SettingsHeaderActions.Visibility = Visibility.Visible;
        }

        if (ReferenceEquals(activePage, LibraryPage))
        {
            PageTitleText.Text = "Library";
            PageSubtitleText.Visibility = Visibility.Collapsed;
            LibraryViewTabs.Visibility = Visibility.Visible;
            RefreshLibraryViewButtons();
            return;
        }

        if (isAudioPage)
        {
            PageTitleText.Text = "Audio Effects";
            PageSubtitleText.Text = "Tune playback";
            PageSubtitleText.Margin = new Thickness(28, 6, 0, 0);
            return;
        }

        if (isKeyboardPage)
        {
            PageTitleText.Text = "Keyboard Rules";
            PageSubtitleText.Text = "Choose which keys make sound.";
            PageSubtitleText.Margin = new Thickness(28, 6, 0, 0);
            return;
        }

        if (isRulesPage)
        {
            PageTitleText.Text = "App Rules";
            PageSubtitleText.Text = "Per-app sound profiles";
            PageSubtitleText.Margin = new Thickness(28, 6, 0, 0);
            return;
        }

        PageTitleText.Text = "Settings";
        PageSubtitleText.Text = "Preferences and privacy";
        PageSubtitleText.Margin = new Thickness(28, 6, 0, 0);
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
            string foregroundKey = selected && ReferenceEquals(activePage, AudioPage)
                ? "AccentHoverBrush"
                : selected
                    ? "TextBrush"
                    : "MutedTextBrush";
            button.Foreground = (MediaBrush)FindResource(foregroundKey);
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
            AddStartupWarning("Invalid global hotkey. Using Ctrl+Alt+L.");
            _ = SaveSettingsAsync();
        }

        IntPtr windowHandle = new WindowInteropHelper(this).Handle;
        if (_hotkeySource is null)
        {
            _hotkeySource = HwndSource.FromHwnd(windowHandle);
            _hotkeySource?.AddHook(WndProc);
        }

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

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_recordingHotkeyTarget is not null)
        {
            CaptureHotkeyFromKeyDown(e);
            return;
        }

        if (IsInteractiveElement(Keyboard.FocusedElement as DependencyObject))
        {
            return;
        }

        if (MatchesHotkey(_settings.PreviewNormalHotkey, e))
        {
            PreviewPackGroup("normal");
            e.Handled = true;
            return;
        }

        if (MatchesHotkey(_settings.NextPackHotkey, e))
        {
            _ = ActivateAdjacentPackAsync(1);
            e.Handled = true;
            return;
        }

        if (MatchesHotkey(_settings.PreviousPackHotkey, e))
        {
            _ = ActivateAdjacentPackAsync(-1);
            e.Handled = true;
        }
    }

    private async Task ActivateAdjacentPackAsync(int direction)
    {
        if (_packs.Count == 0)
        {
            return;
        }

        string? currentId = _activePack?.Id ?? (PacksList.SelectedItem as PackListItem)?.Metadata.Id;
        int currentIndex = -1;
        for (int i = 0; i < _packs.Count; i++)
        {
            if (_packs[i].Id.Equals(currentId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }
        int nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + direction + _packs.Count) % _packs.Count;
        SoundPackMetadata nextPack = _packs[nextIndex];

        PackListItem? visibleItem = PacksList.Items
            .OfType<PackListItem>()
            .FirstOrDefault(item => item.Metadata.Id.Equals(nextPack.Id, StringComparison.OrdinalIgnoreCase));
        if (visibleItem is not null)
        {
            PacksList.SelectedItem = visibleItem;
            PacksList.ScrollIntoView(visibleItem);
        }

        await ActivatePackAsync(nextPack);
        RefreshSelectedPackDetails(nextPack);
    }

    private void StartHotkeyRecording_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string target)
        {
            return;
        }

        _recordingHotkeyTarget = target;
        RefreshHotkeySettingsText(recordingTarget: target);
        button.Content = "Press keys";
        Focus();
    }

    private void CaptureHotkeyFromKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        Key key = NormalizeShortcutKey(e);
        if (key == Key.Escape)
        {
            _recordingHotkeyTarget = null;
            RefreshHotkeySettingsText();
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        string shortcutText = FormatShortcut(modifiers, key);
        if (_recordingHotkeyTarget == HotkeyTargetToggleListening &&
            !HasShortcutModifier(modifiers))
        {
            ToggleListeningHotkeyText.Text = "Add Ctrl / Alt / Shift";
            return;
        }

        switch (_recordingHotkeyTarget)
        {
            case HotkeyTargetToggleListening:
                _settings.GlobalToggleHotkey = shortcutText;
                RegisterGlobalHotkey();
                break;
            case HotkeyTargetPreviewNormal:
                _settings.PreviewNormalHotkey = shortcutText;
                break;
            case HotkeyTargetNextPack:
                _settings.NextPackHotkey = shortcutText;
                break;
            case HotkeyTargetPreviousPack:
                _settings.PreviousPackHotkey = shortcutText;
                break;
        }

        _recordingHotkeyTarget = null;
        RefreshHotkeySettingsText();
        _ = SaveSettingsAsync();
    }

    private void RestoreHotkeys_Click(object sender, RoutedEventArgs e)
    {
        _recordingHotkeyTarget = null;
        _settings.GlobalToggleHotkey = HotkeyGesture.DefaultText;
        _settings.PreviewNormalHotkey = "Space";
        _settings.NextPackHotkey = "Ctrl+Alt+Right";
        _settings.PreviousPackHotkey = "Ctrl+Alt+Left";
        RegisterGlobalHotkey();
        RefreshHotkeySettingsText();
        _ = SaveSettingsAsync();
    }

    private void RefreshHotkeySettingsText(string? recordingTarget = null)
    {
        if (ToggleListeningHotkeyText is null)
        {
            return;
        }

        ToggleListeningHotkeyText.Text = recordingTarget == HotkeyTargetToggleListening ? "Press shortcut" : NormalizeShortcutText(_settings.GlobalToggleHotkey, HotkeyGesture.DefaultText);
        PreviewNormalHotkeyText.Text = recordingTarget == HotkeyTargetPreviewNormal ? "Press shortcut" : NormalizeShortcutText(_settings.PreviewNormalHotkey, "Space");
        NextPackHotkeyText.Text = recordingTarget == HotkeyTargetNextPack ? "Press shortcut" : NormalizeShortcutText(_settings.NextPackHotkey, "Ctrl+Alt+Right");
        PreviousPackHotkeyText.Text = recordingTarget == HotkeyTargetPreviousPack ? "Press shortcut" : NormalizeShortcutText(_settings.PreviousPackHotkey, "Ctrl+Alt+Left");

        ToggleListeningHotkeyRecordButton.Content = recordingTarget == HotkeyTargetToggleListening ? "Listening" : "Record";
        PreviewNormalHotkeyRecordButton.Content = recordingTarget == HotkeyTargetPreviewNormal ? "Listening" : "Record";
        NextPackHotkeyRecordButton.Content = recordingTarget == HotkeyTargetNextPack ? "Listening" : "Record";
        PreviousPackHotkeyRecordButton.Content = recordingTarget == HotkeyTargetPreviousPack ? "Listening" : "Record";
    }

    private static string NormalizeShortcutText(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool MatchesHotkey(string? shortcutText, System.Windows.Input.KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(shortcutText))
        {
            return false;
        }

        Key key = NormalizeShortcutKey(e);
        if (IsModifierKey(key))
        {
            return false;
        }

        return string.Equals(FormatShortcut(Keyboard.Modifiers, key), shortcutText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatShortcut(ModifierKeys modifiers, Key key)
    {
        List<string> parts = [];
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(FormatKeyName(key));
        return string.Join("+", parts);
    }

    private static string FormatKeyName(Key key) =>
        key switch
        {
            Key.Return => "Enter",
            Key.Prior => "PageUp",
            Key.Next => "PageDown",
            Key.Back => "Backspace",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            _ => new KeyConverter().ConvertToInvariantString(key) ?? key.ToString()
        };

    private static Key NormalizeShortcutKey(System.Windows.Input.KeyEventArgs e) =>
        e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };

    private static bool HasShortcutModifier(ModifierKeys modifiers) =>
        (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows)) != ModifierKeys.None;

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System;

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
        _settings.PitchVariation = Math.Clamp(Math.Abs(PitchVariationSlider.Value) / 100.0, 0.0, 0.12);
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

    private void BrowsePacksViewButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetLibraryFavoritesView(false);
    }

    private void FavoritePacksViewButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetLibraryFavoritesView(true);
    }

    private void SetLibraryFavoritesView(bool showFavorites)
    {
        if (_showingFavoritePacks == showFavorites)
        {
            RefreshPackLibrary();
            return;
        }

        _showingFavoritePacks = showFavorites;
        RefreshLibraryViewButtons();
        RefreshPackLibrary();
    }

    private void SelectedPackFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (PacksList.SelectedItem is not PackListItem item)
        {
            return;
        }

        ToggleFavoritePack(item.Metadata);
    }

    private void PackRowFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button button || button.Tag is not PackListItem item)
        {
            return;
        }

        ToggleFavoritePack(item.Metadata);
    }

    private void ToggleFavoritePack(SoundPackMetadata pack)
    {
        if (_settings.FavoriteSoundPackIds.Contains(pack.Id))
        {
            _settings.FavoriteSoundPackIds.Remove(pack.Id);
        }
        else
        {
            _settings.FavoriteSoundPackIds.Add(pack.Id);
        }

        string? selectedPackId = (PacksList.SelectedItem as PackListItem)?.Metadata.Id;
        if (selectedPackId is not null && selectedPackId.Equals(pack.Id, StringComparison.OrdinalIgnoreCase))
        {
            RefreshSelectedPackFavoriteButton(pack);
        }

        RefreshPackLibrary(selectedPackId);
        _ = SaveSettingsAsync();
    }

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

    private void OpenContextMenu(System.Windows.Controls.ContextMenu menu, FrameworkElement? placementTarget)
    {
        menu.Style = (Style)FindResource(typeof(System.Windows.Controls.ContextMenu));
        foreach (object item in menu.Items)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.Style = (Style)FindResource(typeof(MenuItem));
            }
            else if (item is Separator separator)
            {
                separator.Style = (Style)FindResource(typeof(Separator));
            }
        }

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
            PackValidationText.Text = BuildImportResultMessage("Imported", metadata);
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
                PackValidationText.Text = BuildImportResultMessage("Replaced", metadata);
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

    private async void ImportAppRules_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Filter = "SoundType app rules (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await using FileStream stream = File.OpenRead(dialog.FileName);
            List<AppRule>? importedRules = await JsonSerializer.DeserializeAsync<List<AppRule>>(stream);
            if (importedRules is null)
            {
                throw new InvalidDataException("The selected file does not contain app rules.");
            }

            int importedCount = MergeAppRules(importedRules);
            RefreshAppRules();
            await SaveSettingsAsync();
            System.Windows.MessageBox.Show(
                this,
                $"Imported {FormatRuleCount(importedCount).Replace(" total", "", StringComparison.Ordinal)}.",
                "Import app rules",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Could not import app rules: {ex.Message}",
                "Import app rules",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportAppRules_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<AppRule> rules = GetAppRuleDisplayRules();
        if (rules.Count == 0)
        {
            System.Windows.MessageBox.Show(
                this,
                "There are no app rules to export.",
                "Export app rules",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Filter = "SoundType app rules (*.json)|*.json",
            FileName = "soundtype-app-rules.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(rules, options));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Could not export app rules: {ex.Message}",
                "Export app rules",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private int MergeAppRules(IEnumerable<AppRule> importedRules)
    {
        int importedCount = 0;
        foreach (AppRule importedRule in importedRules)
        {
            string processName = NormalizeRuleProcessName(importedRule.ProcessName);
            if (string.IsNullOrWhiteSpace(processName))
            {
                continue;
            }

            AppRuleMode mode = Enum.IsDefined(importedRule.Mode) ? importedRule.Mode : AppRuleMode.Disabled;
            string? soundPackId = mode == AppRuleMode.UseSpecificPack && !string.IsNullOrWhiteSpace(importedRule.SoundPackId)
                ? importedRule.SoundPackId
                : null;
            double? volumeOverride = importedRule.VolumeOverride is double volume
                ? Math.Clamp(volume, 0.0, 1.5)
                : null;

            AppRule? existing = _settings.AppRules.FirstOrDefault(rule =>
                rule.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _settings.AppRules.Add(new AppRule
                {
                    ProcessName = processName,
                    Mode = mode,
                    SoundPackId = soundPackId,
                    VolumeOverride = volumeOverride
                });
            }
            else
            {
                existing.Mode = mode;
                existing.SoundPackId = soundPackId;
                existing.VolumeOverride = volumeOverride;
            }

            importedCount++;
        }

        return importedCount;
    }

    private string BuildImportResultMessage(string action, SoundPackMetadata metadata)
    {
        SoundPackValidationResult validation = _packLoader.Validate(metadata, analyzeAudioQuality: true);
        return validation.Warnings.Count == 0
            ? $"{action} {metadata.Name}."
            : $"{action} {metadata.Name}. Warning: {validation.Warnings[0]}";
    }

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
        OpenFolder(_selectedPacksFolder ?? _activePack?.FolderPath ?? _packsRoot);
    }

    private void BrowsePacksFolder_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Choose the packs folder",
            SelectedPath = Directory.Exists(_selectedPacksFolder) ? _selectedPacksFolder : _packsRoot,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        _selectedPacksFolder = dialog.SelectedPath;
        PacksFolderPathText.Text = _selectedPacksFolder;
    }

    private void OpenPackWaveformLocation_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenFolder(ResolvePackWaveformLocation() ?? _activePack?.FolderPath ?? _packsRoot);
    }

    private string? ResolvePackWaveformLocation()
    {
        if (_activePack is null ||
            _audio is null ||
            !_audio.TryGetLoadedPack(_activePack.Id, out LoadedSoundPack? loadedPack) ||
            loadedPack is null)
        {
            return null;
        }

        LoadedSoundSample? sample = loadedPack.Samples
            .OrderBy(group => group.Key.Equals("normal", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .SelectMany(group => group.Value)
            .FirstOrDefault(candidate => candidate.DecodedSamples.Length > 0);
        if (sample is null || string.IsNullOrWhiteSpace(sample.RelativePath))
        {
            return null;
        }

        string samplePath = Path.GetFullPath(Path.Combine(_activePack.FolderPath, sample.RelativePath));
        string packFolder = Path.GetFullPath(_activePack.FolderPath);
        if (!samplePath.StartsWith(EnsureTrailingSeparator(packFolder), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetDirectoryName(samplePath);
    }

    private static void OpenFolder(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

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
        if (KeyboardSearchPlaceholder is not null)
        {
            KeyboardSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(KeyboardSearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        ApplyKeyboardFilter();
    }

    private void KeyboardSearchClear_Click(object sender, RoutedEventArgs e)
    {
        KeyboardSearchTextBox.Clear();
        KeyboardSearchTextBox.Focus();
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

    private AppRuleMode GetSelectedRuleMode() =>
        RuleModeComboBox.SelectedItem is AppRuleModeOption option
            ? option.Mode
            : AppRuleMode.Disabled;

    private void SelectRuleMode(AppRuleMode mode)
    {
        RuleModeComboBox.SelectedItem = RuleModeComboBox.Items
            .OfType<AppRuleModeOption>()
            .FirstOrDefault(option => option.Mode == mode);
    }

    private void AddAppRule_Click(object sender, RoutedEventArgs e)
    {
        string processName = NormalizeRuleProcessName(ProcessRuleTextBox.Text);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        AppRuleMode mode = GetSelectedRuleMode();
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
        ApplyRuleEditorIcon(processName);
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

    private void ApplyRuleEditorIcon(string processName)
    {
        if (RuleEditorIconText is null)
        {
            return;
        }

        AppVisual visual = AppVisual.ForProcess(processName);
        RuleEditorIconText.Text = visual.IconText;
        RuleEditorIconText.FontFamily = visual.IconFontFamily;
        RuleEditorIconText.Foreground = visual.IconForeground;
        RuleEditorIconImage.Source = visual.IconSource;
    }

    private void RefreshRecentApps()
    {
        if (RecentAppsList is null || RecentAppActivityList is null ||
            RecentAppActivityLaneTop is null || RecentAppActivityLaneBottom is null)
        {
            return;
        }

        string? selected = RecentAppsList.SelectedItem is RecentAppChipItem selectedChip
            ? selectedChip.ProcessName
            : null;
        IReadOnlyList<RecentAppEntry> recentApps = _recentApps.ListRecentApps()
            .Where(app => IsTrackableRecentProcess(app.ProcessName))
            .ToList();

        RecentAppsList.Items.Clear();
        foreach (RecentAppEntry app in recentApps.Take(4))
        {
            RecentAppsList.Items.Add(new RecentAppChipItem(app.ProcessName));
        }

        if (selected is not null)
        {
            RecentAppsList.SelectedItem = RecentAppsList.Items
                .OfType<RecentAppChipItem>()
                .FirstOrDefault(item => item.ProcessName.Equals(selected, StringComparison.OrdinalIgnoreCase));
        }

        RecentAppActivityList.Items.Clear();
        foreach (RecentAppEntry app in recentApps.Take(4))
        {
            RecentAppActivityList.Items.Add(new RecentAppActivityItem(
                app.ProcessName,
                FormatLastSeen(app.LastSeenUtc),
                GetRecentAppColor(app.ProcessName)));
        }

        RefreshRecentAppActivityTimeline(_recentApps.ListSwitchEvents(RecentAppActivityWindow));
    }

    private void RefreshRecentAppActivityTimeline(IReadOnlyList<RecentAppSwitchEvent> events)
    {
        List<MediaColor?> topLane = Enumerable.Repeat<MediaColor?>(null, RecentAppActivitySlots).ToList();
        List<MediaColor?> bottomLane = Enumerable.Repeat<MediaColor?>(null, RecentAppActivitySlots).ToList();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

        List<RecentAppSwitchEvent> trackableEvents = events
            .Where(app => IsTrackableRecentProcess(app.ProcessName))
            .ToList();

        foreach (RecentAppSwitchEvent appEvent in trackableEvents)
        {
            double ageRatio = Math.Clamp(
                (nowUtc - appEvent.SeenUtc).TotalMilliseconds / RecentAppActivityWindow.TotalMilliseconds,
                0,
                0.999);
            int slot = Math.Clamp(RecentAppActivitySlots - 1 - (int)Math.Floor(ageRatio * RecentAppActivitySlots), 0, RecentAppActivitySlots - 1);
            if (appEvent.Lane == 0)
            {
                topLane[slot] = GetRecentAppColor(appEvent.ProcessName);
            }
            else
            {
                bottomLane[slot] = GetRecentAppColor(appEvent.ProcessName);
            }
        }

        PopulateRecentAppActivityLane(RecentAppActivityLaneTop, topLane);
        PopulateRecentAppActivityLane(RecentAppActivityLaneBottom, bottomLane);
        RecentAppActivityEmptyText.Visibility = trackableEvents.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static void PopulateRecentAppActivityLane(UniformGrid lane, IReadOnlyList<MediaColor?> colors)
    {
        lane.Children.Clear();
        lane.Columns = RecentAppActivitySlots;
        foreach (MediaColor? color in colors)
        {
            lane.Children.Add(new ShapeRectangle
            {
                Width = 3,
                RadiusX = 2,
                RadiusY = 2,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Fill = color is MediaColor value
                    ? new SolidColorBrush(value)
                    : System.Windows.Media.Brushes.Transparent
            });
        }
    }

    private void RecentAppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentAppsList.SelectedItem is RecentAppChipItem { ProcessName: string processName })
        {
            ProcessRuleTextBox.Text = processName;
            RuleEditorProcessText.Text = processName;
            ApplyRuleEditorIcon(processName);
        }
    }

    private void RuleSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AppRulesList is null)
        {
            return;
        }

        RefreshAppRules();
    }

    private static string FormatRecentAppSummary(RecentAppEntry app)
    {
        return $"{app.ProcessName}    {FormatLastSeen(app.LastSeenUtc)}";
    }

    private static string FormatLastSeen(DateTimeOffset lastSeenUtc)
    {
        TimeSpan age = DateTimeOffset.UtcNow - lastSeenUtc;
        return age.TotalSeconds < 30
            ? "Now"
            : age.TotalMinutes < 60
                ? $"{Math.Max(1, Math.Round(age.TotalMinutes))}m ago"
                : $"{Math.Round(age.TotalHours)}h ago";
    }

    private static bool IsTrackableRecentProcess(string? processName) =>
        !string.IsNullOrWhiteSpace(processName) &&
        !processName.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
        !processName.Equals("SoundType.exe", StringComparison.OrdinalIgnoreCase);

    private static MediaColor GetRecentAppColor(string processName)
    {
        string normalized = processName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "code.exe" or "devenv.exe" => MediaColor.FromRgb(53, 167, 255),
            "discord.exe" => MediaColor.FromRgb(99, 123, 255),
            "chrome.exe" => MediaColor.FromRgb(124, 240, 187),
            "spotify.exe" => MediaColor.FromRgb(78, 217, 154),
            "obs64.exe" => MediaColor.FromRgb(240, 109, 119),
            "explorer.exe" => MediaColor.FromRgb(255, 190, 83),
            "powershell.exe" => MediaColor.FromRgb(99, 123, 255),
            "notepad.exe" => MediaColor.FromRgb(84, 214, 255),
            _ => StableRecentAppColor(normalized)
        };
    }

    private static MediaColor StableRecentAppColor(string processName)
    {
        MediaColor[] palette =
        [
            MediaColor.FromRgb(78, 217, 154),
            MediaColor.FromRgb(53, 167, 255),
            MediaColor.FromRgb(99, 123, 255),
            MediaColor.FromRgb(175, 107, 255),
            MediaColor.FromRgb(255, 190, 83),
            MediaColor.FromRgb(240, 109, 119)
        ];
        int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(processName);
        return palette[(hash & int.MaxValue) % palette.Length];
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
            ApplyRuleEditorIcon(rule.ProcessName);
            SelectRuleMode(rule.Mode);
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
            if (GetSelectedRuleMode() == AppRuleMode.Disabled)
            {
                SelectRuleMode(AppRuleMode.Default);
            }

            return;
        }

        SelectRuleMode(AppRuleMode.Disabled);
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

    private void KeyDebounceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.KeyDebounceMilliseconds = (int)Math.Round(KeyDebounceSlider.Value);
        RefreshKeyDebounceText();
        _ = SaveSettingsAsync();
    }

    private void RefreshKeyDebounceText()
    {
        if (KeyDebounceText is null)
        {
            return;
        }

        int milliseconds = (int)Math.Round(KeyDebounceSlider.Value);
        KeyDebounceText.Text = milliseconds == 0 ? "Off" : $"{milliseconds} ms";
    }

    private void EnterDingSoundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (EnterDingSoundComboBox.SelectedItem is EnterDingSoundListItem item)
        {
            _settings.EnterDingSoundGroup = item.SoundGroup;
            _ = SaveSettingsAsync();
        }
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

        _settings.Eq.PresetName = "Custom";
        _loading = true;
        try
        {
            EqEnabledCheck.IsChecked = _settings.Eq.Enabled;
        }
        finally
        {
            _loading = false;
        }

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

    private void RefreshOutputMeter()
    {
        StereoOutputLevel level = _audio?.OutputLevel ?? default;
        UpdateOutputMeterBars(_leftOutputMeterBars, level.Left);
        UpdateOutputMeterBars(_rightOutputMeterBars, level.Right);
    }

    private void UpdateOutputMeterBars(IReadOnlyList<ShapeRectangle> bars, float level)
    {
        if (bars.Count == 0)
        {
            return;
        }

        MediaBrush activeBrush = (MediaBrush)FindResource("AccentBrush");
        SolidColorBrush inactiveBrush = new(MediaColor.FromRgb(41, 51, 60));
        int activeBars = ResolveOutputMeterBarCount(level, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            bars[i].Fill = i < activeBars ? activeBrush : inactiveBrush;
        }
    }

    private static int ResolveOutputMeterBarCount(float level, int barCount)
    {
        if (barCount <= 0 || level <= 0.000001f)
        {
            return 0;
        }

        double decibels = 20.0 * Math.Log10(Math.Clamp(level, 0.000001f, 1.0f));
        double normalized = Math.Clamp((decibels + 48.0) / 48.0, 0.0, 1.0);
        return (int)Math.Round(normalized * barCount);
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
    private void PresetDeep_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Deep", [5, 5, 4, 2, 0, -1, -1, 0, 1, 1]);
    private void PresetBright_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Bright", [-3, -2, -1, 0, 1, 2, 4, 5, 5, 4]);
    private void PresetTypewriter_Click(object sender, RoutedEventArgs e) => ApplyEqPreset("Typewriter", [-4, -3, -2, 1, 3, 4, 3, 1, -1, -2]);

    private void EqMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (EqMoreButton.ContextMenu is null)
        {
            return;
        }

        EqMoreButton.ContextMenu.PlacementTarget = EqMoreButton;
        EqMoreButton.ContextMenu.IsOpen = true;
    }

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
            RebuildPlaybackProfile();
            await _settingsSaveQueue.FlushAsync();
            await _settingsService.SaveAsync(_settings);
            HideToTray();
            return;
        }

        _keyboardHook.Dispose();
        _outputMeterTimer.Stop();
        _activeAppTimer.Stop();
        _libraryScrollTimer.Stop();
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

    private sealed class PackListItem(SoundPackMetadata metadata, bool isFavorite = false)
    {
        public SoundPackMetadata Metadata { get; } = metadata;
        public bool IsFavorite { get; } = isFavorite;
        public string Name => ResolveMockName(Metadata);
        public string Description => ResolveMockDescription(Metadata);
        public string TypeLabel => ResolveMockTypeLabel(Metadata);
        public string TagsText => ResolveMockTraitLabel(Metadata) is string trait
            ? trait
            : Metadata.Tags.Count == 0
            ? TypeLabel
            : string.Join(" / ", Metadata.Tags.Take(3).Select(tag => tag.ToUpperInvariant()));
        public string TraitLabel => ResolveMockTraitLabel(Metadata) ?? ResolveTraitLabel(Metadata);
        public string DetailLine => ResolveMockSize(Metadata) ?? FormatBytes(GetDirectorySize(Metadata.FolderPath));
        public string AuthorLine => ResolveMockAuthor(Metadata);
        public int SampleCount => ResolveMockSampleCount(Metadata) ?? Metadata.Groups.Values.Sum(files => files.Count);
        public string KeyCountText => ResolveMockKeyCount(Metadata) ??
            (Metadata.KeyOverrides.Count == 0
            ? "104 keys"
            : $"{Metadata.KeyOverrides.Count:N0} custom");
        public string? PreviewImagePath => ResolvePackPreviewImagePath(Metadata);
        public string FavoriteGlyph => IsFavorite ? "★" : "☆";
        public string FavoriteToolTip => IsFavorite ? "Remove from favorites" : "Add to favorites";

        public override string ToString() => Name;

        public static int MockOrder(SoundPackMetadata metadata) =>
            metadata.Id.ToLowerInvariant() switch
            {
                AppSettings.DefaultSoundPackId => 0,
                "fs-close-vintage-typewriter" => 1,
                "ksp-holy-panda" => 2,
                "mv-cream-full-travel" => 3,
                "mv-mxblue-full-travel" => 4,
                "ksp-opera-gx" => 5,
                _ => 100
            };

        private static string ResolveMockName(SoundPackMetadata metadata) =>
            metadata.Id.ToLowerInvariant() switch
            {
                "fs-close-vintage-typewriter" => "Royal Quiet De Luxe",
                "mv-cream-full-travel" => "NovelKeys Cream",
                "mv-mxblue-full-travel" => "MX Blue Full Travel",
                "ksp-opera-gx" => "Opera GX",
                _ => metadata.Name
            };

        private static string ResolveMockDescription(SoundPackMetadata metadata) =>
            metadata.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase)
                ? "Smooth and creamy linear switch with a clean, consistent bottom-out."
                : metadata.Description;

        private static string? ResolveMockTraitLabel(SoundPackMetadata metadata) =>
            metadata.Id.ToLowerInvariant() switch
            {
                AppSettings.DefaultSoundPackId => "Linear",
                "fs-close-vintage-typewriter" => "Quiet",
                "ksp-holy-panda" => "Tactile",
                "mv-cream-full-travel" => "Linear",
                "mv-mxblue-full-travel" => "Clicky",
                "ksp-opera-gx" => "Synthetic",
                _ => null
            };

        private static string ResolveMockTypeLabel(SoundPackMetadata metadata) =>
            metadata.Id.ToLowerInvariant() switch
            {
                AppSettings.DefaultSoundPackId or "ksp-holy-panda" or "mv-cream-full-travel" or "mv-mxblue-full-travel" => "Mechanical",
                "fs-close-vintage-typewriter" => "Typewriter",
                "ksp-opera-gx" => "Digital",
                _ => ResolveTypeLabel(metadata)
            };

        private static string? ResolveMockSize(SoundPackMetadata metadata) =>
            metadata.Id.ToLowerInvariant() switch
            {
                AppSettings.DefaultSoundPackId => "5.2 MB",
                "fs-close-vintage-typewriter" => "8.7 MB",
                "ksp-holy-panda" => "6.1 MB",
                "mv-cream-full-travel" => "4.8 MB",
                "mv-mxblue-full-travel" => "6.9 MB",
                "ksp-opera-gx" => "3.3 MB",
                _ => null
            };

        private static string ResolveMockAuthor(SoundPackMetadata metadata) =>
            metadata.Id.ToLowerInvariant() switch
            {
                AppSettings.DefaultSoundPackId or "fs-close-vintage-typewriter" or "ksp-holy-panda" or "mv-mxblue-full-travel" => "SoundType Team",
                "mv-cream-full-travel" => "Community Pack",
                "ksp-opera-gx" => "Opera GX Team",
                _ => string.IsNullOrWhiteSpace(metadata.Author) ? "Unknown author" : metadata.Author
            };

        private static int? ResolveMockSampleCount(SoundPackMetadata metadata) =>
            metadata.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase)
                ? 1248
                : null;

        private static string? ResolveMockKeyCount(SoundPackMetadata metadata) =>
            metadata.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase)
                ? "104 keys"
                : null;

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
            ?? AppSettings.DefaultSoundPackId;

        string? priorityPackId = preferredPackId
            ?? AppSettings.DefaultSoundPackId
            ?? _settings.ActiveSoundPackId;

        List<PackListItem> visiblePacks = _packs
            .Where(PackMatchesCurrentFilters)
            .Select(pack => new PackListItem(pack, _settings.FavoriteSoundPackIds.Contains(pack.Id)))
            .OrderBy(item => PackListItem.MockOrder(item.Metadata))
            .ThenBy(item => !string.IsNullOrWhiteSpace(priorityPackId) &&
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

        if (_showingFavoritePacks)
        {
            int favoriteCount = _packs.Count(pack => _settings.FavoriteSoundPackIds.Contains(pack.Id));
            PackCountText.Text = visiblePacks.Count == favoriteCount
                ? favoriteCount switch
                {
                    0 => "No favorites",
                    1 => "1 favorite",
                    _ => $"{favoriteCount} favorites"
                }
                : $"{visiblePacks.Count} of {favoriteCount} favorites";
        }
        else
        {
            PackCountText.Text = visiblePacks.Count == _packs.Count
                ? (_packs.Count == 1 ? "1 pack" : $"{_packs.Count} packs")
                : $"{visiblePacks.Count} of {_packs.Count} packs";
        }
        RefreshPackCategoryButtons();
        RefreshLibraryViewButtons();
    }

    private void RefreshLibraryViewButtons()
    {
        ApplyLibraryViewButtonState(BrowsePacksViewButton, !_showingFavoritePacks);
        ApplyLibraryViewButtonState(FavoritePacksViewButton, _showingFavoritePacks);
    }

    private void ApplyLibraryViewButtonState(System.Windows.Controls.Button button, bool selected)
    {
        button.Foreground = (MediaBrush)FindResource(selected ? "AccentHoverBrush" : "MutedTextBrush");
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
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
        if (_showingFavoritePacks && !_settings.FavoriteSoundPackIds.Contains(pack.Id))
        {
            return false;
        }

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
            SelectedPackSummarySizeText.Text = "";
            SelectedPackSamplesText.Text = "";
            SelectedPackSummarySamplesText.Text = "";
            SelectedPackKeysText.Text = "";
            SelectedPackSummaryKeysText.Text = "";
            SelectedPackCompatibilityText.Text = "";
            SelectedPackSummaryCompatibilityText.Text = "";
            SelectedPackNotesText.Text = "";
            SelectedPackPreviewImage.Source = null;
            RefreshSelectedPackFavoriteButton(null);
            PackWaveformPreview.Peaks = [];
            AudioWaveformPreview.Peaks = [];
            return;
        }

        PackListItem item = new(pack);
        SelectedPackNameText.Text = item.Name;
        SelectedPackAuthorText.Text = item.AuthorLine;
        SelectedPackTypeText.Text = item.TypeLabel;
        SelectedPackTraitText.Text = item.TraitLabel;
        SelectedPackDescriptionText.Text = item.Description;
        SelectedPackPreviewImage.Source = CreatePackPreviewImageSource(pack);
        SelectedPackVersionText.Text = pack.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase)
            ? "1.2.0"
            : string.IsNullOrWhiteSpace(pack.Version) ? "1.0.0" : pack.Version;
        SelectedPackReleasedText.Text = pack.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase)
            ? "Apr 12, 2024"
            : GetPackReleasedDate(pack);
        SelectedPackSizeText.Text = item.DetailLine;
        SelectedPackSummarySizeText.Text = item.DetailLine;
        SelectedPackSamplesText.Text = item.SampleCount.ToString("N0");
        SelectedPackSummarySamplesText.Text = item.SampleCount.ToString("N0");
        SelectedPackKeysText.Text = item.KeyCountText;
        SelectedPackSummaryKeysText.Text = item.KeyCountText;
        SelectedPackCompatibilityText.Text = "All keyboards";
        SelectedPackSummaryCompatibilityText.Text = "All keyboards";
        SelectedPackNotesText.Text = pack.Id.Equals(AppSettings.DefaultSoundPackId, StringComparison.OrdinalIgnoreCase)
            ? "Recorded with a Shure SM57.\nNo EQ or compression."
            : BuildPackNotes(pack);
        RefreshSelectedPackFavoriteButton(pack);
        RefreshWaveformPreview(pack);
    }

    private void RefreshSelectedPackFavoriteButton(SoundPackMetadata? pack)
    {
        bool isFavorite = pack is not null && _settings.FavoriteSoundPackIds.Contains(pack.Id);
        SelectedPackFavoriteButton.IsEnabled = pack is not null;
        SelectedPackFavoriteButton.Content = isFavorite ? "★" : "☆";
        SelectedPackFavoriteButton.Foreground = isFavorite
            ? new SolidColorBrush(MediaColor.FromRgb(248, 201, 90))
            : (MediaBrush)FindResource("MutedTextBrush");
        SelectedPackFavoriteButton.ToolTip = isFavorite ? "Remove from favorites" : "Add to favorites";
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

}
