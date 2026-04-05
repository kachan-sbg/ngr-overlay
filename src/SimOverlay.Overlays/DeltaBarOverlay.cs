using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Delta bar — animated green/red bar showing the gap vs. the player's best lap,
/// with optional numeric delta text and trend arrow.
/// </summary>
public sealed class DeltaBarOverlay : BaseOverlay
{
    public const string OverlayId   = "DeltaBar";
    public const string WindowTitle = "SimOverlay \u2014 Delta";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                 = OverlayId,
        Enabled            = true,
        X                  = 100,
        Y                  = 780,
        Width              = 300,
        Height             = 80,
        Opacity            = 0.85f,
        BackgroundColor    = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize           = 13f,
        DeltaBarMaxSeconds = 2f,
        FasterColor        = ColorConfig.Green,
        SlowerColor        = ColorConfig.Red,
        ShowTrendArrow     = true,
        ShowDeltaText      = true,
    };

    private volatile DriverData? _driver;

    // ── Edit-mode mock data ───────────────────────────────────────────────────
    private static readonly DriverData MockDriver = new()
    {
        Position          = 5,
        Lap               = 12,
        LastLapTime       = TimeSpan.FromSeconds(94.521),
        BestLapTime       = TimeSpan.FromSeconds(93.887),
        LapDeltaVsBestLap = -0.234f,   // green side — visually shows a filled bar
    };

    // 30-sample ring buffer for 500 ms trend computation at ~60 Hz.
    private const int TrendSamples = 30;
    private readonly float[] _trendBuf = new float[TrendSamples];
    private int _trendHead;
    private int _trendCount;

    public DeltaBarOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<DriverData>(data => _driver = data);
    }

    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        var driver = IsLocked ? _driver : MockDriver;
        var delta  = driver?.LapDeltaVsBestLap ?? 0f;

        PushTrend(delta);

        var pad    = 8f;
        var w      = (float)config.Width;
        var h      = (float)config.Height;
        var innerW = w - 2f * pad;

        bool isFaster  = delta <= 0f;
        var fillColor  = isFaster ? config.FasterColor : config.SlowerColor;
        var fillBrush  = Resources.GetBrush(fillColor.R, fillColor.G, fillColor.B, fillColor.A);
        var dimmed     = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                             config.TextColor.B, config.TextColor.A * 0.45f);
        var centerLine = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                             config.TextColor.B, 0.70f);
        // Bar background: slightly lighter than overlay background.
        var barBg = Resources.GetBrush(
            Math.Min(1f, config.BackgroundColor.R + 0.12f),
            Math.Min(1f, config.BackgroundColor.G + 0.12f),
            Math.Min(1f, config.BackgroundColor.B + 0.12f),
            0.95f);

        float y = pad;

        // ── Delta text (optional) ─────────────────────────────────────
        if (config.ShowDeltaText)
        {
            var dw            = Resources.WriteFactory;
            var deltaFontSize = MathF.Max(config.FontSize * 1.5f, 18f);
            var textRowH      = deltaFontSize + 6f;
            var bigFmt        = Resources.GetTextFormat("Consolas", deltaFontSize);
            var smallFmt      = Resources.GetTextFormat("Consolas", config.FontSize);

            var deltaStr  = driver == null ? "---.---" : FormatDelta(delta);
            var textBrush = driver == null ? dimmed : fillBrush;

            // Trend arrow to the left of the centered delta text.
            if (config.ShowTrendArrow && driver != null)
            {
                var trend = ComputeTrend();
                if (trend != 0)
                {
                    var arrow = trend > 0 ? "\u25b2" : "\u25bc"; // ▲ / ▼
                    using var arrowLayout = dw.CreateTextLayout(
                        arrow, smallFmt, 20f, textRowH);
                    arrowLayout.TextAlignment = TextAlignment.Leading;
                    // Vertically center the small arrow within the larger text row.
                    float arrowY = y + (textRowH - (config.FontSize + 6f)) / 2f;
                    context.DrawTextLayout(new Vector2(pad, arrowY), arrowLayout, textBrush, DrawTextOptions.Clip);
                }
            }

            // Delta value: centered across the full inner width.
            using var textLayout = dw.CreateTextLayout(deltaStr, bigFmt, innerW, textRowH);
            textLayout.TextAlignment = TextAlignment.Center;
            context.DrawTextLayout(new Vector2(pad, y), textLayout, textBrush, DrawTextOptions.Clip);

            y += textRowH + 4f;
        }

        // ── Delta bar ─────────────────────────────────────────────────
        float barH = h - y - pad;
        if (barH < 4f) return;

        float centerX  = pad + innerW / 2f;
        float fillFrac = Math.Clamp(MathF.Abs(delta) / config.DeltaBarMaxSeconds, 0f, 1f);
        float fillW    = fillFrac * (innerW / 2f);

        // Bar background (slightly lighter dark panel).
        context.FillRectangle(new Vortice.RawRectF(pad, y, pad + innerW, y + barH), barBg);

        // Colored fill: extends left from center when faster, right when slower.
        if (fillW > 0f)
        {
            var fillRect = isFaster
                ? new Vortice.RawRectF(centerX - fillW, y, centerX,        y + barH)
                : new Vortice.RawRectF(centerX,         y, centerX + fillW, y + barH);
            context.FillRectangle(fillRect, fillBrush);
        }

        // Center zero line.
        context.DrawLine(new Vector2(centerX, y), new Vector2(centerX, y + barH), centerLine, 2f);
    }

    // ── Trend buffer ──────────────────────────────────────────────────────

    private void PushTrend(float delta)
    {
        _trendBuf[_trendHead] = delta;
        _trendHead = (_trendHead + 1) % TrendSamples;
        if (_trendCount < TrendSamples) _trendCount++;
    }

    /// <summary>
    /// Returns +1 if gap is increasing (▲ getting slower), -1 if decreasing (▼ getting faster), 0 if flat.
    /// </summary>
    private int ComputeTrend()
    {
        if (_trendCount < TrendSamples) return 0;

        // _trendHead is the next slot to write — currently holds the oldest value.
        float oldest = _trendBuf[_trendHead];
        float newest = _trendBuf[(_trendHead - 1 + TrendSamples) % TrendSamples];
        float diff   = MathF.Abs(newest) - MathF.Abs(oldest);

        if (diff >  0.01f) return  1;
        if (diff < -0.01f) return -1;
        return 0;
    }

    // ── Formatting helper ─────────────────────────────────────────────────

    private static string FormatDelta(float delta) =>
        delta == 0f ? " 0.000" : (delta < 0f ? $"{delta:F3}" : $"+{delta:F3}");
}
