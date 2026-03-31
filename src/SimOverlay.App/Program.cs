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

            MessagePump.Run();
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
