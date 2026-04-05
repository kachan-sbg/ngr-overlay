using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Session info panel — track name, session type/remaining, elapsed, wall clock, game time,
/// air/track temps, and current/last/best/delta lap times.
/// </summary>
public sealed class SessionInfoOverlay : BaseOverlay
{
    public const string OverlayId   = "SessionInfo";
    public const string WindowTitle = "SimOverlay \u2014 Session Info";

    public static OverlayConfig DefaultConfig => new()
    {
        Id              = OverlayId,
        Enabled         = true,
        X               = 100,
        Y               = 600,
        Width           = 260,
        Height          = 280,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize        = 13f,
        ShowWeather     = true,
        ShowDelta       = true,
        ShowGameTime    = true,
    };

    private volatile SessionData? _session;
    private volatile DriverData?  _driver;

    public SessionInfoOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<SessionData>(data => _session = data);
        Subscribe<DriverData>(data  => _driver  = data);
    }

    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        var session = _session;
        var driver  = _driver;

        var fontSize = config.FontSize;
        var charW    = fontSize * 0.615f;
        var rowH     = fontSize + 6f;
        var pad      = 8f;
        var w        = (float)config.Width;

        var dw     = Resources.WriteFactory;
        var fmt    = Resources.GetTextFormat("Consolas", fontSize);
        var text   = Resources.GetBrush(config.TextColor);
        var dimmed = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                         config.TextColor.B, config.TextColor.A * 0.55f);
        var sep    = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                         config.TextColor.B, 0.25f);
        var green  = Resources.GetBrush(0f, 0.867f, 0f, 1f);
        var red    = Resources.GetBrush(0.867f, 0.133f, 0.133f, 1f);

        // Label column: 9 chars wide; value takes the rest.
        float labelW = 9f * charW;
        float valueX = pad + labelW;
        float valueW = w - valueX - pad;

        float y = pad;

        // ── Track name ──────────────────────────────────────────────────
        DrawL(context, dw, fmt, text, session?.TrackName ?? "\u2014", pad, y, w - 2f * pad, rowH);
        y += rowH;

        // ── Session type + remaining/laps ──────────────────────────────
        DrawL(context, dw, fmt, dimmed, FormatSessionLine(session, driver), pad, y, w - 2f * pad, rowH);
        y += rowH + 2f;

        // ── Separator ─────────────────────────────────────────────────
        context.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 4f;

        // ── Session elapsed ───────────────────────────────────────────
        DrawRow(context, dw, fmt, dimmed, text, "Session",
            session != null ? FormatElapsed(session.SessionTimeElapsed) : "--:--:--",
            pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // ── Wall clock ────────────────────────────────────────────────
        var clockStr = config.Use12HourClock
            ? DateTime.Now.ToString("hh:mm:ss tt")
            : DateTime.Now.ToString("HH:mm:ss");
        DrawRow(context, dw, fmt, dimmed, text, "Clock", clockStr, pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // ── Game time of day (optional) ───────────────────────────────
        if (config.ShowGameTime)
        {
            var gameStr = session != null
                ? $"{session.GameTimeOfDay:HH:mm} ({GetTimeOfDayDesc(session.GameTimeOfDay.Hour)})"
                : "--:-- (--)";
            DrawRow(context, dw, fmt, dimmed, text, "Game", gameStr, pad, y, labelW, valueX, valueW, rowH);
            y += rowH;
        }

        // ── Weather section (optional) ────────────────────────────────
        if (config.ShowWeather)
        {
            y += 2f;
            context.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
            y += 4f;

            DrawRow(context, dw, fmt, dimmed, text, "Air",
                session != null ? FormatTemp(session.AirTempC,   config.TemperatureUnit) : "---",
                pad, y, labelW, valueX, valueW, rowH);
            y += rowH;

            DrawRow(context, dw, fmt, dimmed, text, "Track",
                session != null ? FormatTemp(session.TrackTempC, config.TemperatureUnit) : "---",
                pad, y, labelW, valueX, valueW, rowH);
            y += rowH;
        }

        // ── Lap data ──────────────────────────────────────────────────
        y += 2f;
        context.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 4f;

        DrawRow(context, dw, fmt, dimmed, text, "Lap",
            FormatLap(driver, session), pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        DrawRow(context, dw, fmt, dimmed, text, "Last",
            driver?.LastLapTime > TimeSpan.Zero ? FormatLapTime(driver.LastLapTime) : "--:--.---",
            pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        DrawRow(context, dw, fmt, dimmed, text, "Best",
            driver?.BestLapTime > TimeSpan.Zero ? FormatLapTime(driver.BestLapTime) : "--:--.---",
            pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // ── Delta (optional, colored) ─────────────────────────────────
        if (config.ShowDelta)
        {
            var delta      = driver?.LapDeltaVsBestLap ?? 0f;
            var deltaStr   = driver != null ? FormatDelta(delta) : "---.---";
            var deltaBrush = driver == null ? dimmed : (delta <= 0f ? green : red);
            DrawRow(context, dw, fmt, dimmed, deltaBrush, "Delta", deltaStr, pad, y, labelW, valueX, valueW, rowH);
        }
    }

    // ── Layout helpers ────────────────────────────────────────────────────

    private static void DrawRow(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush labelBrush, ID2D1Brush valueBrush,
        string label, string value,
        float x, float y, float labelW, float valueX, float valueW, float rowH)
    {
        DrawL(ctx, dw, fmt, labelBrush, label, x,      y, labelW, rowH);
        DrawL(ctx, dw, fmt, valueBrush, value, valueX, y, valueW, rowH);
    }

    private static void DrawL(ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float colW, float colH)
    {
        using var layout = dw.CreateTextLayout(text, fmt, colW, colH);
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }

    // ── Formatting helpers ────────────────────────────────────────────────

    private static string FormatSessionLine(SessionData? session, DriverData? driver)
    {
        if (session == null) return "Waiting for session";

        var type = session.SessionType switch
        {
            SessionType.Practice  => "Practice",
            SessionType.Qualify   => "Qualifying",
            SessionType.Race      => "Race",
            SessionType.TimeTrial => "Time Trial",
            SessionType.Warmup    => "Warmup",
            _                     => "Session",
        };

        if (session.TotalLaps > 0)
        {
            var rem = driver != null
                ? Math.Max(0, session.TotalLaps - driver.Lap)
                : session.TotalLaps;
            return $"{type} \u00b7 {rem} laps remaining";
        }

        return $"{type} \u00b7 {FormatCountdown(session.SessionTimeRemaining)} remaining";
    }

    /// <summary>Always HH:MM:SS — for session elapsed display.</summary>
    private static string FormatElapsed(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    /// <summary>MM:SS or H:MM:SS — for session countdown display.</summary>
    private static string FormatCountdown(TimeSpan ts)
    {
        if (ts <= TimeSpan.Zero) return "0:00";
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    private static string FormatLapTime(TimeSpan ts)
    {
        int m  = (int)ts.TotalMinutes;
        int s  = ts.Seconds;
        int ms = ts.Milliseconds;
        return $"{m}:{s:D2}.{ms:D3}";
    }

    private static string FormatDelta(float delta) =>
        delta == 0f ? " 0.000" : (delta < 0f ? $"{delta:F3}" : $"+{delta:F3}");

    private static string FormatTemp(float tempC, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit
            ? $"{tempC * 9f / 5f + 32f:F1}\u00b0F"
            : $"{tempC:F1}\u00b0C";

    private static string FormatLap(DriverData? driver, SessionData? session)
    {
        if (driver == null) return "---";
        return session?.TotalLaps > 0
            ? $"{driver.Lap} / {session.TotalLaps}"
            : driver.Lap.ToString();
    }

    private static string GetTimeOfDayDesc(int hour) => hour switch
    {
        < 6  => "night",
        < 12 => "morning",
        < 17 => "afternoon",
        < 21 => "evening",
        _    => "night",
    };
}
