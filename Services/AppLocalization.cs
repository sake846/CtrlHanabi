using System.Globalization;
using CtrlHanabi.Models;

namespace CtrlHanabi.Services;

public enum UiLanguage
{
    Japanese,
    English
}

public sealed class AppLocalization
{
    private readonly UiLanguage _language;

    public AppLocalization(HanabiSettings settings)
    {
        _language = ResolveLanguage(settings.UiLanguage);
    }

    public string Menu_RunAtStartup => _language switch
    {
        UiLanguage.English => "Run at Windows startup",
        _ => "Windows起動時に実行"
    };

    public string Menu_HourlyStarmine => _language switch
    {
        UiLanguage.English => "Launch starmine every hour",
        _ => "毎時スターマインを上げる"
    };

    public string Menu_GpuPhysics(bool enabled) => _language switch
    {
        UiLanguage.English => $"GPU physics: {(enabled ? "Enabled" : "Disabled")}",
        _ => $"GPU物理演算: {(enabled ? "有効" : "無効")}"
    };

    public string Menu_ResetSettings => _language switch
    {
        UiLanguage.English => "Reset settings",
        _ => "設定をリセット"
    };

    public string Menu_Exit => _language switch
    {
        UiLanguage.English => "Exit",
        _ => "終了"
    };

    public string Menu_About => _language switch
    {
        UiLanguage.English => "About CtrlHanabi...",
        _ => "CtrlHanabi について..."
    };

    public string SettingsResetMessage => _language switch
    {
        UiLanguage.English => "Settings were reset to defaults.",
        _ => "設定を既定値に戻しました。"
    };

    public string ExitConfirmMessage => _language switch
    {
        UiLanguage.English => "Exit CtrlHanabi?",
        _ => "CtrlHanabi を終了しますか？"
    };

    public string About_Title => _language switch
    {
        UiLanguage.English => "CtrlHanabi — About",
        _ => "CtrlHanabi — このアプリについて"
    };

    public string About_VersionLabel => _language switch
    {
        UiLanguage.English => "Version",
        _ => "バージョン"
    };

    public string About_VersionUnknown => _language switch
    {
        UiLanguage.English => "Unknown",
        _ => "不明"
    };

    public static UiLanguage ResolveLanguage(string? configuredLanguage)
    {
        if (TryParseConfiguredLanguage(configuredLanguage, out var configured))
        {
            return configured;
        }

        return ResolveFromSystemCulture(CultureInfo.CurrentUICulture);
    }

    private static UiLanguage ResolveFromSystemCulture(CultureInfo culture)
    {
        for (var current = culture; current != CultureInfo.InvariantCulture; current = current.Parent)
        {
            if (string.Equals(current.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase))
            {
                return UiLanguage.Japanese;
            }

            if (string.Equals(current.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
            {
                return UiLanguage.English;
            }
        }

        return UiLanguage.English;
    }

    private static bool TryParseConfiguredLanguage(string? configuredLanguage, out UiLanguage language)
    {
        language = UiLanguage.English;
        if (string.IsNullOrWhiteSpace(configuredLanguage))
        {
            return false;
        }

        var normalized = configuredLanguage.Trim();
        if (string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(normalized, "ja", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "ja-JP", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "japanese", StringComparison.OrdinalIgnoreCase))
        {
            language = UiLanguage.Japanese;
            return true;
        }

        if (string.Equals(normalized, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "en-US", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "en-GB", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "english", StringComparison.OrdinalIgnoreCase))
        {
            language = UiLanguage.English;
            return true;
        }

        return false;
    }
}
