using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace NrgOverlay.Overlays;

/// <summary>
/// Input telemetry overlay - compact throttle/brake visualization with shared-plane trace.
/// </summary>
public sealed class InputTelemetryOverlay : BaseOverlay
{
    public const string OverlayId   = "InputTelemetry";
    public const string WindowTitle = "NrgOverlay - Input";

    public static OverlayConfig DefaultConfig => new()
    {
        Id             = OverlayId,
        Enabled        = true,
        X              = 100,
        Y              = 100,
        Width          = 200,
        Height         = 300,
        Opacity        = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize       = 13f,
        ShowThrottle   = true,
        ShowBrake      = true,
        ShowClutch     = true,
        ShowInputTrace = true,
        ShowGearSpeed  = true,
        SpeedUnit      = SpeedUnit.Kph,
        ThrottleColor  = ColorConfig.Green,
        BrakeColor     = ColorConfig.Red,
        ClutchColor    = ColorConfig.Blue,
    };

    private volatile TelemetryData? _telemetry;

    // Ring buffer (5 s x 60 Hz = 300 samples)
    private const int TraceSamples = 300;
    private readonly float[] _throttleBuf = new float[TraceSamples];
    private readonly float[] _brakeBuf    = new float[TraceSamples];
    private volatile int _traceTail;  // next write index
    private volatile int _traceCount; // valid sample count (0-TraceSamples)
    private const float CurrentBarWidthPx = 14f;
    private const float CurrentBarGapPx = 4f;

    // Mock animation state (render thread only)
    private int _frameCounter;

    public InputTelemetryOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<TelemetryData>(data =>
        {
            _telemetry = data;
            int tail = _traceTail;
            _throttleBuf[tail] = data.Throttle;
            _brakeBuf[tail]    = data.Brake;
            _traceTail  = (tail + 1) % TraceSamples;
            if (_traceCount < TraceSamples) _traceCount++;
        });
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        bool editMode = !IsLocked;
        var telem = editMode ? null : _telemetry;

        float throttle;
        float brake;
        float speedMps;
        int gear;

