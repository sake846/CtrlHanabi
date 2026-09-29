using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using CtrlHanabi.Services;
using CtrlHanabi.ViewModels;

namespace CtrlHanabi.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AppLocalization localization)
    {
        InitializeComponent();
        Title = localization.About_Title;
        DataContext = new AboutViewModel(localization, this);

        SourceInitialized += (s, e) =>
        {
            ThemeManager.ApplyWindowTheme(this);
        };
    }
}

internal class AboutViewModel
{
    private readonly AppLocalization _localization;
    private readonly Window _window;

    public AboutViewModel(AppLocalization localization, Window window)
    {
        _localization = localization;
        _window = window;
        CloseCommand = new RelayCommand(() => _window.Close());
        VersionText = ReadVersion(localization.About_VersionUnknown);
    }

    public string Description => _localization.Language switch
    {
        UiLanguage.English => "Ctrl key tap effect app",
        _ => "Ctrl キー連打エフェクトアプリ"
    };

    public string VersionLabel => $"{_localization.About_VersionLabel}:";
    public string VersionText { get; }
    public string CloseButtonText => "OK";
    public ICommand CloseCommand { get; }

    private static string ReadVersion(string fallback)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;

            if (string.IsNullOrEmpty(dir)) return fallback;

            string file = Path.Combine(dir, "version.txt");
            if (!File.Exists(file)) return fallback;

            string raw = File.ReadAllText(file).Trim();
            return string.IsNullOrEmpty(raw) ? fallback : raw;
        }
        catch
        {
            return fallback;
        }
    }
}
