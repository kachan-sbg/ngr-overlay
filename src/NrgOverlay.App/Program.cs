using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using NrgOverlay.App.Settings;
using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using NrgOverlay.Sim.iRacing;
using NrgOverlay.Sim.LMU;

namespace NrgOverlay.App;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [STAThread]
    private static void Main()
    {
        StartupSplashWindow? splash = null;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppLog.Error($"UNHANDLED EXCEPTION (terminating={e.IsTerminating}): {ex?.GetType().Name}: {ex?.Message}");
            if (ex is not null)
                AppLog.Error(ex.StackTrace ?? "(no stack trace)");

            if (e.IsTerminating)
            {
                MessageBox(
                    nint.Zero,
                    $"NrgOverlay encountered a fatal error and must close.\n\n{ex?.GetType().Name}: {ex?.Message}\n\n{ex?.StackTrace}",
                    "NrgOverlay - Fatal Error",
                    0x10);
            }
        };

        AppLog.Info("Main() entered");

        using var singleInstance = new SingleInstanceGuard();
        if (singleInstance.IsAlreadyRunning)
        {
            AppLog.Info("Second instance detected - signaled first instance and exiting.");
            return;
        }

        try
        {
            var wpfApp = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            var startupTimer = Stopwatch.StartNew();
            splash = new StartupSplashWindow();
            splash.Show();
            PumpWpfDispatcher(wpfApp);

            wpfApp.DispatcherUnhandledException += (_, e) =>
            {
                AppLog.Exception("WPF dispatcher unhandled exception", e.Exception);
                MessageBox(
                    nint.Zero,
                    $"NrgOverlay encountered an unhandled error.\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "NrgOverlay - Unhandled Error",
                    0x10);
                e.Handled = true;
            };

            var configStore = new ConfigStore();
            var appConfig = configStore.Load();

            var services = new ServiceCollection();
            services.AddSingleton(configStore);
            services.AddSingleton(appConfig);
            services.AddSingleton<ISimDataBus, SimDataBus>();
            services.AddSingleton<IRacingProvider>();
            services.AddSingleton<LmuProvider>();
            services.AddSingleton<IReadOnlyList<ISimProvider>>(sp =>
            {
                var cfg = sp.GetRequiredService<AppConfig>();
                var order = cfg.GlobalSettings.SimPriorityOrder;
                var all = new List<ISimProvider>
                {
                    sp.GetRequiredService<IRacingProvider>(),
                    sp.GetRequiredService<LmuProvider>(),
                };
                return all
                    .OrderBy(p =>
                    {
                        var i = order.IndexOf(p.SimId);
                        return i < 0 ? int.MaxValue : i;
                    })
                    .ToList();
            });
            services.AddSingleton<SimDetector>();
            services.AddSingleton<IOverlayFactory, OverlayFactory>();
            services.AddSingleton<OverlayManager>();

            using var provider = services.BuildServiceProvider();
            AppLog.Info("DI container built");

            var overlayManager = provider.GetRequiredService<OverlayManager>();
            _ = provider.GetRequiredService<SimDetector>();
            var factory = provider.GetRequiredService<IOverlayFactory>();
            AppLog.Info("Core services resolved");

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
            singleInstance.OpenSettingsRequested = OpenSettings;

            var shutdownRequested = false;
            void Shutdown()
            {
                if (shutdownRequested)
                    return;

                shutdownRequested = true;
                AppLog.Info("Shutdown requested - saving config and posting WM_QUIT.");
                configStore.Save(appConfig);
                MessagePump.Quit();
            }

            using var tray = new TrayIconController(
                overlayManager,
                openSettings: OpenSettings,
                quit: () =>
                {
                    AppLog.Info("Quit via tray.");
                    Shutdown();
                });
            AppLog.Info("TrayIconController started - entering message pump");

            using var zOrderHook = new ZOrderHook(
                overlayManager.BringAllToFront,
                overlayManager.OwnedHandles);

            int hotkeySettings = MessagePump.RegisterHotKey(modifiers: 0, virtualKey: 0x78);
            int hotkeyQuit = MessagePump.RegisterHotKey(modifiers: 0, virtualKey: 0x79);
            if (hotkeySettings < 0)
                AppLog.Warn("F9 hotkey registration failed.");
            if (hotkeyQuit < 0)
                AppLog.Warn("F10 hotkey registration failed.");
            AppLog.Info("DEV: F9 = Settings, F10 = quit.");

            var remainingSplash = StartupSplashWindow.MinimumVisibleDuration - startupTimer.Elapsed;
            if (remainingSplash > TimeSpan.Zero)
            {
                var stopAt = DateTime.UtcNow + remainingSplash;
                while (DateTime.UtcNow < stopAt)
                {
                    PumpWpfDispatcher(wpfApp);
                    Thread.Sleep(16);
                }
            }

            if (splash.IsVisible)
            {
                splash.Close();
                PumpWpfDispatcher(wpfApp);
            }

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

            if (hotkeySettings > 0)
                MessagePump.UnregisterHotKey(hotkeySettings);
            if (hotkeyQuit > 0)
                MessagePump.UnregisterHotKey(hotkeyQuit);
            AppLog.Info("Message pump exited - unregistered hotkeys.");
        }
        catch (Exception ex)
        {
            if (splash is not null && splash.IsVisible)
            {
                try
                {
                    splash.Close();
                }
                catch
                {
                    // Best effort.
                }
            }

            AppLog.Exception("Fatal startup error", ex);
            MessageBox(
                nint.Zero,
                $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "NrgOverlay - Startup Error",
                0x10);
        }

        AppLog.Info("Main() exiting");
        Environment.Exit(0);
    }

    private static void PumpWpfDispatcher(Application app) =>
        app.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
}

