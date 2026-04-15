using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NrgOverlay.Core.Config;
using CheckBox = System.Windows.Controls.CheckBox;

namespace NrgOverlay.App.Settings;

/// <summary>
/// Settings window вЂ” lazy singleton; call <see cref="OpenOrActivate"/> to show.
/// Sidebar selects which panel is shown in the content area.
/// Apply button в†’ <see cref="OverlayManager.ApplyConfig"/> (saves to disk).
/// LostFocus on any panel field в†’ <see cref="OverlayManager.PreviewConfig"/> (no save).
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly OverlayManager  _overlayManager;
    private readonly AppConfig       _appConfig;
    private readonly ConfigStore     _configStore;
    private readonly IOverlayFactory _factory;

    // One ViewModel per overlay, keyed by overlay ID.
    private readonly Dictionary<string, OverlayConfigViewModel> _viewModels = new();

    // Reused panel instances.
    private readonly OverlaySettingsPanel _overlayPanel;
    private readonly GlobalSettingsPanel  _globalPanel;

    // Currently shown overlay ID (null = global panel or nothing).
    private string? _activeOverlayId;

    // в”Ђв”Ђ Nav items (bound to OverlayNavList) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    public sealed class OverlayNavItem
    {
        public string DisplayName { get; init; } = "";
        public string OverlayId   { get; init; } = "";
        public bool   IsEnabled
        {
            get => _config.Enabled;
            set => _config.Enabled = value;
        }
        private readonly OverlayConfig _config;
        public OverlayNavItem(string displayName, string overlayId, OverlayConfig config)
        {
            DisplayName = displayName;
            OverlayId   = overlayId;
            _config     = config;
        }
    }

    public SettingsWindow(
        OverlayManager  overlayManager,
        AppConfig       appConfig,
        ConfigStore     configStore,
        IOverlayFactory factory)
    {
        InitializeComponent();

        _overlayManager = overlayManager;
        _appConfig      = appConfig;
        _configStore    = configStore;
        _factory        = factory;

        _overlayPanel = new OverlaySettingsPanel();
        _globalPanel  = new GlobalSettingsPanel(overlayManager, appConfig, configStore);

        LoadIcon();
        BuildViewModels();
        BuildNavList();

        // Select first overlay by default.
        if (OverlayNavList.Items.Count > 0)
            OverlayNavList.SelectedIndex = 0;
    }

    // в”Ђв”Ђ Icon в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void LoadIcon()
    {
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "nrgoverlay.ico");
        if (!File.Exists(icoPath)) return;

        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource      = new Uri(icoPath, UriKind.Absolute);
            img.CacheOption    = BitmapCacheOption.OnLoad;
            img.DecodePixelWidth  = 32;
            img.DecodePixelHeight = 32;
            img.EndInit();
            img.Freeze();
            Icon = img;
        }
        catch { /* icon is cosmetic вЂ” ignore failures */ }
    }

    // в”Ђв”Ђ Entry point в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public void OpenOrActivate()
    {
        // Reload all VMs from live config when opening.
        foreach (var (id, vm) in _viewModels)
        {
            var cfg = _appConfig.Overlays.FirstOrDefault(c => c.Id == id);
            if (cfg != null) vm.LoadFrom(cfg);
        }
        _globalPanel.Reload();

        if (!IsVisible) Show();
        Activate();
    }

    // в”Ђв”Ђ Build helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void BuildViewModels()
    {
        foreach (var cfg in _appConfig.Overlays)
        {
            var vm = new OverlayConfigViewModel();
            vm.LoadFrom(cfg);
            _viewModels[cfg.Id] = vm;
        }
    }

    private void BuildNavList()
    {
        foreach (var (id, label) in _factory.DisplayNames)
        {
            var cfg = _appConfig.Overlays.FirstOrDefault(c => c.Id == id);
            if (cfg == null) continue;
            OverlayNavList.Items.Add(new OverlayNavItem(label, id, cfg));
        }
    }

    // в”Ђв”Ђ Navigation в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void OverlayNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OverlayNavList.SelectedItem is not OverlayNavItem item) return;

        GlobalNavList.SelectedItem = null;
        ShowOverlayPanel(item.OverlayId);
    }

    private void GlobalNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GlobalNavList.SelectedItem == null) return;

        OverlayNavList.SelectedItem = null;
        _activeOverlayId = null;
        ContentArea.Content = _globalPanel;
    }

    private void ShowOverlayPanel(string overlayId)
    {
        _activeOverlayId = overlayId;

        if (!_viewModels.TryGetValue(overlayId, out var vm)) return;

        _overlayPanel.Load(
            overlayId,
            vm,
            preview: () => _overlayManager.PreviewConfig(overlayId, vm.ToConfig()));

        ContentArea.Content = _overlayPanel;
    }

    // в”Ђв”Ђ Overlay enable/disable checkbox в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void OverlayEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not OverlayNavItem item) return;

        if (item.IsEnabled)
            _overlayManager.EnableOverlay(item.OverlayId);
        else
            _overlayManager.DisableOverlay(item.OverlayId);
    }

    // в”Ђв”Ђ Apply / Close в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_activeOverlayId != null && _viewModels.TryGetValue(_activeOverlayId, out var vm))
        {
            _overlayManager.ApplyConfig(_activeOverlayId, vm.ToConfig());
        }
        else
        {
            // Global panel handles its own save.
            _globalPanel.Apply();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Intercept close (Г—) to hide instead of destroy вЂ” preserves state for next open.
        e.Cancel = true;
        Hide();
    }
}

