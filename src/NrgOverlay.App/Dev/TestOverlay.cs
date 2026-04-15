using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace NrgOverlay.App.Dev;

/// <summary>
/// Minimal overlay used to verify the rendering pipeline end-to-end.
/// Draws a semi-transparent red rectangle and an incrementing frame counter.
/// Remove or exclude this class before shipping.
/// </summary>
internal sealed class TestOverlay : BaseOverlay
{
    private int _frame;
    private int _recoveries;

    public TestOverlay(ISimDataBus bus)
        : base(
            displayName: "NrgOverlay - Test",
            config: new OverlayConfig { X = 100, Y = 100, Width = 300, Height = 100 },
            bus: bus)
    {
    }

    // Called by the render loop after automatic device recovery.
    protected override void OnDeviceRecovered() => Interlocked.Increment(ref _recoveries);

    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        var w = (float)config.Width;
        var h = (float)config.Height;

        // Semi-transparent red background rectangle
        var redBrush = Resources.GetBrush(0.8f, 0.1f, 0.1f, 0.7f);
        context.FillRectangle(new Vortice.RawRectF(0, 0, w, h), redBrush);

        // White border
        var whiteBrush = Resources.GetBrush(1f, 1f, 1f, 1f);
        context.DrawRectangle(new Vortice.RawRectF(1, 1, w - 1, h - 1), whiteBrush, 1f);

        // Frame counter + recovery count
        var label = $"Frame {++_frame}  |  Recoveries: {_recoveries}";
        var textFormat = Resources.GetTextFormat("Segoe UI", 14f);
        using var layout = Resources.WriteFactory.CreateTextLayout(label, textFormat, w - 20, h - 20);
        context.DrawTextLayout(new System.Numerics.Vector2(10, 10), layout, whiteBrush);
    }
}