        if (telem is not null)
        {
            throttle = telem.Throttle;
            brake    = telem.Brake;
            speedMps = telem.SpeedMps;
            gear     = telem.Gear;
        }
        else if (editMode)
        {
            float t  = ++_frameCounter / 60f;
            throttle = MathF.Max(0f, MathF.Sin(t * 1.2f));
            brake    = MathF.Max(0f, -MathF.Sin(t * 0.8f + 0.5f));
            speedMps = 33.33f + MathF.Sin(t * 0.3f) * 8f;
            gear     = (int)(t * 0.4f % 5) + 1;
        }
        else
        {
            var dimmed = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                             cfg.TextColor.B, cfg.TextColor.A * 0.45f);
            var fmt = Resources.GetTextFormat("Oswald", cfg.FontSize);
            using var layout = Resources.WriteFactory.CreateTextLayout(
                "No telemetry", fmt, (float)cfg.Width - 16f, (float)cfg.Height - 16f);
            layout.TextAlignment      = TextAlignment.Center;
            layout.ParagraphAlignment = ParagraphAlignment.Center;
            ctx.DrawTextLayout(new Vector2(8f, 8f), layout, dimmed, DrawTextOptions.Clip);
            return;
        }

        float pad = 8f;
        float w = (float)cfg.Width;
        float h = (float)cfg.Height;
        var dw = Resources.WriteFactory;
        var text = Resources.GetBrush(cfg.TextColor);

        float y = pad;

        if (cfg.ShowGearSpeed)
        {
            float gearFontSize = MathF.Max(cfg.FontSize * 2f, 24f);
            float gearSpeedH = gearFontSize + 8f;

            string gearStr = gear switch { -1 => "R", 0 => "N", _ => gear.ToString() };
            float speed = cfg.SpeedUnit == SpeedUnit.Kph ? speedMps * 3.6f : speedMps * 2.23694f;
            string unit = cfg.SpeedUnit == SpeedUnit.Kph ? "km/h" : "mph";
            string speedStr = $"{speed:F0} {unit}";

            float gearColW = w * 0.3f;
            float speedColW = w - gearColW - pad;

            var gearFmt = Resources.GetTextFormat("Oswald", gearFontSize);
            var speedFmt = Resources.GetTextFormat("Oswald", cfg.FontSize);

            using var gearLayout = dw.CreateTextLayout(gearStr, gearFmt, gearColW - pad, gearSpeedH);
            gearLayout.TextAlignment = TextAlignment.Center;
            ctx.DrawTextLayout(new Vector2(pad, y), gearLayout, text, DrawTextOptions.Clip);

            float speedY = y + (gearSpeedH - cfg.FontSize) / 2f;
            using var speedLayout = dw.CreateTextLayout(speedStr, speedFmt, speedColW, gearSpeedH);
            speedLayout.TextAlignment = TextAlignment.Leading;
            ctx.DrawTextLayout(new Vector2(gearColW, speedY), speedLayout, text, DrawTextOptions.Clip);

            y += gearSpeedH + 4f;
        }

        float innerW = w - 2f * pad;
        float contentTop = y;
        float contentH = MathF.Max(16f, h - contentTop - pad);
        float gap = 6f;
        float sideW = (CurrentBarWidthPx * 2f) + CurrentBarGapPx;
        float graphW = MathF.Max(40f, innerW - sideW - gap);

        float graphLeft = pad;
        float graphTop = contentTop;
        float graphBottom = graphTop + contentH;
        float graphRight = graphLeft + graphW;

        var panelBg = Resources.GetBrush(
            Math.Min(1f, cfg.BackgroundColor.R + 0.12f),
            Math.Min(1f, cfg.BackgroundColor.G + 0.12f),
            Math.Min(1f, cfg.BackgroundColor.B + 0.12f),
            0.88f);
        var panelStroke = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G, cfg.TextColor.B, 0.20f);
        ctx.FillRectangle(new Vortice.RawRectF(graphLeft, graphTop, graphRight, graphBottom), panelBg);
        ctx.DrawRectangle(new Vortice.RawRectF(graphLeft, graphTop, graphRight, graphBottom), panelStroke, 1f);

        var grid = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G, cfg.TextColor.B, 0.10f);
        for (int i = 1; i < 5; i++)
        {
            float gx = graphLeft + graphW * (i / 5f);
            ctx.DrawLine(new Vector2(gx, graphTop + 1f), new Vector2(gx, graphBottom - 1f), grid, 1f);
        }

        var tc = cfg.ThrottleColor;
        var bc = cfg.BrakeColor;
        var throttleFill = Resources.GetBrush(tc.R, tc.G, tc.B, Math.Clamp(tc.A * 0.5f, 0f, 1f));
        var brakeFill = Resources.GetBrush(bc.R, bc.G, bc.B, Math.Clamp(bc.A * 0.5f, 0f, 1f));
        var throttleStroke = Resources.GetBrush(tc.R, tc.G, tc.B, 1f);
        var brakeStroke = Resources.GetBrush(bc.R, bc.G, bc.B, 1f);

        int availableSamples = editMode ? TraceSamples : Math.Max(0, _traceCount);
        int renderPoints = graphW < 220f
            ? Math.Max(2, (int)MathF.Ceiling(graphW))
            : Math.Clamp((int)MathF.Ceiling(graphW * 0.75f), 160, 600);
        float pxPerPoint = graphW / MathF.Max(1f, renderPoints - 1f);

        Vector2? prevThrottle = null;
        Vector2? prevBrake = null;
        var previousAntialias = ctx.AntialiasMode;
        ctx.AntialiasMode = AntialiasMode.Aliased;
        for (int i = 0; i < renderPoints; i++)
        {
            float samplePos = renderPoints <= 1 || availableSamples <= 1
                ? 0f
                : (i / (float)(renderPoints - 1)) * (availableSamples - 1);
            GetTraceSampleInterpolated(editMode, samplePos, availableSamples, out float tVal, out float bVal);

            float x = graphLeft + i * pxPerPoint;
            float xNext = i == renderPoints - 1 ? graphRight : graphLeft + (i + 1) * pxPerPoint + 0.75f;
            float x0 = MathF.Floor(x);
            float x1 = MathF.Ceiling(xNext);
            if (x1 <= x0) x1 = x0 + 1f;
            float tY = graphBottom - Math.Clamp(tVal, 0f, 1f) * contentH;
            float bY = graphBottom - Math.Clamp(bVal, 0f, 1f) * contentH;

            if (tVal > 0.001f)
                ctx.FillRectangle(new Vortice.RawRectF(x0, tY, x1, graphBottom), throttleFill);
            if (bVal > 0.001f)
                ctx.FillRectangle(new Vortice.RawRectF(x0, bY, x1, graphBottom), brakeFill);

            var throttlePoint = new Vector2(x, tY);
            var brakePoint = new Vector2(x, bY);
            if (prevThrottle.HasValue)
                ctx.DrawLine(prevThrottle.Value, throttlePoint, throttleStroke, 2.5f);
            if (prevBrake.HasValue)
                ctx.DrawLine(prevBrake.Value, brakePoint, brakeStroke, 2.5f);
            prevThrottle = throttlePoint;
            prevBrake = brakePoint;
        }
        ctx.AntialiasMode = previousAntialias;

        float sideLeft = graphRight + gap;
        DrawCompactBar(ctx, sideLeft, graphTop, CurrentBarWidthPx, contentH, throttle, tc);
        DrawCompactBar(ctx, sideLeft + CurrentBarWidthPx + CurrentBarGapPx, graphTop, CurrentBarWidthPx, contentH, brake, bc);
    }

    private void DrawCompactBar(
        ID2D1RenderTarget ctx,
        float x,
        float top,
        float w,
        float h,
        float value,
        ColorConfig color)
    {
        var bg = Resources.GetBrush(0.08f, 0.08f, 0.08f, 0.80f);
        var fill = Resources.GetBrush(color.R, color.G, color.B, Math.Clamp(color.A * 0.85f, 0f, 1f));
        var stroke = Resources.GetBrush(color.R, color.G, color.B, 1f);
        ctx.FillRectangle(new Vortice.RawRectF(x, top, x + w, top + h), bg);
        float fillH = Math.Clamp(value, 0f, 1f) * h;
        if (fillH > 0.001f)
        {
            var y0 = top + h - fillH;
            ctx.FillRectangle(new Vortice.RawRectF(x, y0, x + w, top + h), fill);
            ctx.DrawLine(new Vector2(x, y0), new Vector2(x + w, y0), stroke, 2f);
        }
        ctx.DrawRectangle(new Vortice.RawRectF(x, top, x + w, top + h), stroke, 1f);
    }

    private void GetTraceSampleInterpolated(
        bool editMode,
        float samplePos,
        int availableSamples,
        out float throttle,
        out float brake)
    {
        if (editMode)
        {
            float offset = _frameCounter * 0.5f;
            float frac = ((samplePos + offset) % TraceSamples) / TraceSamples;
            throttle = MathF.Max(0f, MathF.Sin(frac * MathF.Tau * 2.5f));
            brake = MathF.Max(0f, -MathF.Sin(frac * MathF.Tau * 2f + 1f));
            return;
        }

        if (availableSamples <= 0)
        {
            throttle = 0f;
            brake = 0f;
            return;
        }

        int count = Math.Min(availableSamples, TraceSamples);
        if (count <= 1)
        {
            int idx0 = count < TraceSamples ? 0 : _traceTail;
            throttle = _throttleBuf[idx0];
            brake = _brakeBuf[idx0];
            return;
        }

        int startIdx = count < TraceSamples ? 0 : _traceTail;
        float clamped = Math.Clamp(samplePos, 0f, count - 1f);
        int i0 = (int)MathF.Floor(clamped);
        int i1 = Math.Min(i0 + 1, count - 1);
        float t = clamped - i0;

        int buf0 = (startIdx + i0) % TraceSamples;
        int buf1 = (startIdx + i1) % TraceSamples;

        throttle = _throttleBuf[buf0] + ((_throttleBuf[buf1] - _throttleBuf[buf0]) * t);
        brake = _brakeBuf[buf0] + ((_brakeBuf[buf1] - _brakeBuf[buf0]) * t);
    }
}
