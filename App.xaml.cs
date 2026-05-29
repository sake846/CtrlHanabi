using WpfApplication = System.Windows.Application;
using System.Windows;
using System.Threading;
using CtrlHanabi.Services;

namespace CtrlHanabi;

public partial class App : WpfApplication
{
    private const string SingleInstanceMutexName = @"Local\CtrlHanabi.SingleInstance";

    private AppController? _controller;
    private Mutex? _singleInstanceMutex;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (!TryAcquireSingleInstanceMutex())
        {
            Shutdown();
            return;
        }

        _controller = new AppController();
        _controller.Start();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _controller?.Dispose();
        ReleaseSingleInstanceMutex();
    }

    private bool TryAcquireSingleInstanceMutex()
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private void ReleaseSingleInstanceMutex()
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
    }
}
