namespace SoundType.Tests;

public sealed class ReleaseReadinessTests
{
    [Fact]
    public void AppProject_DefinesPortableReleaseMetadataAndIcon()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "SoundType.App.csproj"));

        Assert.Contains("<ApplicationIcon>Assets\\SoundType.ico</ApplicationIcon>", project);
        Assert.Contains("<AssemblyName>SoundType</AssemblyName>", project);
        Assert.Contains("<Product>SoundType</Product>", project);
        Assert.Contains("<Version>", project);
    }

    [Fact]
    public void PublishScript_BuildsSelfContainedPortableZipAndChecksum()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(root, "tools", "publish-portable.ps1"));

        Assert.Contains("--self-contained true", script);
        Assert.Contains("portable.sha256", script);
        Assert.Contains("DebugType=None", script);
        Assert.Contains("DebugSymbols=false", script);
    }

    [Fact]
    public void MainWindow_UsesWindowsChromeAndHasNoPrototypeSidebarLabels()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.DoesNotContain("Text=\"Git\"", xaml);
        Assert.DoesNotContain("Text=\"Chat\"", xaml);
        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("<shell:WindowChrome", xaml);
        Assert.Contains("ResizeMode=\"CanResize\"", xaml);
    }

    [Fact]
    public void MainWindow_DoesNotForceEnablePanningOnStartup()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("_settings.Pan.Enabled = true", code);
        Assert.DoesNotContain("_settings.Pan.Strength = 1.1", code);
    }

    [Fact]
    public void MainWindow_DoesNotRewriteFlatEqOnStartup()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("ApplyDefaultAudioPageEqCurve", code);
        Assert.DoesNotContain("DefaultAudioPageEqCurve", code);
    }

    [Fact]
    public void EqualizerPresetButtonsLeaveRoomForLabels()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("Content=\"Thock\" Height=\"32\" Width=\"74\" Padding=\"0\"", xaml);
        Assert.Contains("Content=\"Soft Night\" Height=\"32\" Width=\"104\" Padding=\"0\"", xaml);
    }

    [Fact]
    public void EqualizerMoreButtonOpensUsefulPresetMenu()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"EqMoreButton\"", xaml);
        Assert.Contains("Click=\"EqMoreButton_Click\"", xaml);
        Assert.Contains("Header=\"Deep\"", xaml);
        Assert.Contains("Header=\"Bright\"", xaml);
        Assert.Contains("Header=\"Reset flat\"", xaml);
        Assert.Contains("private void PresetDeep_Click", code);
        Assert.Contains("private void PresetBright_Click", code);
        Assert.Contains("private void EqMoreButton_Click", code);
    }

    [Fact]
    public void AppRulesEditorCardMatchesRulesListHeight()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("<RowDefinition Height=\"306\"/>", xaml);
        Assert.Contains("x:Name=\"RuleEditorCard\"", xaml);
        Assert.Contains("Height=\"450\"", xaml);
        Assert.Contains("VerticalAlignment=\"Top\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", xaml);
    }

    [Fact]
    public void AppRulesForegroundCardAlignsWithRecentApps()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"RecentAppsCard\"", xaml);
        Assert.Contains("x:Name=\"ForegroundAppListeningCard\"", xaml);
        Assert.Contains("MinWidth=\"{Binding ActualWidth, ElementName=RuleEditorCard}\"", xaml);
        Assert.DoesNotContain("x:Name=\"ForegroundAppListeningCard\" Style=\"{StaticResource SectionCardStyle}\" Padding=\"18\" Margin=\"0,28,0,0\"", xaml);
    }

    [Fact]
    public void AppRulesEmptyStateCreateRuleButtonCentersContent()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        int start = xaml.IndexOf("x:Name=\"AppRulesEmptyState\"", StringComparison.Ordinal);
        int end = xaml.IndexOf("<DockPanel Grid.Row=\"3\"", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string emptyState = xaml[start..end];

        Assert.Contains("Text=\"No app rules yet\"", emptyState);
        Assert.Contains("Click=\"FocusNewRule_Click\"", emptyState);
        Assert.Contains("<Grid HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\">", emptyState);
        Assert.Contains("<ColumnDefinition Width=\"16\"/>", emptyState);
        Assert.Contains("<ColumnDefinition Width=\"10\"/>", emptyState);
        Assert.Contains("Text=\"&#xE710;\"", emptyState);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", emptyState);
        Assert.Contains("Width=\"16\"", emptyState);
        Assert.Contains("Height=\"16\"", emptyState);
        Assert.DoesNotContain("Margin=\"0,-2,0,0\"", emptyState);
        Assert.Contains("Text=\"Create Rule\" VerticalAlignment=\"Center\"", emptyState);
    }

    [Fact]
    public void AppRulesRuleEditorProcessNameIsLeftAligned()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        int start = xaml.IndexOf("x:Name=\"ProcessRuleTextBox\"", StringComparison.Ordinal);
        int end = xaml.IndexOf("/>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string processField = xaml[start..end];

        Assert.Contains("Padding=\"14,0\"", processField);
        Assert.Contains("TextAlignment=\"Left\"", processField);
        Assert.Contains("HorizontalContentAlignment=\"Left\"", processField);
        Assert.Contains("VerticalContentAlignment=\"Center\"", processField);
    }

    [Fact]
    public void LibraryPackListExpandsWithoutRedundantFooterCount()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("<ListBox x:Name=\"PacksList\"", xaml);
        Assert.Contains("Grid.RowSpan=\"2\"", xaml);
        Assert.Contains("VirtualizingPanel.IsVirtualizing\" Value=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"", xaml);
        Assert.Contains("LibraryScrollImmediateRatio", code);
        Assert.Contains("ScrollToVerticalOffset(immediateOffset)", code);
        Assert.DoesNotContain("Text=\"22 packs total\"", xaml);
    }

    [Fact]
    public void LibraryCategoryFiltersKeepDigitalLabelVisible()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("x:Key=\"LibraryCategoryButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"MechanicalCategoryButton\" Content=\"Mechanical switches\" Style=\"{StaticResource LibraryCategoryButtonStyle}\"", xaml);
        Assert.Contains("x:Name=\"TypewriterCategoryButton\" Content=\"Typewriters\" Style=\"{StaticResource LibraryCategoryButtonStyle}\"", xaml);
        Assert.Contains("x:Name=\"DigitalCategoryButton\" Content=\"Digital\" Style=\"{StaticResource LibraryCategoryButtonStyle}\"", xaml);
        Assert.Contains("x:Name=\"PackCountText\"", xaml);
        Assert.Contains("Margin=\"8,0,0,0\"", xaml);
    }

    [Fact]
    public void SwitchInformationFavoriteStarIsClickableAndPersistent()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "src", "SoundType.Core", "Models", "AppSettings.cs"));

        Assert.Contains("x:Name=\"SelectedPackFavoriteButton\"", xaml);
        Assert.Contains("Click=\"SelectedPackFavoriteButton_Click\"", xaml);
        Assert.Contains("private void SelectedPackFavoriteButton_Click", code);
        Assert.Contains("RefreshSelectedPackFavoriteButton(pack)", code);
        Assert.Contains("FavoriteSoundPackIds", settings);
    }

    [Fact]
    public void LibraryHeaderCanShowFavoritePacks()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"BrowsePacksViewButton\"", xaml);
        Assert.Contains("Click=\"BrowsePacksViewButton_Click\"", xaml);
        Assert.Contains("x:Name=\"FavoritePacksViewButton\"", xaml);
        Assert.Contains("Click=\"FavoritePacksViewButton_Click\"", xaml);
        Assert.Contains("x:Name=\"LibraryViewTabs\" Orientation=\"Horizontal\" Margin=\"28,4,0,0\" Visibility=\"Collapsed\" shell:WindowChrome.IsHitTestVisibleInChrome=\"True\"", xaml);
        Assert.Contains("private bool _showingFavoritePacks", code);
        Assert.Contains("private void FavoritePacksViewButton_Click", code);
        Assert.Contains("!_settings.FavoriteSoundPackIds.Contains(pack.Id)", code);
        Assert.Contains("\"No favorites\"", code);
    }

    [Fact]
    public void LibraryRowsCanToggleFavoritePacks()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"PackRowFavoriteButton\"", xaml);
        Assert.Contains("Click=\"PackRowFavoriteButton_Click\"", xaml);
        Assert.Contains("Content=\"{Binding FavoriteGlyph}\"", xaml);
        Assert.Contains("private void PackRowFavoriteButton_Click", code);
        Assert.Contains("private void ToggleFavoritePack", code);
        Assert.Contains("public string FavoriteGlyph", code);
    }

    [Fact]
    public void PackPreviewVolumeUsesRealIcon()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"PackPreviewVolumeIcon\"", xaml);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
        Assert.DoesNotContain("Text=\")))\"", xaml);
    }

    [Fact]
    public void PackWaveformToolbarOpensWaveformLocationWithoutDuplicatePlayButton()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"PackWaveformLocationButton\"", xaml);
        Assert.Contains("Click=\"OpenPackWaveformLocation_Click\"", xaml);
        Assert.Contains("private void OpenPackWaveformLocation_Click", code);
        Assert.Contains("ResolvePackWaveformLocation", code);
        Assert.Contains("<Style TargetType=\"{x:Type ToolTip}\" BasedOn=\"{StaticResource PitchHelpToolTipStyle}\">", xaml);
        Assert.Contains("ToolTip=\"Open waveform location\"", xaml);
        Assert.DoesNotContain("Margin=\"0,0,12,0\" Click=\"PreviewNormal_Click\"", xaml);
    }

    [Fact]
    public void PackDetailsButtonsCenterIconsAndReportIssuesOnGitHub()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("Click=\"ReportIssue_Click\"", xaml);
        Assert.Contains("Text=\"Open Pack Folder\" VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("Text=\"Report an issue\" VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("Width=\"16\" Height=\"16\" LineHeight=\"16\"", xaml);
        Assert.Contains("private void ReportIssue_Click", code);
        Assert.Contains("https://github.com/M3RCU3Y/SoundType/issues/new/choose", code);
        Assert.Contains("private static void OpenUrl", code);
    }

    [Fact]
    public void MainHeaderCanSurfaceGitHubUpdate()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"UpdateAvailableButton\"", xaml);
        Assert.Contains("Click=\"UpdateAvailableButton_Click\"", xaml);
        Assert.Contains("Visibility=\"Collapsed\"", xaml);
        Assert.Contains("ToolTip=\"Update available\"", xaml);
        Assert.Contains("private async Task CheckForUpdatesAsync", code);
        Assert.Contains("UpdateAvailableButton.Visibility = Visibility.Visible", code);
        Assert.Contains("private async void UpdateAvailableButton_Click", code);
        Assert.Contains("await StartPortableUpdateAsync", code);
        Assert.Contains("PortableZipUrl", code);
        Assert.Contains("Open update page", code);
    }

    [Fact]
    public void GitHubIssueTemplatesCoverMainReportPaths()
    {
        string root = FindRepositoryRoot();
        string templateRoot = Path.Combine(root, ".github", "ISSUE_TEMPLATE");

        Assert.True(File.Exists(Path.Combine(templateRoot, "bug_report.yml")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "feature_request.yml")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "sound_pack_issue.yml")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "config.yml")));
    }

    [Fact]
    public void MainGlobalOutputKeepsOnlyListeningToggle()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        int start = xaml.IndexOf("<TextBlock Text=\"GLOBAL OUTPUT\"", StringComparison.Ordinal);
        int end = xaml.IndexOf("<StackPanel Grid.Row=\"2\"", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);

        string globalOutput = xaml[start..end];
        Assert.Contains("x:Name=\"EnabledToggle\"", globalOutput);
        Assert.DoesNotContain("<ColumnDefinition Width=\"62\"/>", globalOutput);
        Assert.DoesNotContain("Text=\"-48\"", globalOutput);
    }

    [Fact]
    public void SidebarNavIconsUseWhiteInactiveAndGreenSelectedBrushes()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("<Setter Property=\"Tag\" Value=\"{StaticResource TextBrush}\"/>", xaml);
        Assert.Contains("Stroke=\"{Binding Tag, RelativeSource={RelativeSource AncestorType=Button}}\"", xaml);
        Assert.Contains("Fill=\"{Binding Tag, RelativeSource={RelativeSource AncestorType=Button}}\"", xaml);
        Assert.Contains("Foreground=\"{Binding Tag, RelativeSource={RelativeSource AncestorType=Button}}\"", xaml);
        Assert.Contains("button.Foreground = (MediaBrush)FindResource(\"TextBrush\");", code);
        Assert.Contains("button.Tag = (MediaBrush)FindResource(selected ? \"AccentBrush\" : \"TextBrush\");", code);
        Assert.DoesNotContain("selected && ReferenceEquals(activePage, AudioPage)", code);
    }

    [Fact]
    public void TrayStartupIconsReflectEnabledStateWithAnimation()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"MinimizeToTrayIconPath\"", xaml);
        Assert.Contains("x:Name=\"HideToTrayIconPath\"", xaml);
        Assert.Contains("x:Name=\"StartWithWindowsIconPath\"", xaml);
        Assert.Contains("x:Name=\"StartHiddenInTrayIconPath\"", xaml);
        Assert.Contains("private void RefreshTrayStartupIcons", code);
        Assert.Contains("AnimateIconBrush", code);
        Assert.Contains("ColorAnimation", code);
    }

    [Fact]
    public void PlaybackBehaviorDebounceSliderIsFunctionalAndNotDuplicated()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "src", "SoundType.Core", "Models", "AppSettings.cs"));

        Assert.Contains("x:Name=\"KeyDebounceSlider\"", xaml);
        Assert.Contains("ValueChanged=\"KeyDebounceSlider_ValueChanged\"", xaml);
        Assert.Contains("x:Name=\"KeyDebounceText\"", xaml);
        Assert.DoesNotContain("<TextBlock Text=\"20\" Style=\"{StaticResource HotkeyChipTextStyle}\"/>", xaml);
        Assert.Contains("private void KeyDebounceSlider_ValueChanged", code);
        Assert.Contains("ShouldDebounceKeyPress", code);
        Assert.Contains("KeyDebounceMilliseconds", settings);
    }

    [Fact]
    public void SettingsHeaderUsesSimplePreferencesSubtitle()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("PageSubtitleText.Text = \"Preferences\";", code);
        Assert.DoesNotContain("Preferences and privacy", code);
    }

    [Fact]
    public void SettingsHotkeysCardUsesReadableShortcutRows()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "src", "SoundType.Core", "Models", "AppSettings.cs"));

        Assert.Contains("x:Name=\"SettingsHotkeysPanel\"", xaml);
        Assert.Contains("x:Name=\"ToggleListeningHotkeyRow\"", xaml);
        Assert.Contains("HotkeyDisplayStyle", xaml);
        Assert.Contains("x:Name=\"ToggleListeningHotkeyText\"", xaml);
        Assert.Contains("x:Name=\"PreviewNormalHotkeyText\"", xaml);
        Assert.Contains("x:Name=\"NextPackHotkeyText\"", xaml);
        Assert.Contains("x:Name=\"PreviousPackHotkeyText\"", xaml);
        Assert.Contains("Click=\"StartHotkeyRecording_Click\"", xaml);
        Assert.Contains("Click=\"RestoreHotkeys_Click\"", xaml);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"76\"/>", xaml);
        Assert.Contains("private void StartHotkeyRecording_Click", code);
        Assert.Contains("private void CaptureHotkeyFromKeyDown", code);
        Assert.Contains("private static bool MatchesHotkey", code);
        Assert.Contains("PreviewNormalHotkey", settings);
        Assert.Contains("NextPackHotkey", settings);
        Assert.Contains("PreviousPackHotkey", settings);
        Assert.DoesNotContain("<ColumnDefinition Width=\"224\"/>", xaml);
    }

    [Fact]
    public void SettingsEnterDingCardKeepsPreviewControlsVisible()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));
        string packJson = File.ReadAllText(Path.Combine(root, "assets", "packs", "SoundType-EnterDing", "pack.json"));
        int title = xaml.IndexOf("Text=\"Enter Ding\"", StringComparison.Ordinal);
        int start = xaml.LastIndexOf("<Border Grid.Column=\"2\"", title, StringComparison.Ordinal);
        int end = xaml.IndexOf("<Border Grid.Row=\"6\"", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string enterDingCard = xaml[start..end];

        Assert.Contains("x:Name=\"EnterDingSoundComboBox\"", enterDingCard);
        Assert.Contains("Text=\"Preview the selected bell\"", enterDingCard);
        Assert.Contains("Click=\"PreviewEnterDing_Click\"", enterDingCard);
        Assert.Contains("<RowDefinition Height=\"62\"/>", enterDingCard);
        Assert.Contains("x:Name=\"EnterDingSoundComboBox\" Grid.Row=\"1\" Height=\"38\" VerticalAlignment=\"Top\"", enterDingCard);
        Assert.Contains("<Grid Grid.Row=\"5\" Margin=\"0,8,0,0\">", enterDingCard);
        Assert.Contains("<Grid HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\">", enterDingCard);
        Assert.Contains("Width=\"16\" Height=\"16\" LineHeight=\"16\"", enterDingCard);
        Assert.DoesNotContain("<Grid Grid.Row=\"5\" Margin=\"0,14,0,0\">", enterDingCard);
        Assert.Contains("new(\"ding-12\", \"Deep Typewriter Bell\")", code);
        Assert.Contains("\"ding-12\": [ \"enter/ding-12.wav\" ]", packJson);
        Assert.True(File.Exists(Path.Combine(root, "assets", "packs", "SoundType-EnterDing", "enter", "ding-12.wav")));
    }

    [Fact]
    public void SettingsStorageActionsFitInsideCard()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));
        int start = xaml.IndexOf("x:Name=\"SettingsStoragePathGrid\"", StringComparison.Ordinal);
        int manageButton = xaml.IndexOf("x:Name=\"ManagePacksButton\"", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && manageButton > start);
        int end = Math.Min(xaml.Length, manageButton + 900);
        string storageCard = xaml[start..end];

        Assert.Contains("x:Name=\"SettingsStoragePathGrid\"", storageCard);
        Assert.Contains("<ColumnDefinition Width=\"*\"/>", storageCard);
        Assert.Contains("x:Name=\"OpenPacksFolderButton\"", storageCard);
        Assert.Contains("x:Name=\"ClearWaveformCacheButton\"", storageCard);
        Assert.Contains("x:Name=\"ManagePacksButton\"", storageCard);
        Assert.Contains("SettingsStorageActionButtonStyle", xaml);
        Assert.Contains("Style=\"{StaticResource SettingsStorageActionButtonStyle}\"", storageCard);
        Assert.Contains("MinWidth\" Value=\"178\"", xaml);
        Assert.Contains("Height\" Value=\"40\"", xaml);
        Assert.Contains("Click=\"BrowsePacksFolder_Click\"", storageCard);
        Assert.Contains("private void BrowsePacksFolder_Click", code);
        Assert.Contains("FolderBrowserDialog", code);
        Assert.DoesNotContain("<ColumnDefinition Width=\"340\"/>", storageCard);
        Assert.DoesNotContain("<ColumnDefinition Width=\"280\"/>", storageCard);
        Assert.DoesNotContain("Margin=\"18,37,0,0\"", storageCard);
        Assert.DoesNotContain("Width=\"194\"", storageCard);
        Assert.DoesNotContain("Width=\"176\"", storageCard);
        Assert.DoesNotContain("Clear Waveform Cache", storageCard);
    }

    [Fact]
    public void SettingsActivePackMetadataPillCentersText()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"SettingsActivePackTypePill\"", xaml);
        Assert.Contains("Height=\"32\"", xaml);
        Assert.Contains("TextAlignment=\"Center\"", xaml);
        Assert.Contains("VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
    }

    [Fact]
    public void SettingsActivePackChangeButtonHasRoomForLabel()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"SettingsChangePackButton\"", xaml);
        Assert.Contains("Content=\"Change Pack\"", xaml);
        Assert.Contains("MinWidth=\"136\"", xaml);
        Assert.DoesNotContain("Content=\"Change Pack\" Width=\"112\"", xaml);
    }

    [Fact]
    public void AppRulesDefaultProfileTagHasRoomForText()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"DefaultProfileTagPill\"", xaml);
        Assert.Contains("MinWidth=\"84\"", xaml);
        Assert.Contains("Height=\"24\"", xaml);
        Assert.Contains("x:Name=\"DefaultProfileTagText\"", xaml);
        Assert.Contains("Text=\"Default profile\" Foreground=\"{StaticResource MutedTextBrush}\" FontSize=\"12\" LineHeight=\"14\"", xaml);
        Assert.Contains("x:Name=\"DefaultProfileText\" FontSize=\"19\" FontWeight=\"SemiBold\" LineHeight=\"23\"", xaml);
        Assert.Contains("TextAlignment=\"Center\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("VerticalAlignment=\"Center\"", xaml);
    }

    [Fact]
    public void PitchCharacterHelpMarkersExplainTheirControls()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("Legacy uses the old full-group picker", xaml);
        Assert.Contains("Natural adds subtle life", xaml);
        Assert.Contains("Lower values keep the keyboard tighter", xaml);
        Assert.Contains("x:Name=\"SampleVariationModeComboBox\"", xaml);
        Assert.Contains("x:Name=\"SampleVariationSlider\"", xaml);
        Assert.Contains("PitchHelpBadgeStyle", xaml);
    }

    private static int CountNamedOutputMeterBars(string xaml, string meterName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            xaml,
            $"<UniformGrid x:Name=\"{meterName}\"[\\s\\S]*?</UniformGrid>");
        Assert.True(match.Success, $"Could not find {meterName}.");
        return System.Text.RegularExpressions.Regex.Matches(match.Value, "<Rectangle ").Count;
    }

    [Fact]
    public void SpatialMixPanel_KeepsControlsClearOfCardEdges()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("<Border Grid.Column=\"4\" Style=\"{StaticResource SectionCardStyle}\" Padding=\"14,13,14,13\">", xaml);
        Assert.Contains("<ColumnDefinition Width=\"30\"/>", xaml);
        Assert.Contains("<UniformGrid x:Name=\"LeftOutputMeterBars\" Grid.Column=\"1\" Columns=\"48\" Height=\"18\" Margin=\"4,0,8,0\">", xaml);
        Assert.Contains("<UniformGrid x:Name=\"RightOutputMeterBars\" Grid.Column=\"1\" Columns=\"48\" Height=\"18\" Margin=\"4,0,8,0\">", xaml);
        Assert.Equal(48, CountNamedOutputMeterBars(xaml, "LeftOutputMeterBars"));
        Assert.Equal(48, CountNamedOutputMeterBars(xaml, "RightOutputMeterBars"));
        Assert.Contains("<Rectangle Grid.Row=\"5\" Fill=\"#2C353D\" Margin=\"0,10,0,0\"/>", xaml);
    }

    [Fact]
    public void MainWindow_FlushesSettingsWhenClosingToTray()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("await _settingsSaveQueue.FlushAsync();", code);
        Assert.Contains("await _settingsService.SaveAsync(_settings);", code);
        Assert.Contains("HideToTray();", code);
    }

    [Fact]
    public void AnimatedCards_DoNotScalePastTheirLayoutSlot()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("new DoubleAnimation(1.004", code);
        Assert.Contains("CardHoverScale = 1.0", code);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SoundType.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SoundType repository root.");
    }
}
