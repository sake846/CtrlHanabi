using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;

namespace CtrlHanabi.Services;

public static class ThemeManager
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static bool IsDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            if (key?.GetValue("AppsUseLightTheme") is int appUsesLight)
            {
                return appUsesLight == 0;
            }
            if (key?.GetValue("SystemUsesLightTheme") is int sysUsesLight)
            {
                return sysUsesLight == 0;
            }
        }
        catch
        {
            // Fallback to light
        }

        return false;
    }

    public static void ApplyTheme(ResourceDictionary resources)
    {
        bool isDark = IsDarkTheme();

        if (isDark)
        {
            // Windows 11 Dark Mode Palette
            resources["Brush.Surface"]      = new SolidColorBrush(MediaColor.FromRgb(0x20, 0x20, 0x20));
            resources["Brush.SurfaceAlt"]   = new SolidColorBrush(MediaColor.FromRgb(0x2C, 0x2C, 0x2C));
            resources["Brush.Text"]         = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            resources["Brush.TextMuted"]    = new SolidColorBrush(MediaColor.FromRgb(0xA6, 0xA6, 0xA6));
            resources["Brush.Border"]       = new SolidColorBrush(MediaColor.FromRgb(0x3E, 0x3E, 0x3E));
            resources["Brush.Accent"]       = new SolidColorBrush(MediaColor.FromRgb(0x00, 0x78, 0xD4));
            resources["Brush.AccentText"]   = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            resources["Brush.Danger"]       = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x6B, 0x6B));
            resources["Brush.Success"]      = new SolidColorBrush(MediaColor.FromRgb(0x6C, 0xC6, 0x44));
            resources["Brush.Overlay"]      = new SolidColorBrush(MediaColor.FromArgb(0xDC, 0x1A, 0x1A, 0x1A));
        }
        else
        {
            // Windows 11 Light Mode Palette
            resources["Brush.Surface"]      = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            resources["Brush.SurfaceAlt"]   = new SolidColorBrush(MediaColor.FromRgb(0xF5, 0xF5, 0xF5));
            resources["Brush.Text"]         = new SolidColorBrush(MediaColor.FromRgb(0x1A, 0x1A, 0x1A));
            resources["Brush.TextMuted"]    = new SolidColorBrush(MediaColor.FromRgb(0x66, 0x66, 0x66));
            resources["Brush.Border"]       = new SolidColorBrush(MediaColor.FromRgb(0xDC, 0xDC, 0xDC));
            resources["Brush.Accent"]       = new SolidColorBrush(MediaColor.FromRgb(0x00, 0x67, 0xC0));
            resources["Brush.AccentText"]   = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
            resources["Brush.Danger"]       = new SolidColorBrush(MediaColor.FromRgb(0xC4, 0x2B, 0x1C));
            resources["Brush.Success"]      = new SolidColorBrush(MediaColor.FromRgb(0x10, 0x7C, 0x41));
            resources["Brush.Overlay"]      = new SolidColorBrush(MediaColor.FromArgb(0xCC, 0x00, 0x00, 0x00));
        }
    }

    public static void ApplyWindowTheme(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int isDark = IsDarkTheme() ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDark, sizeof(int));
        }
        catch
        {
            // Ignore if DWM attribute is not supported
        }
    }
}
