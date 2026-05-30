using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using CtrlHanabi.Models;
using WpfApplication = System.Windows.Application;
using System.Threading;
using System.Threading.Tasks;

namespace CtrlHanabi.Services;

public sealed class AppController : IDisposable
{
    private const string AppName = "CtrlHanabi";
    private const string AutoStartMenuText = "Windows起動時に実行";
    private const string ResetSettingsMenuText = "設定をリセット";
    private const string ExitMenuText = "終了";
    private const string SettingsResetMessage = "設定を初期値に戻しました。";

    private readonly SettingsService _settingsService = new();
    private readonly KeyboardDoubleTapDetector _detector;
    private readonly FireworkOverlayWindow _overlay;
    private readonly NotifyIcon _notifyIcon;
    private readonly int _tapThresholdMs;

    private DateTime _lastTrigger = DateTime.MinValue;
    private CancellationTokenSource? _doubleTapCts;

    public AppController()
    {
        var settings = _settingsService.Load();
        _tapThresholdMs = settings.DoubleTapThresholdMs;
        _detector = new KeyboardDoubleTapDetector(settings.DoubleTapThresholdMs);
        _overlay = new FireworkOverlayWindow(settings);

        _detector.DoubleTapDetected += OnDoubleTapDetected;
        _detector.TripleTapDetected += OnTripleTapDetected;
        _detector.FiveTapDetected += OnFiveTapDetected;

        _notifyIcon = CreateNotifyIcon();
    }

    public void Start()
    {
        _detector.Start();
        _notifyIcon.Visible = true;
    }

    private void OnDoubleTapDetected(object? sender, EventArgs e)
    {
        _doubleTapCts?.Cancel();
        _doubleTapCts = new CancellationTokenSource();
        _ = FireDoubleTapAfterGraceAsync(_doubleTapCts.Token);
    }

    private void OnTripleTapDetected(object? sender, EventArgs e)
    {
        _doubleTapCts?.Cancel();

        var settings = _settingsService.Load();
        if ((DateTime.UtcNow - _lastTrigger).TotalMilliseconds < settings.CooldownMs)
        {
            return;
        }

        _lastTrigger = DateTime.UtcNow;

        var mouse = NativeMethods.GetCursorScreenPoint();

        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            _overlay.ShowFirework(mouse, forceStarmine: true);
        });
    }

    private async Task FireDoubleTapAfterGraceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_tapThresholdMs + 10, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var settings = _settingsService.Load();
        if ((DateTime.UtcNow - _lastTrigger).TotalMilliseconds < settings.CooldownMs)
        {
            return;
        }

        _lastTrigger = DateTime.UtcNow;
        var mouse = NativeMethods.GetCursorScreenPoint();

        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            _overlay.ShowFirework(mouse, forceStarmine: false);
        });
    }

    private void OnFiveTapDetected(object? sender, EventArgs e)
    {
        WpfApplication.Current.Dispatcher.Invoke(RequestExit);
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var notifyIcon = new NotifyIcon
        {
            Text = AppName,
            Icon = SystemIcons.Information,
            Visible = false,
            ContextMenuStrip = BuildMenu()
        };
        return notifyIcon;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var launchItem = new ToolStripMenuItem(AutoStartMenuText)
        {
            Checked = AutoStartService.IsEnabled(),
            CheckOnClick = true
        };
        launchItem.Click += (_, _) =>
        {
            if (launchItem.Checked)
            {
                AutoStartService.Enable();
            }
            else
            {
                AutoStartService.Disable();
            }
        };

        var settingsItem = new ToolStripMenuItem(ResetSettingsMenuText);
        settingsItem.Click += (_, _) =>
        {
            _settingsService.Save(HanabiSettings.Default);
            System.Windows.MessageBox.Show(SettingsResetMessage, AppName);
        };

        var exitItem = new ToolStripMenuItem(ExitMenuText);
        exitItem.Click += (_, _) => WpfApplication.Current.Dispatcher.Invoke(RequestExit);

        menu.Items.Add(launchItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void RequestExit()
    {
        var result = NativeMethods.ShowTopmostExitConfirmation();

        if (result != DialogResult.Yes)
        {
            return;
        }

        _notifyIcon.Visible = false;
        WpfApplication.Current.Shutdown();
    }

    private static class NativeMethods
    {
        private const uint MbIconQuestion = 0x00000020;
        private const uint MbYesNo = 0x00000004;
        private const uint MbSetForeground = 0x00010000;
        private const uint MbTopmost = 0x00040000;
        private const int IdYes = 6;
        private const string ExitConfirmMessage = "CtrlHanabiを終了しますか？";

        public static DialogResult ShowTopmostExitConfirmation()
        {
            var result = MessageBox(
                nint.Zero,
                ExitConfirmMessage,
                AppName,
                MbYesNo | MbIconQuestion | MbSetForeground | MbTopmost);

            return result == IdYes ? DialogResult.Yes : DialogResult.No;
        }

        public static System.Windows.Point GetCursorScreenPoint()
        {
            if (GetPhysicalCursorPos(out var point) || GetCursorPos(out point))
            {
                return new System.Windows.Point(point.X, point.Y);
            }

            var fallback = System.Windows.Forms.Cursor.Position;
            return new System.Windows.Point(fallback.X, fallback.Y);
        }

        [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBox(nint hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicalCursorPos(out CursorPoint point);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out CursorPoint point);

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint
        {
            public int X;
            public int Y;
        }
    }

    public void Dispose()
    {
        _detector.Dispose();
        _notifyIcon.Dispose();
        _overlay.Close();
    }
}
