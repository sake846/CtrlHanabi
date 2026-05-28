using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CtrlHanabi.Services;

public sealed class KeyboardDoubleTapDetector : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int KeyDownMask = 0x8000;
    private const int PollingIntervalMs = 15;
    private const int DuplicateInputGuardMs = 40;

    private readonly int _thresholdMs;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly Lock _syncRoot = new();
    private nint _hookId;
    private System.Threading.Timer? _pollingTimer;
    private DateTime _lastCtrlDownUtc = DateTime.MinValue;
    private DateTime _lastObservedCtrlDownUtc = DateTime.MinValue;
    private int _ctrlTapCount;
    private bool _isLeftCtrlDown;
    private bool _isRightCtrlDown;

    public event EventHandler? DoubleTapDetected;
    public event EventHandler? FiveTapDetected;

    public KeyboardDoubleTapDetector(int thresholdMs)
    {
        _thresholdMs = thresholdMs;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId == 0)
        {
            _hookId = NativeMethods.SetHook(_proc);
        }

        _pollingTimer ??= new System.Threading.Timer(PollKeyboardState, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(PollingIntervalMs));
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode is VkLControl or VkRControl)
            {
                if ((wParam == WmKeyDown || wParam == WmSysKeyDown) && MarkCtrlDown(vkCode))
                {
                    ProcessCtrlDown(DateTime.UtcNow);
                }
                else if (wParam == WmKeyUp || wParam == WmSysKeyUp)
                {
                    MarkCtrlUp(vkCode);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void PollKeyboardState(object? state)
    {
        var isLeftCtrlDown = IsKeyDown(VkLControl);
        var isRightCtrlDown = IsKeyDown(VkRControl);
        var shouldProcessCtrlDown = false;

        lock (_syncRoot)
        {
            var wasCtrlUp = !_isLeftCtrlDown && !_isRightCtrlDown;
            var isCtrlDown = isLeftCtrlDown || isRightCtrlDown;

            _isLeftCtrlDown = isLeftCtrlDown;
            _isRightCtrlDown = isRightCtrlDown;

            shouldProcessCtrlDown = wasCtrlUp && isCtrlDown;
        }

        if (shouldProcessCtrlDown)
        {
            ProcessCtrlDown(DateTime.UtcNow);
        }
    }

    private bool MarkCtrlDown(int virtualKeyCode)
    {
        lock (_syncRoot)
        {
            if (virtualKeyCode == VkLControl)
            {
                if (_isLeftCtrlDown)
                {
                    return false;
                }

                _isLeftCtrlDown = true;
                return true;
            }

            if (_isRightCtrlDown)
            {
                return false;
            }

            _isRightCtrlDown = true;
            return true;
        }
    }

    private void MarkCtrlUp(int virtualKeyCode)
    {
        lock (_syncRoot)
        {
            if (virtualKeyCode == VkLControl)
            {
                _isLeftCtrlDown = false;
            }
            else
            {
                _isRightCtrlDown = false;
            }
        }
    }

    private void ProcessCtrlDown(DateTime now)
    {
        EventHandler? doubleTapDetected = null;
        EventHandler? fiveTapDetected = null;

        lock (_syncRoot)
        {
            if ((now - _lastObservedCtrlDownUtc).TotalMilliseconds < DuplicateInputGuardMs)
            {
                return;
            }

            _lastObservedCtrlDownUtc = now;
            if ((now - _lastCtrlDownUtc).TotalMilliseconds <= _thresholdMs)
            {
                _ctrlTapCount++;

                if (_ctrlTapCount == 2)
                {
                    doubleTapDetected = DoubleTapDetected;
                }

                if (_ctrlTapCount >= 5)
                {
                    fiveTapDetected = FiveTapDetected;
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

        doubleTapDetected?.Invoke(this, EventArgs.Empty);
        fiveTapDetected?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsKeyDown(int virtualKeyCode)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKeyCode) & KeyDownMask) != 0;
    }

    public void Dispose()
    {
        _pollingTimer?.Dispose();
        _pollingTimer = null;

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

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

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
