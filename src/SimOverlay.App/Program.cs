using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using SimOverlay.App.Settings;
using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using SimOverlay.Sim.iRacing;
using SimOverlay.Sim.LMU;

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

            // ── DI composition root ───────────────────────────────────────────
            // Load config before building the container so we can register the instance.
            var configStore = new ConfigStore();
            var appConfig   = configStore.Load();

            var services = new ServiceCollection();
            services.AddSingleton(configStore);
            services.AddSingleton(appConfig);
            services.AddSingleton<ISimDataBus, SimDataBus>();
            services.AddSingleton<IRacingProvider>();
            services.AddSingleton<LmuProvider>();
            // Provider order is determined by GlobalSettings.SimPriorityOrder.
            // Providers not listed in the config appear after those that are.
            services.AddSingleton<IReadOnlyList<ISimProvider>>(sp =>
            {
                var cfg   = sp.GetRequiredService<AppConfig>();
                var order = cfg.GlobalSettings.SimPriorityOrder;
                var all   = new List<ISimProvider>
                {
                    sp.GetRequiredService<IRacingProvider>(),
                    sp.GetRequiredService<LmuProvider>(),
                };
                return all
                    .OrderBy(p => { var i = order.IndexOf(p.SimId); return i < 0 ? int.MaxValue : i; })
                    .ToList();
            });
            services.AddSingleton<SimDetector>();
            services.AddSingleton<IOverlayFactory, OverlayFactory>();
            services.AddSingleton<OverlayManager>();

            using var provider = services.BuildServiceProvider();
            AppLog.Info("DI container built");

            // Resolve singletons — provider manages their lifetimes.
            // IMPORTANT: OverlayManager must be resolved BEFORE SimDetector.
            // SimDetector's constructor starts its poll timer with TimeSpan.Zero (fires immediately
            // on a ThreadPool thread). If SimDetector were resolved first, it could detect the sim
            // and publish SimStateChangedEvent before any overlays have subscribed to the bus,
            // leaving all overlays stuck at SimState.Disconnected ("Sim not detected") forever.
            var overlayManager = provider.GetRequiredService<OverlayManager>();
            var detector       = provider.GetRequiredService<SimDetector>();
            var factory        = provider.GetRequiredService<IOverlayFactory>();
            AppLog.Info("Core services resolved");

            // --- Settings window (lazy singleton, not in DI — requires delegate wiring) ---
            SettingsWindow? settingsWindow = null;
            SettingsWindow GetOrCreateSettings()
            {
                if (settingsWindow is null)
                {
                    settingsWindow = new SettingsWindow(overlayManager, appConfig, configStore, factory);
                    AppLog.Info("SettingsWindow created");
                }
                return settingsWindow;
            }
            void OpenSettings() => GetOrCreateSettings().OpenOrActivate();

            // Wire single-instance signal → open Settings (fired when a second instance starts).
            singleInstance.OpenSettingsRequested = OpenSettings;

            // Shared shutdown — saves config and posts WM_QUIT so the message pump
            // exits cleanly.  This lets the 'using var provider' block above unwind,
            // which cascades: ServiceProvider → SimDetector → IRacingProvider/LmuProvider
            // → IRacingPoller (GC.Collect for IRSDKSharper Win32 handles) / LmuPoller.
            // Using Environment.Exit(0) directly would bypass this entire chain.
            var shutdownRequested = false;
            void Shutdown()
            {
                if (shutdownRequested) return;
                shutdownRequested = true;
                AppLog.Info("Shutdown requested — saving config and posting WM_QUIT.");
                configStore.Save(appConfig);
                MessagePump.Quit();
            }

            // --- Tray icon ---
            using var tray = new TrayIconController(
                overlayManager,
                openSettings: OpenSettings,
                quit: () =>
                {
                    AppLog.Info("Quit via tray.");
                    Shutdown();
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
                        AppLog.Info("F10 quit.");
                        Shutdown();
                    }
                }
            });

            // Pump has exited — unregister hotkeys before the window is fully torn down.
            MessagePump.UnregisterHotKey(hotkeySettings);
            MessagePump.UnregisterHotKey(hotkeyQuit);
            AppLog.Info("Message pump exited — unregistered hotkeys.");
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
