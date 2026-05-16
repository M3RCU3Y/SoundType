using SoundType.Input;

namespace SoundType.Tests;

public sealed class StartupReadinessTests
{
    [Fact]
    public void KeyboardHookStart_ReturnsFailure_WhenWindowsRegistrationFails()
    {
        KeyboardHookService hook = new(new FailingKeyboardHookPlatform("access denied"));

        KeyboardHookStartResult result = hook.Start();

        Assert.False(result.Started);
        Assert.Contains("access denied", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FailingKeyboardHookPlatform(string message) : IKeyboardHookPlatform
    {
        public IntPtr SetHook(KeyboardHookService.LowLevelKeyboardProc callback) => IntPtr.Zero;

        public bool TryGetLastErrorMessage(out string messageText)
        {
            messageText = message;
            return true;
        }

        public bool Unhook(IntPtr hookId) => true;

        public IntPtr CallNextHook(IntPtr hookId, int nCode, IntPtr wParam, IntPtr lParam) => IntPtr.Zero;
    }
}
