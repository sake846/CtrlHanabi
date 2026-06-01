using System.Drawing;
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
    private const string HourlyStarmineMenuText = "毎時スターマインを打ち上げ";
    private const string ResetSettingsMenuText = "設定をリセット";
    private const string ExitMenuText = "終了";
    private const string SettingsResetMessage = "設定を初期値に戻しました。";

    private readonly ISettingsService _settingsService;
    private readonly ICursorService _cursorService;
    private readonly IExitConfirmationService _exitConfirmationService;
    private readonly KeyboardDoubleTapDetector _detector;
    private readonly FireworkOverlayWindow _overlay;
    private readonly Icon _trayIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly int _tapThresholdMs;
    private readonly System.Threading.Timer _hourlyStarmineTimer;

    private DateTime _lastTrigger = DateTime.MinValue;
    private DateTime _lastHourlyStarmineHour = DateTime.MinValue;
    private CancellationTokenSource? _doubleTapCts;

    public AppController()
        : this(new SettingsService(), new WindowsCursorService(), new WindowsExitConfirmationService())
    {
    }

    internal AppController(ISettingsService settingsService, ICursorService cursorService, IExitConfirmationService exitConfirmationService)
    {
        _settingsService = settingsService;
        _cursorService = cursorService;
        _exitConfirmationService = exitConfirmationService;

        var settings = _settingsService.Load();
        _tapThresholdMs = settings.DoubleTapThresholdMs;
        _detector = new KeyboardDoubleTapDetector(settings.DoubleTapThresholdMs);
        _overlay = new FireworkOverlayWindow(settings, _settingsService);

        _detector.DoubleTapDetected += OnDoubleTapDetected;
        _detector.TripleTapDetected += OnTripleTapDetected;
        _detector.FiveTapDetected += OnFiveTapDetected;

        _trayIcon = LoadTrayIcon();
        _notifyIcon = CreateNotifyIcon();
        _hourlyStarmineTimer = new System.Threading.Timer(CheckHourlyStarmine, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start()
    {
        _detector.Start();
        _notifyIcon.Visible = true;
        _hourlyStarmineTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
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

        var mouse = _cursorService.GetCursorScreenPoint();

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
        var mouse = _cursorService.GetCursorScreenPoint();

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
            Icon = _trayIcon,
            Visible = false,
            ContextMenuStrip = BuildMenu()
        };
        return notifyIcon;
    }

    private static Icon LoadTrayIcon()
    {
        var icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        return icon is null ? SystemIcons.Information : (Icon)icon.Clone();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var settings = _settingsService.Load();

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

        var hourlyStarmineItem = new ToolStripMenuItem(HourlyStarmineMenuText)
        {
            Checked = settings.HourlyStarmineEnabled,
            CheckOnClick = true
        };
        hourlyStarmineItem.Click += (_, _) =>
        {
            var current = _settingsService.Load();
            _settingsService.Save(CopySettings(current, hourlyStarmineEnabled: hourlyStarmineItem.Checked));
        };

        var settingsItem = new ToolStripMenuItem(ResetSettingsMenuText);
        settingsItem.Click += (_, _) =>
        {
            _settingsService.Save(HanabiSettings.Default);
            hourlyStarmineItem.Checked = HanabiSettings.Default.HourlyStarmineEnabled;
            System.Windows.MessageBox.Show(SettingsResetMessage, AppName);
        };

        var exitItem = new ToolStripMenuItem(ExitMenuText);
        exitItem.Click += (_, _) => WpfApplication.Current.Dispatcher.Invoke(RequestExit);

        menu.Items.Add(launchItem);
        menu.Items.Add(hourlyStarmineItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private static HanabiSettings CopySettings(HanabiSettings settings, bool hourlyStarmineEnabled) => new()
    {
        DoubleTapThresholdMs = settings.DoubleTapThresholdMs,
        CooldownMs = settings.CooldownMs,
        ParticleCount = settings.ParticleCount,
        ExplosionRadius = settings.ExplosionRadius,
        HourlyStarmineEnabled = hourlyStarmineEnabled,
        StarmineLaneLeftEnabled = settings.StarmineLaneLeftEnabled,
        StarmineLaneCenterEnabled = settings.StarmineLaneCenterEnabled,
        StarmineLaneRightEnabled = settings.StarmineLaneRightEnabled
    };

    private void CheckHourlyStarmine(object? state)
    {
        var now = DateTime.Now;
        if (now.Minute != 59 || now.Second < 30 || now.Second > 34)
        {
            return;
        }

        var hourKey = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
        if (_lastHourlyStarmineHour == hourKey)
        {
            return;
        }

        if (!_settingsService.Load().HourlyStarmineEnabled)
        {
            return;
        }

        _lastHourlyStarmineHour = hourKey;
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            _overlay.ShowFirework(new System.Windows.Point(0, 0), forceStarmine: true);
        });
    }

    private void RequestExit()
    {
        var result = _exitConfirmationService.ConfirmExit(AppName);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _notifyIcon.Visible = false;
        WpfApplication.Current.Shutdown();
    }

    public void Dispose()
    {
        _detector.Dispose();
        _hourlyStarmineTimer.Dispose();
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
        _overlay.Close();
    }
}
