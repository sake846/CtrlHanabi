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

    public UiLanguage Language => _language;

    public AppLocalization(HanabiSettings settings)
    {
        _language = ResolveLanguage(settings.UiLanguage);
    }

    // --- Tray Menu ---
    public string Menu_RunAtStartup => _language switch
    {
        UiLanguage.English => "Run at Windows startup (&R)",
        _ => "Windows 起動時に実行 (&R)"
    };

    public string Menu_HourlyStarmine => _language switch
    {
        UiLanguage.English => "Launch hourly starmine",
        _ => "毎時スターマインを上げる"
    };

    public string Menu_GpuPhysics(bool enabled) => _language switch
    {
        UiLanguage.English => $"GPU physics: {(enabled ? "Enabled" : "Disabled")}",
        _ => $"GPU物理演算: {(enabled ? "有効" : "無効")}"
    };

    public string Menu_Settings => _language switch
    {
        UiLanguage.English => "Settings (&S)...",
        _ => "設定 (&S)..."
    };

    public string Menu_ResetSettings => _language switch
    {
        UiLanguage.English => "Reset settings",
        _ => "設定をリセット"
    };

    public string Menu_Exit => _language switch
    {
        UiLanguage.English => "Exit (&X)",
        _ => "終了 (&X)"
    };

    public string Menu_About => _language switch
    {
        UiLanguage.English => "About CtrlHanabi (&A)...",
        _ => "CtrlHanabi について (&A)..."
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

    // --- Settings Window Titles & Headers ---
    public string Settings_Title => _language switch
    {
        UiLanguage.English => "CtrlHanabi — Settings",
        _ => "CtrlHanabi — 設定"
    };

    public string Settings_HeaderDescription => _language switch
    {
        UiLanguage.English => "Configure key triggers, visual effects, and launch behaviors.",
        _ => "キー連打による花火演出や動作を設定します。"
    };

    public string Settings_StatusActive => _language switch
    {
        UiLanguage.English => "Active (Resident)",
        _ => "常駐中（有効）"
    };

    public string Settings_StatusListening => _language switch
    {
        UiLanguage.English => "Listening for Ctrl taps",
        _ => "Ctrl キー入力待機中"
    };

    // --- Section 1: Key Input ---
    public string Settings_SectionKeyInput => _language switch
    {
        UiLanguage.English => "Key Input",
        _ => "キー操作"
    };

    public string Settings_KeyActionsHeader => _language switch
    {
        UiLanguage.English => "Assigned Key Actions",
        _ => "割り当てられた操作"
    };

    public string Settings_DoubleTapAction => _language switch
    {
        UiLanguage.English => "Ctrl x2: Launch firework at cursor",
        _ => "Ctrl 2 回: カーソル位置に花火打ち上げ"
    };

    public string Settings_TripleTapAction => _language switch
    {
        UiLanguage.English => "Ctrl x3: Launch starmine",
        _ => "Ctrl 3 回: スターマイン打ち上げ"
    };

    public string Settings_FiveTapAction => _language switch
    {
        UiLanguage.English => "Ctrl x5: Confirm app exit",
        _ => "Ctrl 5 回: アプリ終了確認"
    };

    public string Settings_DoubleTapThreshold => _language switch
    {
        UiLanguage.English => "Double-tap threshold",
        _ => "ダブルタップ判定時間"
    };

    public string Settings_DoubleTapThresholdHelp => _language switch
    {
        UiLanguage.English => "Max time between key taps to detect multi-press (ms)",
        _ => "連打として認識するキー入力の間隔上限 (ms)"
    };

    public string Settings_Cooldown => _language switch
    {
        UiLanguage.English => "Cooldown period",
        _ => "クールダウン時間"
    };

    public string Settings_CooldownHelp => _language switch
    {
        UiLanguage.English => "Minimum interval before next firework can be triggered (ms)",
        _ => "花火を再度打ち上げるまでの最小間隔 (ms)"
    };

    // --- Section 2: Fireworks Effects ---
    public string Settings_SectionEffects => _language switch
    {
        UiLanguage.English => "Fireworks Effects",
        _ => "花火演出"
    };

    public string Settings_ParticleCount => _language switch
    {
        UiLanguage.English => "Particle count",
        _ => "花火の粒子数"
    };

    public string Settings_ExplosionRadius => _language switch
    {
        UiLanguage.English => "Explosion radius",
        _ => "爆発半径"
    };

    public string Settings_GpuPhysics => _language switch
    {
        UiLanguage.English => "GPU physics acceleration",
        _ => "GPU 物理演算"
    };

    public string Settings_GpuPhysics_Active => _language switch
    {
        UiLanguage.English => "DirectX 11 Compute Shader (Active)",
        _ => "DirectX 11 計算シェーダー（有効）"
    };

    public string Settings_GpuPhysics_Inactive => _language switch
    {
        UiLanguage.English => "CPU Processing (Fallback)",
        _ => "CPU 処理（フォールバック）"
    };

    // --- Section 3: Starmine ---
    public string Settings_SectionStarmine => _language switch
    {
        UiLanguage.English => "Starmine",
        _ => "スターマイン"
    };

    public string Settings_HourlyStarmine => _language switch
    {
        UiLanguage.English => "Launch hourly starmine automatically (at :59:30)",
        _ => "毎時スターマインを自動打ち上げ（毎時 59 分 30 秒）"
    };

    public string Settings_StarmineLanes => _language switch
    {
        UiLanguage.English => "Launch lanes",
        _ => "打ち上げレーン"
    };

    public string Settings_LaneLeft => _language switch
    {
        UiLanguage.English => "Left lane",
        _ => "左レーン"
    };

    public string Settings_LaneCenter => _language switch
    {
        UiLanguage.English => "Center lane",
        _ => "中央レーン"
    };

    public string Settings_LaneRight => _language switch
    {
        UiLanguage.English => "Right lane",
        _ => "右レーン"
    };

    public string Settings_NoLaneError => _language switch
    {
        UiLanguage.English => "At least one launch lane must be selected.",
        _ => "少なくとも 1 つの打ち上げレーンを選択してください。"
    };

    public string Settings_TargetDisplay => _language switch
    {
        UiLanguage.English => "Target display",
        _ => "対象ディスプレイ"
    };

    public string DisplayPrefix => _language switch
    {
        UiLanguage.English => "Display",
        _ => "ディスプレイ"
    };

    public string PrimaryLabel => _language switch
    {
        UiLanguage.English => "Primary",
        _ => "プライマリ"
    };

    // --- Section 4: System / Behavior ---
    public string Settings_SectionSystem => _language switch
    {
        UiLanguage.English => "System",
        _ => "全般・システム"
    };

    public string Settings_RunAtStartup => _language switch
    {
        UiLanguage.English => "Run at Windows startup",
        _ => "Windows 起動時に実行"
    };

    public string Settings_Language => _language switch
    {
        UiLanguage.English => "Display language",
        _ => "表示言語"
    };

    public string Settings_LangAuto => _language switch
    {
        UiLanguage.English => "Auto (System default)",
        _ => "自動（OS の設定に従う）"
    };

    public string Settings_LangJa => "日本語";
    public string Settings_LangEn => "English";

    // --- Section 5: About ---
    public string Settings_SectionAbout => _language switch
    {
        UiLanguage.English => "About this app",
        _ => "このアプリについて"
    };

    public string Settings_VersionUnknown => _language switch
    {
        UiLanguage.English => "Unknown",
        _ => "不明"
    };

    // --- Section 6: Danger Zone ---
    public string Settings_SectionDanger => _language switch
    {
        UiLanguage.English => "Reset Settings",
        _ => "設定の初期化"
    };

    public string Settings_ResetButton => _language switch
    {
        UiLanguage.English => "Reset settings to defaults...",
        _ => "設定を既定値に戻す..."
    };

    public string Settings_ResetConfirmMessage => _language switch
    {
        UiLanguage.English => "Are you sure you want to reset all settings to default values?",
        _ => "すべての設定を初期値に戻しますか？"
    };

    // --- Buttons & Footer ---
    public string Settings_Save => _language switch
    {
        UiLanguage.English => "Save",
        _ => "保存"
    };

    public string Settings_Cancel => _language switch
    {
        UiLanguage.English => "Cancel",
        _ => "キャンセル"
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
