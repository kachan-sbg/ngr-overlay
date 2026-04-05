using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;
using SimOverlay.App.Settings;
using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.iRacing;

namespace SimOverlay.App;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppLog.Error($"UNHANDLED EXCEPTION (terminating={e.IsTerminating}): {ex?.GetType().Name}: {ex?.Message}");
            if (ex is not null) AppLog.Error(ex.StackTrace ?? "(no stack trace)");

            if (e.IsTerminating)
            {
                MessageBox(nint.Zero,
                    $"SimOverlay encountered a fatal error and must close.\n\n{ex?.GetType().Name}: {ex?.Message}\n\n{ex?.StackTrace}",
                    "SimOverlay — Fatal Error",
                    0x10 /* MB_ICONERROR */);
            }
        };

        AppLog.Info("Main() entered");

        // ── Single-instance guard ─────────────────────────────────────────────
        // Must be created before any UI so the hidden HWND is on the STA thread.
        using var singleInstance = new SingleInstanceGuard();
        if (singleInstance.IsAlreadyRunning)
        {
            AppLog.Info("Second instance detected — signaled first instance and exiting.");
            return;
        }

        try
        {
            // --- WPF Application (required for SettingsWindow) ---
            // ShutdownMode=OnExplicitShutdown: WPF doesn't own the message loop — our
            // Win32 MessagePump does. WPF messages are dispatched via ComponentDispatcher.
            var wpfApp = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            wpfApp.DispatcherUnhandledException += (_, e) =>
            {
                AppLog.Exception("WPF dispatcher unhandled exception", e.Exception);
                MessageBox(nint.Zero,
                    $"SimOverlay encountered an unhandled error.\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "SimOverlay — Unhandled Error",
                    0x10 /* MB_ICONERROR */);
                e.Handled = true;
            };

            // --- Core services ---
            var configStore = new ConfigStore();
            var appConfig   = configStore.Load();
            var bus         = new SimDataBus();
            AppLog.Info("Core services created");

            // --- Sim providers (priority order) ---
            IReadOnlyList<ISimProvider> providers = [new IRacingProvider(bus)];

            // --- Sim detector ---
            using var detector = new SimDetector(bus, providers);
            AppLog.Info("SimDetector started");

            // --- Overlays ---
            using var overlayManager = new OverlayManager(bus, appConfig, configStore);
            AppLog.Info("OverlayManager created");

            // --- Settings window (lazy singleton) ---
            SettingsWindow? settingsWindow = null;
            SettingsWindow GetOrCreateSettings()
            {
                if (settingsWindow is null)
                {
                    settingsWindow = new SettingsWindow(overlayManager, appConfig, configStore);
                    AppLog.Info("SettingsWindow created");
                }
                return settingsWindow;
            }
            void OpenSettings() => GetOrCreateSettings().OpenOrActivate();

            // Wire single-instance signal → open Settings (fired when a second instance starts).
            singleInstance.OpenSettingsRequested = OpenSettings;

            // --- Tray icon ---
            using var tray = new TrayIconController(
                overlayManager,
                openSettings: OpenSettings,
                quit: () =>
                {
                    AppLog.Info("Quit via tray — saving config and exiting.");
                    configStore.Save(appConfig);
                    Environment.Exit(0);
                });
            AppLog.Info("TrayIconController started — entering message pump");

            // --- Z-order hook ---
            using var zOrderHook = new ZOrderHook(
                overlayManager.BringAllToFront,
                overlayManager.OwnedHandles);

            // Dev hotkeys (no modifiers):
            //   F9  = open Settings
            //   F10 = quit
            int hotkeySettings = MessagePump.RegisterHotKey(modifiers: 0, virtualKey: 0x78 /* F9  */);
            int hotkeyQuit     = MessagePump.RegisterHotKey(modifiers: 0, virtualKey: 0x79 /* F10 */);
            AppLog.Info("DEV: F9 = Settings, F10 = quit.");

            MessagePump.Run((msgId, wParam) =>
            {
                if (msgId == MessagePump.WmHotKey)
                {
                    var id = (int)wParam.ToInt64();

                    if (id == hotkeySettings)
                        OpenSettings();

                    if (id == hotkeyQuit)
                    {
                        AppLog.Info("F10 quit — saving config and exiting.");
                        zOrderHook.Dispose();
                        configStore.Save(appConfig);
                        Environment.Exit(0);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            AppLog.Exception("Fatal startup error", ex);

            MessageBox(nint.Zero,
                $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "SimOverlay — Startup Error",
                0x10 /* MB_ICONERROR */);
        }

        AppLog.Info("Main() exiting");

        // IRSDKSharper may leave a foreground thread alive after Stop().
        // Force-exit so the process doesn't hang in the terminal.
        Environment.Exit(0);
    }
}
