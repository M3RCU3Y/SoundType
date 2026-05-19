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
    public void MainWindow_FlushesSettingsWhenClosingToTray()
    {
        string root = FindRepositoryRoot();
        string code = File.ReadAllText(Path.Combine(root, "src", "SoundType.App", "MainWindow.xaml.cs"));

        Assert.Contains("await _settingsSaveQueue.FlushAsync();", code);
        Assert.Contains("await _settingsService.SaveAsync(_settings);", code);
        Assert.Contains("HideToTray();", code);
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
