using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using CtrlHanabi.Services;

namespace CtrlHanabi.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AppLocalization localization)
    {
        InitializeComponent();
        Title = localization.About_Title;
        DataContext = new AboutViewModel(localization);
    }
}

internal class AboutViewModel
{
    private readonly AppLocalization _localization;

    public AboutViewModel(AppLocalization localization)
    {
        _localization = localization;
        CloseCommand = new RelayCommand(() => { /* closed via IsCancel */ });
        VersionText = ReadVersion(localization.About_VersionUnknown);
    }

    public string Description => "Ctrl キー押下エフェクトアプリ / Ctrl key press effect app";
    public string VersionLabel => $"{_localization.About_VersionLabel}:";
    public string VersionText { get; }
    public string CloseButtonText => "OK";
    public ICommand CloseCommand { get; }

    private static string ReadVersion(string fallback)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dir == null) return fallback;
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

internal class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => _execute();
}
