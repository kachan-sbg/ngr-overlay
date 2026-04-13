using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimOverlay.App;

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
            Text    = "SimOverlay",
            Visible = true,
            Icon    = LoadAppIcon(),
        };

        _icon.DoubleClick += (_, _) => _openSettings();
        _icon.ContextMenuStrip = BuildMenu();
    }

    // ── Menu construction ─────────────────────────────────────────────────────

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

    // ── Icon ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the app icon from the Resources folder embedded next to the executable.
    /// Falls back to a programmatically-generated icon if the file is not present.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        var exeDir  = AppContext.BaseDirectory;
        var icoPath = Path.Combine(exeDir, "Resources", "simoverlay.ico");

        if (File.Exists(icoPath))
        {
            try { return new Icon(icoPath, 16, 16); }
            catch { /* fall through to generated icon */ }
        }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var font = new Font("Arial", 7f, FontStyle.Bold);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.FillEllipse(Brushes.DodgerBlue, 1, 1, 13, 13);
            g.DrawString("S", font,
                         Brushes.White, 3f, 2f);
        }
        var hIcon = bmp.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);
}
