using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Diagnostics;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace NrgOverlay.Overlays;

/// <summary>
/// Animated green/red delta bar around a center-zero line.
/// </summary>
public sealed class DeltaBarOverlay : BaseOverlay
{
    public const string OverlayId = "DeltaBar";
    public const string WindowTitle = "NrgOverlay - Delta";

    public static OverlayConfig DefaultConfig => new()
    {
        Id = OverlayId,
        Enabled = true,
        X = 100,
        Y = 780,
        Width = 300,
        Height = 90,
        Opacity = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0f },
        FontSize = 13f,
        DeltaBarMaxSeconds = 3f,
        // Pastel defaults, still fully configurable.
        FasterColor = new ColorConfig { R = 0.56f, G = 0.85f, B = 0.66f, A = 0.95f },
        SlowerColor = new ColorConfig { R = 0.95f, G = 0.65f, B = 0.65f, A = 0.95f },
        ShowTrendArrow = true,
        ShowDeltaText = true,
        ShowReferenceLapTime = true,
    };

    private volatile DriverData? _driver;

    private static readonly DriverData MockDriver = new()
    {
        Position = 5,
        Lap = 12,
        LastLapTime = TimeSpan.FromSeconds(94.521),
        BestLapTime = TimeSpan.FromSeconds(93.887),
        SessionBestLapTime = TimeSpan.FromSeconds(93.451),
        LapDeltaVsBestLap = -0.234f,
        LapDeltaVsSessionBest = +0.202f,
    };

    private const int TrendSamples = 30;
    private readonly float[] _trendBuf = new float[TrendSamples];
    private int _trendHead;
    private int _trendCount;

    private bool _deltaInitialized;
    private float _smoothedDelta;
    private const float DeltaEmaAlpha = 0.12f;

    protected override bool DrawBaseBackground => false;

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

        var sessionDelta = driver?.LapDeltaVsSessionBest ?? 0f;
        var personalDelta = driver?.LapDeltaVsBestLap ?? 0f;
        if (float.IsNaN(sessionDelta)) sessionDelta = 0f;
        if (float.IsNaN(personalDelta)) personalDelta = 0f;

        var usingSessionBest = driver?.HasSessionBestReference == true
            || (driver?.SessionBestLapTime ?? TimeSpan.Zero) > TimeSpan.Zero;
        var rawDelta = usingSessionBest ? sessionDelta : personalDelta;
        var delta = SmoothDelta(rawDelta, DeltaEmaAlpha);

        PushTrend(delta);

        var pad = 8f;
        var w = (float)config.Width;
        var h = (float)config.Height;
        var innerW = w - 2f * pad;

        bool isFaster = delta <= 0f;
        var baseFill = isFaster ? config.FasterColor : config.SlowerColor;
        float maxSeconds = Math.Clamp(config.DeltaBarMaxSeconds, 0.25f, 30f);
        float fillFrac = Math.Clamp(MathF.Abs(delta) / maxSeconds, 0f, 1f);
        float opacityFactor = 0.2f + (0.8f * fillFrac); // 0s => 20%, max => 100%
        float barAlpha = Math.Clamp(baseFill.A * opacityFactor, 0f, 1f);
        var fillBrush = Resources.GetBrush(baseFill.R, baseFill.G, baseFill.B, barAlpha);
        var textBrushFixed = Resources.GetBrush(baseFill.R, baseFill.G, baseFill.B, 1f);
        var dimmed = Resources.GetBrush(
            config.TextColor.R,
            config.TextColor.G,
            config.TextColor.B,
            config.TextColor.A * 0.45f);
        var referenceTextBrush = Resources.GetBrush(
            config.TextColor.R,
            config.TextColor.G,
            config.TextColor.B,
            Math.Clamp(config.TextColor.A * 0.95f, 0f, 1f));
        var centerLine = Resources.GetBrush(
            config.TextColor.R,
            config.TextColor.G,
            config.TextColor.B,
            0.70f);

        float y = pad;
        var dw = Resources.WriteFactory;
        var smallFmt = Resources.GetTextFormat("Oswald", config.FontSize);

        if (config.ShowDeltaText)
        {
            var deltaFontSize = MathF.Max(config.FontSize * 1.5f, 18f);
            var textRowH = deltaFontSize + 6f;
            var bigFmt = Resources.GetTextFormat(
                "Oswald",
                deltaFontSize,
                FontWeight.Bold,
                Vortice.DirectWrite.FontStyle.Normal);

            var deltaStr = driver == null ? "---.---" : FormatDelta(delta);
            var textBrush = driver == null ? dimmed : textBrushFixed;

            if (config.ShowTrendArrow && driver != null)
            {
                var trend = ComputeTrend();
                if (trend != 0)
                {
                    var arrow = trend > 0 ? "^" : "v";
                    using var arrowLayout = dw.CreateTextLayout(arrow, smallFmt, 20f, textRowH);
                    arrowLayout.TextAlignment = TextAlignment.Leading;
                    float arrowY = y + (textRowH - (config.FontSize + 6f)) / 2f;
                    context.DrawTextLayout(new Vector2(pad, arrowY), arrowLayout, textBrush, DrawTextOptions.Clip);
                }
            }

            using var textLayout = dw.CreateTextLayout(deltaStr, bigFmt, innerW, textRowH);
            textLayout.TextAlignment = TextAlignment.Center;
            context.DrawTextLayout(new Vector2(pad, y), textLayout, textBrush, DrawTextOptions.Clip);

            if (driver != null)
            {
                var referenceLabel = usingSessionBest ? "Session Best" : "All Time Best";
                var referenceTime = usingSessionBest ? driver.SessionBestLapTime : driver.BestLapTime;

                using var labelLayout = dw.CreateTextLayout(referenceLabel, smallFmt, innerW - 12f, textRowH);
                labelLayout.TextAlignment = TextAlignment.Leading;
                context.DrawTextLayout(new Vector2(pad + 6f, y), labelLayout, referenceTextBrush, DrawTextOptions.Clip);

                if (config.ShowReferenceLapTime)
                {
                    var timeText = FormatLapTime(referenceTime);
                    using var timeLayout = dw.CreateTextLayout(timeText, smallFmt, innerW - 12f, textRowH);
                    timeLayout.TextAlignment = TextAlignment.Trailing;
                    context.DrawTextLayout(new Vector2(pad + 6f, y), timeLayout, referenceTextBrush, DrawTextOptions.Clip);
                }
            }

            y += textRowH + 2f;
        }
        else if (driver != null && config.ShowReferenceLapTime)
        {
            var referenceLabel = usingSessionBest ? "Session Best" : "All Time Best";
            var referenceTime = usingSessionBest ? driver.SessionBestLapTime : driver.BestLapTime;

            using var labelLayout = dw.CreateTextLayout(referenceLabel, smallFmt, innerW - 12f, config.FontSize + 8f);
            labelLayout.TextAlignment = TextAlignment.Leading;
            context.DrawTextLayout(new Vector2(pad + 6f, y), labelLayout, referenceTextBrush, DrawTextOptions.Clip);

            var timeText = FormatLapTime(referenceTime);
            using var timeLayout = dw.CreateTextLayout(timeText, smallFmt, innerW - 12f, config.FontSize + 8f);
            timeLayout.TextAlignment = TextAlignment.Trailing;
            context.DrawTextLayout(new Vector2(pad + 6f, y), timeLayout, referenceTextBrush, DrawTextOptions.Clip);
            y += config.FontSize + 8f;
        }

        float barH = h - y - pad;
        if (barH < 4f)
            return;

        float centerX = pad + innerW / 2f;
        float fillW = fillFrac * (innerW / 2f);

        if (fillW > 0f)
        {
            var fillRect = isFaster
                ? new Vortice.RawRectF(centerX - fillW, y, centerX, y + barH)
                : new Vortice.RawRectF(centerX, y, centerX + fillW, y + barH);
            context.FillRectangle(fillRect, fillBrush);
        }

        context.DrawLine(new Vector2(centerX, y), new Vector2(centerX, y + barH), centerLine, 2f);
    }

    private float SmoothDelta(float rawDelta, float emaAlpha)
    {
        emaAlpha = Math.Clamp(emaAlpha, 0.01f, 0.8f);

        if (!_deltaInitialized)
        {
            _smoothedDelta = rawDelta;
            _deltaInitialized = true;
            return _smoothedDelta;
        }

        _smoothedDelta = emaAlpha * rawDelta + ((1f - emaAlpha) * _smoothedDelta);
        return _smoothedDelta;
    }

    private void PushTrend(float delta)
    {
        _trendBuf[_trendHead] = delta;
        _trendHead = (_trendHead + 1) % TrendSamples;
        if (_trendCount < TrendSamples)
            _trendCount++;
    }

    /// <summary>
    /// Returns +1 if gap is increasing, -1 if decreasing, 0 if flat.
    /// </summary>
    private int ComputeTrend()
    {
        if (_trendCount < TrendSamples)
            return 0;

        float oldest = _trendBuf[_trendHead];
        float newest = _trendBuf[(_trendHead - 1 + TrendSamples) % TrendSamples];
        float diff = MathF.Abs(newest) - MathF.Abs(oldest);

        if (diff > 0.01f) return 1;
        if (diff < -0.01f) return -1;
        return 0;
    }

    private static string FormatDelta(float delta)
    {
        if (float.IsNaN(delta) || delta == 0f)
            return " 0.000";

        var s = delta.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return delta < 0f ? s : $"+{s}";
    }

    private static string FormatLapTime(TimeSpan lap)
    {
        if (lap <= TimeSpan.Zero)
            return "--:--.---";

        int totalMinutes = (int)lap.TotalMinutes;
        int seconds = lap.Seconds;
        int millis = lap.Milliseconds;
        return $"{totalMinutes}:{seconds:00}.{millis:000}";
    }
}
