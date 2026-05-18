using System.Runtime.InteropServices;
using SoundType.Input;

namespace SoundType.Tests;

public sealed class KeyboardHookServiceTests
{
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int AKey = 0x41;

    [Fact]
    public void KeyPressed_MarksSecondKeydownAsRepeatUntilKeyup()
    {
        FakeKeyboardHookPlatform platform = new();
        using KeyboardHookService service = new(platform);
        List<KeyPressedEvent> events = [];
        service.KeyPressed += (_, e) => events.Add(e);
        service.Start();

        platform.Send(WmKeydown, AKey);
        Thread.Sleep(60);
        platform.Send(WmKeydown, AKey);
        platform.Send(WmKeyup, AKey);
        platform.Send(WmKeydown, AKey);

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal("A", first.Key.Code);
                Assert.False(first.IsRepeat);
                Assert.False(first.IsRelease);
            },
            second =>
            {
                Assert.Equal("A", second.Key.Code);
                Assert.True(second.IsRepeat);
                Assert.False(second.IsRelease);
            },
            release =>
            {
                Assert.Equal("A", release.Key.Code);
                Assert.False(release.IsRepeat);
                Assert.True(release.IsRelease);
            },
            afterRelease =>
            {
                Assert.Equal("A", afterRelease.Key.Code);
                Assert.False(afterRelease.IsRepeat);
                Assert.False(afterRelease.IsRelease);
            });
    }

    private sealed class FakeKeyboardHookPlatform : IKeyboardHookPlatform
    {
        private KeyboardHookService.LowLevelKeyboardProc? _callback;

        public IntPtr SetHook(KeyboardHookService.LowLevelKeyboardProc callback)
        {
            _callback = callback;
            return 1;
        }

        public bool TryGetLastErrorMessage(out string message)
        {
            message = "";
            return false;
        }

        public bool Unhook(IntPtr hookId) => true;

        public IntPtr CallNextHook(IntPtr hookId, int nCode, IntPtr wParam, IntPtr lParam) => IntPtr.Zero;

        public void Send(int message, int virtualKey)
        {
            if (_callback is null)
            {
                throw new InvalidOperationException("Keyboard hook was not started.");
            }

            HookData data = new(virtualKey, 0, 0, 0, IntPtr.Zero);
            IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<HookData>());
            try
            {
                Marshal.StructureToPtr(data, pointer, false);
                _callback(0, message, pointer);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct HookData
    {
        public HookData(int vkCode, int scanCode, int flags, int time, IntPtr extraInfo)
        {
            VkCode = vkCode;
            ScanCode = scanCode;
            Flags = flags;
            Time = time;
            ExtraInfo = extraInfo;
        }

        public readonly int VkCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr ExtraInfo;
    }
}
