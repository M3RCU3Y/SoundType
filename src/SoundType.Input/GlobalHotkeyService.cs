using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SoundType.Input;

public sealed class GlobalHotkeyService : IDisposable
{
    public const int WmHotkey = 0x0312;
    private IntPtr _windowHandle;
    private int _hotkeyId;
    private bool _registered;

    public bool TryRegister(IntPtr windowHandle, int hotkeyId, HotkeyGesture gesture, out string? errorMessage)
    {
        errorMessage = null;
        Unregister();

        if (windowHandle == IntPtr.Zero)
        {
            errorMessage = "Global hotkey unavailable: app window is not ready.";
            return false;
        }

        HotkeyModifiers modifiers = gesture.Modifiers | HotkeyModifiers.NoRepeat;
        if (!RegisterHotKey(windowHandle, hotkeyId, (uint)modifiers, (uint)gesture.VirtualKey))
        {
            int error = Marshal.GetLastWin32Error();
            errorMessage = $"Global hotkey unavailable: {new Win32Exception(error).Message}";
            return false;
        }

        _windowHandle = windowHandle;
        _hotkeyId = hotkeyId;
        _registered = true;
        return true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        _ = UnregisterHotKey(_windowHandle, _hotkeyId);
        _registered = false;
        _windowHandle = IntPtr.Zero;
        _hotkeyId = 0;
    }

    public void Dispose() => Unregister();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
