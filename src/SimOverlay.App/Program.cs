using System.Runtime.InteropServices;
using SimOverlay.App.Dev;
using SimOverlay.Core;
using SimOverlay.Rendering;

namespace SimOverlay.App;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [STAThread]
    private static void Main()
    {
        try
        {
            // DEV: spin up a test overlay to verify the rendering pipeline.
            // Replace this block with the real host/DI wiring in a later task.
            var bus = new SimDataBus();

            using var overlay = new TestOverlay(bus);
            overlay.Show();

            MessagePump.Run();
        }
        catch (Exception ex)
        {
            MessageBox(nint.Zero,
                $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "SimOverlay — Startup Error",
                0x10 /* MB_ICONERROR */);
        }
    }
}
