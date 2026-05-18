using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SoundType.Input;

public sealed class KeyboardHookService : IDisposable
{
    internal const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeyup = 0x0105;
    private readonly LowLevelKeyboardProc _proc;
    private readonly IKeyboardHookPlatform _platform;
    private readonly ConcurrentDictionary<int, byte> _pressedKeys = new();
    private IntPtr _hookId;
    private bool _disposed;

    public KeyboardHookService()
        : this(new WindowsKeyboardHookPlatform())
    {
    }

    public KeyboardHookService(IKeyboardHookPlatform platform)
    {
        _platform = platform;
        _proc = HookCallback;
    }

    public event EventHandler<KeyPressedEvent>? KeyPressed;

    public KeyboardHookStartResult Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            return KeyboardHookStartResult.Success();
        }

        _hookId = _platform.SetHook(_proc);
        if (_hookId == IntPtr.Zero)
        {
            string message = _platform.TryGetLastErrorMessage(out string errorMessage)
                ? errorMessage
                : "Windows did not provide a keyboard hook error.";
            return KeyboardHookStartResult.Failure($"Keyboard hook unavailable: {message}");
        }

        return KeyboardHookStartResult.Success();
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        _ = _platform.Unhook(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WmKeydown || wParam == WmSyskeydown || wParam == WmKeyup || wParam == WmSyskeyup))
        {
            KbdLlHookStruct hookData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool isRelease = wParam == WmKeyup || wParam == WmSyskeyup;
            bool isRepeat = false;
            if (isRelease)
            {
                _pressedKeys.TryRemove(hookData.VkCode, out _);
            }
            else
            {
                isRepeat = !_pressedKeys.TryAdd(hookData.VkCode, 0);
            }

            KeyPressed?.Invoke(this, new KeyPressedEvent
            {
                Key = KeyIdentityMapper.FromVirtualKey(hookData.VkCode),
                Timestamp = now,
                IsRepeat = isRepeat,
                IsRelease = isRelease
            });
        }

        return _platform.CallNextHook(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly int VkCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr DwExtraInfo;
    }

}

public readonly record struct KeyboardHookStartResult(bool Started, string? ErrorMessage)
{
    public static KeyboardHookStartResult Success() => new(true, null);

    public static KeyboardHookStartResult Failure(string errorMessage) => new(false, errorMessage);
}

public interface IKeyboardHookPlatform
{
    IntPtr SetHook(KeyboardHookService.LowLevelKeyboardProc callback);

    bool TryGetLastErrorMessage(out string message);

    bool Unhook(IntPtr hookId);

    IntPtr CallNextHook(IntPtr hookId, int nCode, IntPtr wParam, IntPtr lParam);
}

internal sealed class WindowsKeyboardHookPlatform : IKeyboardHookPlatform
{
    public IntPtr SetHook(KeyboardHookService.LowLevelKeyboardProc callback)
    {
        using System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using System.Diagnostics.ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = currentModule is null ? IntPtr.Zero : GetModuleHandle(currentModule.ModuleName);
        return SetWindowsHookEx(KeyboardHookService.WhKeyboardLl, callback, moduleHandle, 0);
    }

    public bool TryGetLastErrorMessage(out string message)
    {
        int error = Marshal.GetLastWin32Error();
        if (error == 0)
        {
            message = "";
            return false;
        }

        message = new Win32Exception(error).Message;
        return true;
    }

    public bool Unhook(IntPtr hookId) => UnhookWindowsHookEx(hookId);

    public IntPtr CallNextHook(IntPtr hookId, int nCode, IntPtr wParam, IntPtr lParam) =>
        CallNextHookEx(hookId, nCode, wParam, lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardHookService.LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
