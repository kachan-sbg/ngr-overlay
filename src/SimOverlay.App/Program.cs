using System.Runtime.InteropServices;
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
        };

        AppLog.Info("Main() entered");

        try
        {
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
            AppLog.Info("OverlayManager created — entering message pump");

            // Dev hotkey: F10 = quit.
            int hotkeyQuit = MessagePump.RegisterHotKey(0, 0x79 /* F10 */);
            AppLog.Info("DEV: F10 = quit.");

            MessagePump.Run((msgId, wParam) =>
            {
                if (msgId == MessagePump.WmHotKey)
                {
                    var id = (int)wParam.ToInt64();
                    if (id == hotkeyQuit)
                        MessagePump.Quit();
                }
            });

            MessagePump.UnregisterHotKey(hotkeyQuit);

            // Persist any final state on clean shutdown.
            configStore.Save(appConfig);
            AppLog.Info("Message pump exited — clean shutdown");
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
    }
}
