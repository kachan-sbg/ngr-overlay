using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Input telemetry overlay — vertical bars for throttle, brake, clutch; gear and speed
/// header; optional scrolling time-series trace.
/// </summary>
public sealed class InputTelemetryOverlay : BaseOverlay
{
    public const string OverlayId   = "InputTelemetry";
    public const string WindowTitle = "SimOverlay \u2014 Input";

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

    // ── Live data ────────────────────────────────────────────────────────────
    private volatile TelemetryData? _telemetry;

    // ── Scrolling trace ring buffer (5 s × 60 Hz = 300 samples) ─────────────
    private const int TraceSamples = 300;
    private readonly float[] _throttleBuf = new float[TraceSamples];
    private readonly float[] _brakeBuf    = new float[TraceSamples];
    private volatile int _traceTail;  // next write index
    private volatile int _traceCount; // valid sample count (0–TraceSamples)

    // ── Mock animation state (render thread only) ────────────────────────────
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
        var  telem    = editMode ? null : _telemetry;

        float throttle, brake, clutch, speedMps;
        int   gear;

        if (telem is not null)
        {
            throttle = telem.Throttle;
            brake    = telem.Brake;
            clutch   = telem.Clutch;
            speedMps = telem.SpeedMps;
            gear     = telem.Gear;
        }
        else if (editMode)
        {
            float t  = ++_frameCounter / 60f;
            throttle = MathF.Max(0f, MathF.Sin(t * 1.2f));
            brake    = MathF.Max(0f, -MathF.Sin(t * 0.8f + 0.5f));
            clutch   = MathF.Max(0f, MathF.Sin(t * 0.4f + 1f) * 0.3f);
            speedMps = 33.33f + MathF.Sin(t * 0.3f) * 8f; // ~100–150 km/h
            gear     = (int)(t * 0.4f % 5) + 1;
        }
        else
        {
            // Connected but no telemetry data yet.
            var dimmed = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                             cfg.TextColor.B, cfg.TextColor.A * 0.45f);
            var fmt = Resources.GetTextFormat("Consolas", cfg.FontSize);
            using var layout = Resources.WriteFactory.CreateTextLayout(
                "No telemetry", fmt, (float)cfg.Width - 16f, (float)cfg.Height - 16f);
            layout.TextAlignment      = TextAlignment.Center;
            layout.ParagraphAlignment = ParagraphAlignment.Center;
            ctx.DrawTextLayout(new Vector2(8f, 8f), layout, dimmed, DrawTextOptions.Clip);
            return;
        }

        float pad  = 8f;
        float w    = (float)cfg.Width;
        float h    = (float)cfg.Height;
        var   dw   = Resources.WriteFactory;
        var   text = Resources.GetBrush(cfg.TextColor);
        var   dim  = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                         cfg.TextColor.B, cfg.TextColor.A * 0.45f);

        float y = pad;

        // ── Gear + Speed header ───────────────────────────────────────────────
        float gearSpeedH = 0f;
        if (cfg.ShowGearSpeed)
        {
            float gearFontSize = MathF.Max(cfg.FontSize * 2f, 24f);
            gearSpeedH = gearFontSize + 8f;

            string gearStr = gear switch { -1 => "R", 0 => "N", _ => gear.ToString() };
            float  speed   = cfg.SpeedUnit == SpeedUnit.Kph ? speedMps * 3.6f : speedMps * 2.23694f;
            string unit    = cfg.SpeedUnit == SpeedUnit.Kph ? "km/h" : "mph";
            string speedStr = $"{speed:F0} {unit}";

            float gearColW  = w * 0.3f;
            float speedColW = w - gearColW - pad;

            var gearFmt  = Resources.GetTextFormat("Consolas", gearFontSize);
            var speedFmt = Resources.GetTextFormat("Consolas", cfg.FontSize);

            using var gearLayout = dw.CreateTextLayout(gearStr, gearFmt, gearColW - pad, gearSpeedH);
            gearLayout.TextAlignment = TextAlignment.Center;
            ctx.DrawTextLayout(new Vector2(pad, y), gearLayout, text, DrawTextOptions.Clip);

            float speedY = y + (gearSpeedH - cfg.FontSize) / 2f;
            using var speedLayout = dw.CreateTextLayout(speedStr, speedFmt, speedColW, gearSpeedH);
            speedLayout.TextAlignment = TextAlignment.Leading;
            ctx.DrawTextLayout(new Vector2(gearColW, speedY), speedLayout, text, DrawTextOptions.Clip);

            y += gearSpeedH + 4f;
        }

        // ── Compute section heights ───────────────────────────────────────────
        float labelH  = cfg.FontSize + 4f;
        float available = h - y - pad - labelH;
        float traceH  = cfg.ShowInputTrace ? MathF.Max(50f, available * 0.28f) : 0f;
        float traceGap = traceH > 0f ? 4f : 0f;
        float barsH   = available - traceH - traceGap;
        if (barsH < 4f) barsH = 4f;

        // ── Pedal bars ────────────────────────────────────────────────────────
        bool[] showBar = [cfg.ShowThrottle, cfg.ShowBrake, cfg.ShowClutch];
        float[] values = [throttle, brake, clutch];
        ColorConfig[] colors = [cfg.ThrottleColor, cfg.BrakeColor, cfg.ClutchColor];
        string[] barLabels = ["T", "B", "C"];

        int numBars = 0;
        for (int i = 0; i < 3; i++) if (showBar[i]) numBars++;

        float innerW = w - 2f * pad;

        if (numBars > 0 && barsH > 4f)
        {
            float slotW = innerW / numBars;
            float barW  = slotW * 0.55f;
            float barOff = (slotW - barW) / 2f;

            var barBg = Resources.GetBrush(
                Math.Min(1f, cfg.BackgroundColor.R + 0.15f),
                Math.Min(1f, cfg.BackgroundColor.G + 0.15f),
                Math.Min(1f, cfg.BackgroundColor.B + 0.15f),
                0.9f);
            var labelFmt = Resources.GetTextFormat("Consolas", cfg.FontSize);

            int slotIdx = 0;
            for (int i = 0; i < 3; i++)
            {
                if (!showBar[i]) continue;

                float bx      = pad + slotIdx * slotW + barOff;
                float barTop  = y;
                float barBot  = y + barsH;
                float fillH   = Math.Clamp(values[i], 0f, 1f) * barsH;

                ctx.FillRectangle(new Vortice.RawRectF(bx, barTop, bx + barW, barBot), barBg);

                if (fillH > 0f)
                {
                    var c = colors[i];
                    var fillBrush = Resources.GetBrush(c.R, c.G, c.B, c.A);
                    ctx.FillRectangle(
                        new Vortice.RawRectF(bx, barBot - fillH, bx + barW, barBot),
                        fillBrush);
                }

                using var labelLayout = dw.CreateTextLayout(barLabels[i], labelFmt, slotW, labelH);
                labelLayout.TextAlignment = TextAlignment.Center;
                ctx.DrawTextLayout(new Vector2(pad + slotIdx * slotW, y + barsH + 2f),
                                   labelLayout, dim, DrawTextOptions.Clip);

                slotIdx++;
            }
        }

        y += barsH + labelH + 4f;

        // ── Scrolling trace ───────────────────────────────────────────────────
        if (cfg.ShowInputTrace && traceH > 4f)
        {
            float traceLeft = pad;
            float traceTop  = y;
            float traceW    = innerW;

            var traceBg = Resources.GetBrush(
                Math.Min(1f, cfg.BackgroundColor.R + 0.10f),
                Math.Min(1f, cfg.BackgroundColor.G + 0.10f),
                Math.Min(1f, cfg.BackgroundColor.B + 0.10f),
                0.85f);
            ctx.FillRectangle(
                new Vortice.RawRectF(traceLeft, traceTop, traceLeft + traceW, traceTop + traceH),
                traceBg);

            var divider = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                              cfg.TextColor.B, 0.20f);
            float mid = traceTop + traceH * 0.5f;
            ctx.DrawLine(new Vector2(traceLeft, mid), new Vector2(traceLeft + traceW, mid), divider, 1f);

            var tc = cfg.ThrottleColor;
            var bc = cfg.BrakeColor;
            var throttleBrush = Resources.GetBrush(tc.R, tc.G, tc.B, 0.85f);
            var brakeBrush    = Resources.GetBrush(bc.R, bc.G, bc.B, 0.85f);
            float halfH = traceH * 0.48f;
            float pxPerSample = traceW / TraceSamples;
            float pxW = MathF.Max(1f, pxPerSample);

            if (editMode)
            {
                float offset = _frameCounter * 0.5f;
                for (int i = 0; i < TraceSamples; i++)
                {
                    float frac = (i + offset) / TraceSamples;
                    float tVal = MathF.Max(0f, MathF.Sin(frac * MathF.Tau * 2.5f));
                    float bVal = MathF.Max(0f, -MathF.Sin(frac * MathF.Tau * 2f + 1f));
                    float x = traceLeft + i * pxPerSample;

                    if (tVal > 0.01f)
                        ctx.FillRectangle(
                            new Vortice.RawRectF(x, mid - tVal * halfH, x + pxW, mid),
                            throttleBrush);
                    if (bVal > 0.01f)
                        ctx.FillRectangle(
                            new Vortice.RawRectF(x, mid, x + pxW, mid + bVal * halfH),
                            brakeBrush);
                }
            }
            else
            {
                int count    = _traceCount;
                int startIdx = count < TraceSamples ? 0 : _traceTail;

                for (int i = 0; i < count; i++)
                {
                    int   bufIdx = (startIdx + i) % TraceSamples;
                    float x      = traceLeft + (TraceSamples - count + i) * pxPerSample;
                    float tVal   = _throttleBuf[bufIdx];
                    float bVal   = _brakeBuf[bufIdx];

                    if (tVal > 0.01f)
                        ctx.FillRectangle(
                            new Vortice.RawRectF(x, mid - tVal * halfH, x + pxW, mid),
                            throttleBrush);
                    if (bVal > 0.01f)
                        ctx.FillRectangle(
                            new Vortice.RawRectF(x, mid, x + pxW, mid + bVal * halfH),
                            brakeBrush);
                }
            }
        }
    }
}
