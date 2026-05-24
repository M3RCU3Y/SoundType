using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaImageSource = System.Windows.Media.ImageSource;

namespace SoundType.App.ViewModels;

internal sealed record AppVisual(
    string IconText,
    MediaFontFamily IconFontFamily,
    MediaBrush IconForeground,
    MediaBrush IconBackground,
    double IconFontSize,
    MediaImageSource? IconSource)
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;

    private static readonly MediaFontFamily Segoe = new("Segoe UI");
    private static readonly MediaFontFamily Mdl2 = new("Segoe MDL2 Assets");
    private static readonly Dictionary<string, MediaImageSource?> IconCache = [];
    private static readonly object IconCacheLock = new();

    public static AppVisual ForProcess(string? processName)
    {
        string normalized = (processName ?? "").Trim().ToLowerInvariant();
        MediaImageSource? iconSource = GetIconSource(normalized);

        return normalized switch
        {
            "code.exe" or "devenv.exe" => Native("\uE8A7", Mdl2, 24, iconSource),
            "explorer.exe" => Native("\uE8B7", Mdl2, 22, iconSource),
            "notepad.exe" => Native("\uE70B", Mdl2, 20, iconSource),
            "powershell.exe" => Native(">", Segoe, 18, iconSource),
            "" or "unknown" => Native("?", Segoe, 18, iconSource),
            _ => Native(GetInitial(normalized), Segoe, 16, iconSource)
        };
    }

    private static MediaImageSource? GetIconSource(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName) || processName == "unknown")
        {
            return null;
        }

        lock (IconCacheLock)
        {
            if (IconCache.TryGetValue(processName, out MediaImageSource? cached))
            {
                return cached;
            }
        }

        MediaImageSource? source = ResolveExecutablePath(processName) is { } path
            ? CreateIconSource(path)
            : CreateGenericExecutableIconSource();

        lock (IconCacheLock)
        {
            IconCache[processName] = source;
        }

        return source;
    }

    private static string? ResolveExecutablePath(string processName)
    {
        foreach (string path in ResolveFromRunningProcesses(processName))
        {
            return path;
        }

        foreach (string path in ResolveFromAppPaths(processName))
        {
            return path;
        }

        foreach (string path in ResolveKnownInstallPaths(processName))
        {
            return path;
        }

        return null;
    }

    private static IEnumerable<string> ResolveFromRunningProcesses(string processName)
    {
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
        {
            yield break;
        }

        foreach (Process process in Process.GetProcessesByName(nameWithoutExtension))
        {
            using (process)
            {
                string? fileName = null;
                try
                {
                    fileName = process.MainModule?.FileName;
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }

                if (IsExistingFile(fileName))
                {
                    yield return fileName!;
                }
            }
        }
    }

    private static IEnumerable<string> ResolveFromAppPaths(string processName)
    {
        string[] roots =
        [
            $@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\{processName}",
            $@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\{processName}"
        ];

        foreach (string root in roots)
        {
            if (Registry.GetValue(root, "", null) is string value && IsExistingFile(value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> ResolveKnownInstallPaths(string processName)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        IEnumerable<string> candidates = processName switch
        {
            "explorer.exe" => [Path.Combine(windows, "explorer.exe")],
            "notepad.exe" => [Path.Combine(windows, "System32", "notepad.exe")],
            "powershell.exe" => [Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe")],
            "code.exe" => [
                Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(programFiles, "Microsoft VS Code", "Code.exe"),
                Path.Combine(programFilesX86, "Microsoft VS Code", "Code.exe")
            ],
            "chrome.exe" => [
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe")
            ],
            "discord.exe" => ResolveDiscordPaths(localAppData),
            "spotify.exe" => [
                Path.Combine(appData, "Spotify", "Spotify.exe"),
                Path.Combine(localAppData, "Microsoft", "WindowsApps", "Spotify.exe")
            ],
            "obs64.exe" => [Path.Combine(programFiles, "obs-studio", "bin", "64bit", "obs64.exe")],
            "devenv.exe" => ResolveVisualStudioPaths(programFiles, programFilesX86),
            _ => []
        };

        foreach (string candidate in candidates)
        {
            if (IsExistingFile(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> ResolveDiscordPaths(string localAppData)
    {
        string root = Path.Combine(localAppData, "Discord");
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(root, "Discord.exe", SearchOption.AllDirectories)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> ResolveVisualStudioPaths(string programFiles, string programFilesX86)
    {
        foreach (string root in new[] { programFiles, programFilesX86 }
                     .Select(path => Path.Combine(path, "Microsoft Visual Studio"))
                     .Where(Directory.Exists))
        {
            foreach (string path in Directory.EnumerateFiles(root, "devenv.exe", SearchOption.AllDirectories)
                         .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                yield return path;
            }
        }
    }

    private static MediaImageSource? CreateIconSource(string executablePath)
    {
        return CreateShellIconSource(executablePath, useFileAttributes: false) ??
            CreateAssociatedIconSource(executablePath) ??
            CreateGenericExecutableIconSource();
    }

    private static MediaImageSource? CreateGenericExecutableIconSource() =>
        CreateShellIconSource("application.exe", useFileAttributes: true);

    private static MediaImageSource? CreateShellIconSource(string executablePath, bool useFileAttributes)
    {
        nint iconHandle = 0;
        try
        {
            ShellFileInfo shellFileInfo = new();
            uint flags = ShgfiIcon | ShgfiLargeIcon;
            uint attributes = 0;
            if (useFileAttributes)
            {
                flags |= ShgfiUseFileAttributes;
                attributes = FileAttributeNormal;
            }

            nint result = SHGetFileInfo(
                executablePath,
                attributes,
                ref shellFileInfo,
                (uint)Marshal.SizeOf<ShellFileInfo>(),
                flags);

            iconHandle = shellFileInfo.IconHandle;
            if (result == 0 || iconHandle == 0)
            {
                return null;
            }

            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            return source;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            if (iconHandle != 0)
            {
                DestroyIcon(iconHandle);
            }
        }
    }

    private static MediaImageSource? CreateAssociatedIconSource(string executablePath)
    {
        try
        {
            using System.Drawing.Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
            {
                return null;
            }

            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            source.Freeze();
            return source;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShellFileInfo shellFileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint iconHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    private static bool IsExistingFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static string GetInitial(string value) =>
        string.IsNullOrWhiteSpace(value) ? "?" : value[0].ToString().ToUpperInvariant();

    private static AppVisual Native(string fallbackText, MediaFontFamily fallbackFont, double fallbackFontSize, MediaImageSource? iconSource) =>
        new(
            fallbackText,
            fallbackFont,
            Brush(185, 196, 204),
            new SolidColorBrush(Colors.Transparent),
            fallbackFontSize,
            iconSource);

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(MediaColor.FromRgb(r, g, b));
}
