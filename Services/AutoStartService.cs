using Microsoft.Win32;

namespace CtrlHanabi.Services;

public static class AutoStartService
{
    private const string AppName = "CtrlHanabi";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const byte StartupApprovedEnabled = 0x02;
    private const byte StartupApprovedDisabled = 0x03;

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        if (key?.GetValue(AppName) is not string runValue || string.IsNullOrWhiteSpace(runValue))
        {
            return false;
        }

        return !IsStartupApprovedDisabled();
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        var exePath = Environment.ProcessPath;
        if (key is null || string.IsNullOrWhiteSpace(exePath))
        {
            return;
        }

        key.SetValue(AppName, BuildQuotedPath(exePath));
        SetStartupApprovedState(StartupApprovedEnabled);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, false);
        SetStartupApprovedState(StartupApprovedDisabled);
    }

    private static string BuildQuotedPath(string path) => $"\"{path}\"";

    private static bool IsStartupApprovedDisabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKeyPath, false);
        if (key?.GetValue(AppName) is not byte[] state || state.Length == 0)
        {
            return false;
        }

        return state[0] == StartupApprovedDisabled;
    }

    private static void SetStartupApprovedState(byte state)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupApprovedRunKeyPath, true);
        if (key is null)
        {
            return;
        }

        if (key.GetValue(AppName) is byte[] existingState && existingState.Length > 0)
        {
            existingState[0] = state;
            key.SetValue(AppName, existingState, RegistryValueKind.Binary);
            return;
        }

        key.SetValue(AppName, new byte[12] { state, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
    }
}
