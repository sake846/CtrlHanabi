using WpfApplication = System.Windows.Application;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.IO;
using CtrlHanabi.Services;

namespace CtrlHanabi;

public partial class App : WpfApplication
{
    private const string SingleInstanceMutexName = @"Local\CtrlHanabi.SingleInstance";

    private AppController? _controller;
    private Mutex? _singleInstanceMutex;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        EnsureDefaultRuntimeFlags();
        WriteStartupDiagnostics();
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

    private static void WriteStartupDiagnostics()
    {
        if (!RuntimeLogging.IsD3D11LogEnabled())
        {
            return;
        }

        try
        {
            var logPath = RuntimeLogging.GetD3D11LogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var process = Process.GetCurrentProcess();
            var lines = new[]
            {
                "==== Startup Diagnostics ====",
                $"Time: {DateTime.Now:O}",
                $"ProcessPath: {Environment.ProcessPath ?? "(null)"}",
                $"MainModule: {process.MainModule?.FileName ?? "(null)"}",
                $"BaseDirectory: {AppContext.BaseDirectory}",
                $"CurrentDirectory: {Environment.CurrentDirectory}",
                $"CommandLine: {Environment.CommandLine}",
                $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
                $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
                $"BuildConfig: {GetBuildConfiguration()}",
                $"CTRLHANABI_LOG: {Environment.GetEnvironmentVariable("CTRLHANABI_LOG") ?? "(unset)"}",
                $"CTRLHANABI_D3D11: {Environment.GetEnvironmentVariable("CTRLHANABI_D3D11") ?? "(unset)"}",
                $"CTRLHANABI_D3D11_LOG: {Environment.GetEnvironmentVariable("CTRLHANABI_D3D11_LOG") ?? "(unset)"}",
                $"CTRLHANABI_GPU_PHYSICS: {Environment.GetEnvironmentVariable("CTRLHANABI_GPU_PHYSICS") ?? "(unset)"}",
                $"CTRLHANABI_GPU_PHYSICS_LOG: {Environment.GetEnvironmentVariable("CTRLHANABI_GPU_PHYSICS_LOG") ?? "(unset)"}",
                string.Empty
            };

            File.AppendAllLines(logPath, lines);
        }
        catch
        {
        }
    }

    private static void EnsureDefaultRuntimeFlags()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CTRLHANABI_D3D11")))
        {
            Environment.SetEnvironmentVariable("CTRLHANABI_D3D11", "1");
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CTRLHANABI_GPU_PHYSICS")))
        {
            Environment.SetEnvironmentVariable("CTRLHANABI_GPU_PHYSICS", "1");
        }
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
