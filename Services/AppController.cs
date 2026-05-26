using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using CtrlHanabi.Models;
using WpfApplication = System.Windows.Application;

namespace CtrlHanabi.Services;

public sealed class AppController : IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly KeyboardDoubleTapDetector _detector;
    private readonly FireworkOverlayWindow _overlay;
    private readonly NotifyIcon _notifyIcon;

    private DateTime _lastTrigger = DateTime.MinValue;

    public AppController()
    {
        var settings = _settingsService.Load();
        _detector = new KeyboardDoubleTapDetector(settings.DoubleTapThresholdMs);
        _overlay = new FireworkOverlayWindow(settings);

        _detector.DoubleTapDetected += OnDoubleTapDetected;
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
        var settings = _settingsService.Load();
        if ((DateTime.UtcNow - _lastTrigger).TotalMilliseconds < settings.CooldownMs)
        {
            return;
        }

        _lastTrigger = DateTime.UtcNow;

        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var mouse = Cursor.Position;
            _overlay.ShowFirework(new System.Windows.Point(mouse.X, mouse.Y));
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
            Text = "CtrlHanabi",
            Icon = SystemIcons.Information,
            Visible = false,
            ContextMenuStrip = BuildMenu()
        };
        return notifyIcon;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var launchItem = new ToolStripMenuItem("Windows起動時に実行")
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

        var settingsItem = new ToolStripMenuItem("設定をリセット");
        settingsItem.Click += (_, _) =>
        {
            _settingsService.Save(HanabiSettings.Default);
            System.Windows.MessageBox.Show("設定を初期値に戻しました。", "CtrlHanabi");
        };

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => WpfApplication.Current.Dispatcher.Invoke(RequestExit);

        menu.Items.Add(launchItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void RequestExit()
    {
        var result = System.Windows.MessageBox.Show(
            "CtrlHanabiを終了しますか？",
            "CtrlHanabi",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _notifyIcon.Visible = false;
        WpfApplication.Current.Shutdown();
    }

    public void Dispose()
    {
        _detector.Dispose();
        _notifyIcon.Dispose();
        _overlay.Close();
    }
}
