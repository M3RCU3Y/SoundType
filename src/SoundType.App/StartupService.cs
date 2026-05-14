using System.IO;
using System.Security;
using Microsoft.Win32;

namespace SoundType.App;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SoundType";

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string;
    }

    public string? GetRegisteredCommand()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) as string;
    }

    public void SetEnabled(bool enabled) => SetEnabled(enabled, startHiddenInTray: false);

    public void SetEnabled(bool enabled, bool startHiddenInTray)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppName, BuildStartupCommand(startHiddenInTray));
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    public bool TrySetEnabled(bool enabled, out string? errorMessage) =>
        TrySetEnabled(enabled, startHiddenInTray: false, out errorMessage);

    public bool TrySetEnabled(bool enabled, bool startHiddenInTray, out string? errorMessage)
    {
        try
        {
            SetEnabled(enabled, startHiddenInTray);
            errorMessage = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    internal static string BuildStartupCommand(bool startHiddenInTray = false)
    {
        string exePath = Environment.ProcessPath
            ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
            ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        return startHiddenInTray ? $"\"{exePath}\" --tray" : $"\"{exePath}\"";
    }
}
