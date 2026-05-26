using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CtrlHanabi.Services;

public sealed class KeyboardDoubleTapDetector : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;

    private readonly int _thresholdMs;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private nint _hookId;
    private DateTime _lastCtrlDownUtc = DateTime.MinValue;
    private int _ctrlTapCount;

    public event EventHandler? DoubleTapDetected;
    public event EventHandler? FiveTapDetected;

    public KeyboardDoubleTapDetector(int thresholdMs)
    {
        _thresholdMs = thresholdMs;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != 0) return;
        _hookId = NativeMethods.SetHook(_proc);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WmKeyDown)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode is VkLControl or VkRControl)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastCtrlDownUtc).TotalMilliseconds <= _thresholdMs)
                {
                    _ctrlTapCount++;

                    if (_ctrlTapCount == 2)
                    {
                        DoubleTapDetected?.Invoke(this, EventArgs.Empty);
                    }

                    if (_ctrlTapCount >= 5)
                    {
                        FiveTapDetected?.Invoke(this, EventArgs.Empty);
                        _ctrlTapCount = 0;
                        _lastCtrlDownUtc = DateTime.MinValue;
                    }
                    else
                    {
                        _lastCtrlDownUtc = now;
                    }
                }
                else
                {
                    _ctrlTapCount = 1;
                    _lastCtrlDownUtc = now;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = 0;
        }
    }

    private static class NativeMethods
    {
        public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint GetModuleHandle(string? lpModuleName);

        public static nint SetHook(LowLevelKeyboardProc proc)
        {
            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            return SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(currentModule?.ModuleName), 0);
        }
    }
}
