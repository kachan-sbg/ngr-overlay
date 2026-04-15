using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UserControl = System.Windows.Controls.UserControl;
using NrgOverlay.Core.Config;

namespace NrgOverlay.App.Settings;

/// <summary>
/// Panel shown when "Global Settings" is selected in the sidebar.
/// Manages Edit Mode, Stream Mode, and Start With Windows.
/// </summary>
public partial class GlobalSettingsPanel : UserControl
{
    private const string RunKey       = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "NrgOverlay";

    private readonly OverlayManager _overlayManager;
    private readonly AppConfig      _appConfig;
    private readonly ConfigStore    _configStore;

    // Suppress feedback loops while we're loading state.
    private bool _loading;

    public GlobalSettingsPanel(OverlayManager overlayManager, AppConfig appConfig, ConfigStore configStore)
    {
        InitializeComponent();

        _overlayManager = overlayManager;
        _appConfig      = appConfig;
        _configStore    = configStore;

        Reload();
    }

    // в”Ђв”Ђ Public в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Refreshes UI state from the live config / manager. Called by
    /// <see cref="SettingsWindow.OpenOrActivate"/>.
    /// </summary>
    public void Reload()
    {
        _loading = true;

        EditModeCheck.IsChecked         = _overlayManager.EditModeActive;
        StreamModeCheck.IsChecked       = _appConfig.GlobalSettings.StreamModeActive;
        StartWithWindowsCheck.IsChecked = IsStartWithWindowsEnabled();

        _loading = false;
    }

    /// <summary>
    /// Persists the Start With Windows preference. Edit/stream mode changes are
    /// applied immediately via their event handlers, so Apply just handles the
    /// registry entry and saves config.
    /// </summary>
    public void Apply()
    {
        if (_loading) return;

        _appConfig.GlobalSettings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        SetStartWithWindows(_appConfig.GlobalSettings.StartWithWindows);
        _configStore.Save(_appConfig);
    }

    // в”Ђв”Ђ Event handlers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void EditMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _overlayManager.SetEditMode(EditModeCheck.IsChecked == true);
    }

    private void StreamMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _overlayManager.SetStreamMode(StreamModeCheck.IsChecked == true);
    }

    // в”Ђв”Ђ Registry helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(RunValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath
                    ?? System.AppContext.BaseDirectory + AppDomain.CurrentDomain.FriendlyName + ".exe";
                key.SetValue(RunValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-fatal вЂ” registry write may fail in restricted environments.
        }
    }
}

