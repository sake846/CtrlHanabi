using Microsoft.Win32;

namespace CtrlHanabi.Services;

public static class AutoStartService
{
    private const string AppName = "CtrlHanabi";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) is string;
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        var exePath = Environment.ProcessPath;
        if (key is null || string.IsNullOrWhiteSpace(exePath))
        {
            return;
        }

        key.SetValue(AppName, BuildQuotedPath(exePath));
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, false);
    }

    private static string BuildQuotedPath(string path) => $"\"{path}\"";
}