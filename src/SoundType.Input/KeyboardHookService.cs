using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace SoundType.Input;

public sealed class KeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private readonly LowLevelKeyboardProc _proc;
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastKeyDown = new();
    private IntPtr _hookId;
    private bool _disposed;

    public KeyboardHookService()
    {
        _proc = HookCallback;
    }

    public event EventHandler<KeyPressedEvent>? KeyPressed;

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        using System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using System.Diagnostics.ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = currentModule is null ? IntPtr.Zero : GetModuleHandle(currentModule.ModuleName);
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        _ = UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WmKeydown || wParam == WmSyskeydown))
        {
            KbdLlHookStruct hookData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool isRepeat = _lastKeyDown.TryGetValue(hookData.VkCode, out DateTimeOffset previous) &&
                            now - previous < TimeSpan.FromMilliseconds(35);
            _lastKeyDown[hookData.VkCode] = now;

            KeyPressed?.Invoke(this, new KeyPressedEvent
            {
                Key = KeyIdentityMapper.FromVirtualKey(hookData.VkCode),
                Timestamp = now,
                IsRepeat = isRepeat
            });
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
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

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly int VkCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
