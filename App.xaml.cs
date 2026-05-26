using WpfApplication = System.Windows.Application;
using System.Windows;
using CtrlHanabi.Services;

namespace CtrlHanabi;

public partial class App : WpfApplication
{
    private AppController? _controller;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _controller = new AppController();
        _controller.Start();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _controller?.Dispose();
    }
}
