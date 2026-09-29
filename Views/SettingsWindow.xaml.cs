using System.Windows;
using CtrlHanabi.Services;
using CtrlHanabi.ViewModels;

namespace CtrlHanabi.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ISettingsService settingsService, AppLocalization localization, bool isGpuPhysicsActive)
    {
        InitializeComponent();

        var vm = new SettingsViewModel(settingsService, localization, isGpuPhysicsActive);
        Title = vm.Title;
        vm.RequestClose += (result) =>
        {
            DialogResult = result;
            Close();
        };
        DataContext = vm;

        SourceInitialized += (s, e) =>
        {
            ThemeManager.ApplyWindowTheme(this);
        };
    }
}
