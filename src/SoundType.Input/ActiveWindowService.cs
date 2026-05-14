using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SoundType.Input;

public sealed class ActiveWindowService
{
    public string? GetActiveProcessName()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out uint processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }

    public string? GetActiveWindowTitleForDisplayOnly()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return null;
        }

        StringBuilder title = new(256);
        return GetWindowText(foregroundWindow, title, title.Capacity) > 0 ? title.ToString() : null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
