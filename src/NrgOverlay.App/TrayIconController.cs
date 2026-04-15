using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NrgOverlay.App;

/// <summary>
/// Manages the system-tray notification icon and its context menu.
/// Requires <c>&lt;UseWindowsForms&gt;true&lt;/UseWindowsForms&gt;</c> in the csproj.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon     _icon;
    private readonly OverlayManager _overlayManager;
    private readonly Action         _openSettings;
    private readonly Action         _quit;

    private ToolStripMenuItem _editModeItem   = null!;
    private ToolStripMenuItem _streamModeItem = null!;

    // Prevents feedback loops when we programmatically sync checkbox states.
    private bool _syncingMenu;

    public TrayIconController(
        OverlayManager overlayManager,
        Action         openSettings,
        Action         quit)
    {
        _overlayManager = overlayManager;
        _openSettings   = openSettings;
        _quit           = quit;

        _icon = new NotifyIcon
        {
            Text    = "NrgOverlay",
            Visible = true,
            Icon    = LoadAppIcon(),
        };

        _icon.DoubleClick += (_, _) => _openSettings();
        _icon.ContextMenuStrip = BuildMenu();
    }

    // в”Ђв”Ђ Menu construction в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private ContextMenuStrip BuildMenu()
    {
        _editModeItem = new ToolStripMenuItem("Edit mode")
        {
            CheckOnClick = true,
            Checked      = _overlayManager.EditModeActive,
        };
        _editModeItem.CheckedChanged += (_, _) =>
        {
            if (!_syncingMenu)
                _overlayManager.SetEditMode(_editModeItem.Checked);
        };

        _streamModeItem = new ToolStripMenuItem("Stream mode")
        {
            CheckOnClick = true,
            Checked      = _overlayManager.StreamModeActive,
        };
        _streamModeItem.CheckedChanged += (_, _) =>
        {
            if (!_syncingMenu)
                _overlayManager.SetStreamMode(_streamModeItem.Checked);
        };

        var settingsItem = new ToolStripMenuItem("Settings\u2026");
        settingsItem.Click += (_, _) => _openSettings();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => _quit();

        var menu = new ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_editModeItem);
        menu.Items.Add(_streamModeItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        // Sync checked states from live manager each time the menu opens.
        menu.Opening += (_, _) => SyncCheckedStates();

        return menu;
    }

    private void SyncCheckedStates()
    {
        _syncingMenu = true;
        _editModeItem.Checked   = _overlayManager.EditModeActive;
        _streamModeItem.Checked = _overlayManager.StreamModeActive;
        _syncingMenu = false;
    }

    // в”Ђв”Ђ Icon в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Loads the app icon from the Resources folder next to the executable.
    /// Falls back to the default system application icon when unavailable.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        var exeDir  = AppContext.BaseDirectory;
        var icoPath = Path.Combine(exeDir, "Resources", "nrgoverlay.ico");

        if (File.Exists(icoPath))
        {
            try { return new Icon(icoPath, 16, 16); }
            catch { /* fall through to system icon */ }
        }

        return SystemIcons.Application;
    }

    // в”Ђв”Ђ IDisposable в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

