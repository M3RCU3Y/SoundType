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
        Assert.Contains("private void StartHotkeyRecording_Click", code);
        Assert.Contains("private void CaptureHotkeyFromKeyDown", code);
        Assert.Contains("private static bool MatchesHotkey", code);
        Assert.Contains("PreviewNormalHotkey", settings);
        Assert.Contains("NextPackHotkey", settings);
        Assert.Contains("PreviousPackHotkey", settings);
        Assert.DoesNotContain("<ColumnDefinition Width=\"224\"/>", xaml);
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
        Assert.Contains("x:Name=\"DefaultProfileTagText\"", xaml);
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
