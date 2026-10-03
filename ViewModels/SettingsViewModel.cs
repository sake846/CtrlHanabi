using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using FormsScreen = System.Windows.Forms.Screen;
using CtrlHanabi.Models;
using CtrlHanabi.Services;

namespace CtrlHanabi.ViewModels;

public sealed class DisplayOptionViewModel
{
    public int DisplayIndex { get; }
    public string DisplayName { get; }

    public DisplayOptionViewModel(int displayIndex, FormsScreen screen, AppLocalization localization)
    {
        DisplayIndex = displayIndex;
        var primarySuffix = screen.Primary ? $" ({localization.PrimaryLabel})" : string.Empty;
        DisplayName = $"{localization.DisplayPrefix} {displayIndex}{primarySuffix} [{screen.Bounds.Width}x{screen.Bounds.Height}]";
    }
}

public sealed class LanguageOptionViewModel
{
    public string? Code { get; }
    public string DisplayText { get; }

    public LanguageOptionViewModel(string? code, string displayText)
    {
        Code = code;
        DisplayText = displayText;
    }
}

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly AppLocalization _localization;
    private readonly HanabiSettings _initialSettings;
    private readonly bool _isGpuPhysicsActive;

    private int _doubleTapThresholdMs;
    private int _cooldownMs;
    private int _particleCount;
    private double _explosionRadius;
    private bool _hourlyStarmineEnabled;
    private bool _starmineLaneLeftEnabled;
    private bool _starmineLaneCenterEnabled;
    private bool _starmineLaneRightEnabled;
    private DisplayOptionViewModel? _selectedDisplay;
    private bool _runAtStartup;
    private LanguageOptionViewModel? _selectedLanguage;
    private bool _hasLaneError;

    public event Action<bool>? RequestClose;

    // ---- Localization properties ----
    public string Title => _localization.Settings_Title;
    public string HeaderDescription => _localization.Settings_HeaderDescription;
    public string StatusText => $"{_localization.Settings_StatusActive} — {_localization.Settings_StatusListening}";

    // Section 1: Key Input
    public string SectionKeyInputHeader => _localization.Settings_SectionKeyInput;
    public string KeyActionsHeader => _localization.Settings_KeyActionsHeader;
    public string DoubleTapActionText => _localization.Settings_DoubleTapAction;
    public string TripleTapActionText => _localization.Settings_TripleTapAction;
    public string FiveTapActionText => _localization.Settings_FiveTapAction;
    public string DoubleTapThresholdLabel => _localization.Settings_DoubleTapThreshold;
    public string DoubleTapThresholdHelp => _localization.Settings_DoubleTapThresholdHelp;
    public string CooldownLabel => _localization.Settings_Cooldown;
    public string CooldownHelp => _localization.Settings_CooldownHelp;

    // Section 2: Fireworks Effects
    public string SectionEffectsHeader => _localization.Settings_SectionEffects;
    public string ParticleCountLabel => _localization.Settings_ParticleCount;
    public string ExplosionRadiusLabel => _localization.Settings_ExplosionRadius;
    public string GpuPhysicsLabel => _localization.Settings_GpuPhysics;
    public string GpuPhysicsStatusText => _isGpuPhysicsActive
        ? _localization.Settings_GpuPhysics_Active
        : _localization.Settings_GpuPhysics_Inactive;

    // Section 3: Starmine
    public string SectionStarmineHeader => _localization.Settings_SectionStarmine;
    public string HourlyStarmineLabel => _localization.Settings_HourlyStarmine;
    public string StarmineLanesHeader => _localization.Settings_StarmineLanes;
    public string LaneLeftLabel => _localization.Settings_LaneLeft;
    public string LaneCenterLabel => _localization.Settings_LaneCenter;
    public string LaneRightLabel => _localization.Settings_LaneRight;
    public string NoLaneErrorMessage => _localization.Settings_NoLaneError;
    public string TargetDisplayHeader => _localization.Settings_TargetDisplay;

    // Section 4: System
    public string SectionSystemHeader => _localization.Settings_SectionSystem;
    public string RunAtStartupLabel => _localization.Settings_RunAtStartup;
    public string LanguageHeader => _localization.Settings_Language;

    // Section 5: About
    public string SectionAboutHeader => _localization.Settings_SectionAbout;
    public string VersionLabel => $"{_localization.About_VersionLabel}:";
    public string VersionText { get; }

    // Section 6: Danger Zone
    public string SectionDangerHeader => _localization.Settings_SectionDanger;
    public string ResetButtonText => _localization.Settings_ResetButton;

    // Buttons
    public string SaveButtonText => _localization.Settings_Save;
    public string CancelButtonText => _localization.Settings_Cancel;

    // ---- Settings bindings ----
    public int DoubleTapThresholdMs
    {
        get => _doubleTapThresholdMs;
        set => SetProperty(ref _doubleTapThresholdMs, value);
    }

    public int CooldownMs
    {
        get => _cooldownMs;
        set => SetProperty(ref _cooldownMs, value);
    }

    public int ParticleCount
    {
        get => _particleCount;
        set => SetProperty(ref _particleCount, value);
    }

    public double ExplosionRadius
    {
        get => _explosionRadius;
        set => SetProperty(ref _explosionRadius, value);
    }

    public bool HourlyStarmineEnabled
    {
        get => _hourlyStarmineEnabled;
        set => SetProperty(ref _hourlyStarmineEnabled, value);
    }

    public bool StarmineLaneLeftEnabled
    {
        get => _starmineLaneLeftEnabled;
        set
        {
            if (SetProperty(ref _starmineLaneLeftEnabled, value) && value && _hasLaneError)
            {
                HasLaneError = false;
            }
        }
    }

    public bool StarmineLaneCenterEnabled
    {
        get => _starmineLaneCenterEnabled;
        set
        {
            if (SetProperty(ref _starmineLaneCenterEnabled, value) && value && _hasLaneError)
            {
                HasLaneError = false;
            }
        }
    }

    public bool StarmineLaneRightEnabled
    {
        get => _starmineLaneRightEnabled;
        set
        {
            if (SetProperty(ref _starmineLaneRightEnabled, value) && value && _hasLaneError)
            {
                HasLaneError = false;
            }
        }
    }

    public bool HasLaneError
    {
        get => _hasLaneError;
        private set
        {
            if (SetProperty(ref _hasLaneError, value))
            {
                OnPropertyChanged(nameof(NoLaneErrorVisible));
            }
        }
    }

    public Visibility NoLaneErrorVisible => _hasLaneError ? Visibility.Visible : Visibility.Collapsed;

    public List<DisplayOptionViewModel> Displays { get; }
    public DisplayOptionViewModel? SelectedDisplay
    {
        get => _selectedDisplay;
        set => SetProperty(ref _selectedDisplay, value);
    }

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set => SetProperty(ref _runAtStartup, value);
    }

    public List<LanguageOptionViewModel> LanguageOptions { get; }
    public LanguageOptionViewModel? SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    // ---- Commands ----
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, AppLocalization localization, bool isGpuPhysicsActive)
    {
        _settingsService = settingsService;
        _localization = localization;
        _isGpuPhysicsActive = isGpuPhysicsActive;
        _initialSettings = _settingsService.Load();

        _doubleTapThresholdMs = _initialSettings.DoubleTapThresholdMs;
        _cooldownMs = _initialSettings.CooldownMs;
        _particleCount = _initialSettings.ParticleCount;
        _explosionRadius = _initialSettings.ExplosionRadius;
        _hourlyStarmineEnabled = _initialSettings.HourlyStarmineEnabled;
        _starmineLaneLeftEnabled = _initialSettings.StarmineLaneLeftEnabled;
        _starmineLaneCenterEnabled = _initialSettings.StarmineLaneCenterEnabled;
        _starmineLaneRightEnabled = _initialSettings.StarmineLaneRightEnabled;
        _runAtStartup = AutoStartService.IsEnabled();

        // Populate displays in same order as FireworkOverlayWindow
        var screens = FormsScreen.AllScreens;
        var orderedScreens = screens
            .OrderByDescending(s => s.Primary)
            .ThenBy(s => s.Bounds.Left)
            .ThenBy(s => s.Bounds.Top)
            .ToArray();

        Displays = new List<DisplayOptionViewModel>();
        for (var i = 0; i < orderedScreens.Length; i++)
        {
            Displays.Add(new DisplayOptionViewModel(i + 1, orderedScreens[i], _localization));
        }

        var targetDisplayIndex = _initialSettings.StarmineDisplayIndex;
        SelectedDisplay = Displays.FirstOrDefault(d => d.DisplayIndex == targetDisplayIndex)
            ?? Displays.FirstOrDefault();

        // Populate language options
        LanguageOptions =
        [
            new(null, _localization.Settings_LangAuto),
            new("ja", _localization.Settings_LangJa),
            new("en", _localization.Settings_LangEn)
        ];

        SelectedLanguage = LanguageOptions.FirstOrDefault(l =>
            string.Equals(l.Code, _initialSettings.UiLanguage, StringComparison.OrdinalIgnoreCase))
            ?? LanguageOptions[0];

        VersionText = AppVersionProvider.GetVersion(_localization.Settings_VersionUnknown);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        ResetCommand = new RelayCommand(ResetToDefaults);
    }

    private void Save()
    {
        // Validation: at least one lane must be enabled
        if (!StarmineLaneLeftEnabled && !StarmineLaneCenterEnabled && !StarmineLaneRightEnabled)
        {
            HasLaneError = true;
            return;
        }

        HasLaneError = false;

        var newSettings = new HanabiSettings
        {
            DoubleTapThresholdMs = DoubleTapThresholdMs,
            CooldownMs = CooldownMs,
            ParticleCount = ParticleCount,
            ExplosionRadius = ExplosionRadius,
            HourlyStarmineEnabled = HourlyStarmineEnabled,
            StarmineLaneLeftEnabled = StarmineLaneLeftEnabled,
            StarmineLaneCenterEnabled = StarmineLaneCenterEnabled,
            StarmineLaneRightEnabled = StarmineLaneRightEnabled,
            StarmineDisplayIndex = SelectedDisplay?.DisplayIndex ?? 1,
            UiLanguage = SelectedLanguage?.Code
        };

        _settingsService.Save(newSettings);

        // Update startup registration if changed
        var currentAutoStart = AutoStartService.IsEnabled();
        if (RunAtStartup != currentAutoStart)
        {
            if (RunAtStartup)
            {
                AutoStartService.Enable();
            }
            else
            {
                AutoStartService.Disable();
            }
        }

        RequestClose?.Invoke(true);
    }

    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void ResetToDefaults()
    {
        var result = System.Windows.MessageBox.Show(
            _localization.Settings_ResetConfirmMessage,
            "CtrlHanabi",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var defaults = HanabiSettings.Default;
        DoubleTapThresholdMs = defaults.DoubleTapThresholdMs;
        CooldownMs = defaults.CooldownMs;
        ParticleCount = defaults.ParticleCount;
        ExplosionRadius = defaults.ExplosionRadius;
        HourlyStarmineEnabled = defaults.HourlyStarmineEnabled;
        StarmineLaneLeftEnabled = defaults.StarmineLaneLeftEnabled;
        StarmineLaneCenterEnabled = defaults.StarmineLaneCenterEnabled;
        StarmineLaneRightEnabled = defaults.StarmineLaneRightEnabled;
        SelectedDisplay = Displays.FirstOrDefault(d => d.DisplayIndex == defaults.StarmineDisplayIndex)
            ?? Displays.FirstOrDefault();
        SelectedLanguage = LanguageOptions[0];
        HasLaneError = false;
    }
}
