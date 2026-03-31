using System.Runtime.InteropServices;
using SimOverlay.App.Dev;
using SimOverlay.Core;
using SimOverlay.Core.Events;
using SimOverlay.Rendering;

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
            var bus = new SimDataBus();
            AppLog.Info("SimDataBus created");

            using var overlay = new TestOverlay(bus);
            AppLog.Info("TestOverlay created — entering message pump");

            overlay.Show();
            bus.Publish(new EditModeChangedEvent(IsLocked: false));

            // Dev hotkeys: F9 = force device recovery, F10 = quit.
            // Remove before shipping.
            int hotkeyRecovery = MessagePump.RegisterHotKey(0, 0x78 /* F9  */);
            int hotkeyQuit     = MessagePump.RegisterHotKey(0, 0x79 /* F10 */);
            AppLog.Info("DEV: F9 = force device recovery, F10 = quit.");

            MessagePump.Run((msgId, wParam) =>
            {
                if (msgId == MessagePump.WmHotKey)
                {
                    var id = (int)wParam.ToInt64();
                    if (id == hotkeyRecovery)
                    {
                        AppLog.Info("DEV: F9 — forcing device recovery.");
                        overlay.RecoverDevice();
                        overlay.InvalidateResources();
                        AppLog.Info("DEV: Device recovery forced.");
                    }
                    else if (id == hotkeyQuit)
                    {
                        MessagePump.Quit();
                    }
                }
            });

            MessagePump.UnregisterHotKey(hotkeyRecovery);
            MessagePump.UnregisterHotKey(hotkeyQuit);
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
