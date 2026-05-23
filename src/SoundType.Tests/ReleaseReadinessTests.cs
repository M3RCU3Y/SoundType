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
    public void PitchCharacterHelpMarkersExplainTheirControls()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml"));

        Assert.Contains("Adds tiny pitch differences between keystrokes", xaml);
        Assert.Contains("Softens the start of each key sound", xaml);
        Assert.Contains("PitchHelpBadgeStyle", xaml);
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
