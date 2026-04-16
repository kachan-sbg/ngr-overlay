using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace NrgOverlay.Overlays;

/// <summary>
/// Session info panel вЂ” track name, session type/remaining, elapsed, wall clock, game time,
/// air/track temps, and current/last/SB/PB lap times.
/// </summary>
public sealed class SessionInfoOverlay : BaseOverlay
{
    public const string OverlayId   = "SessionInfo";
    public const string WindowTitle = "NrgOverlay \u2014 Session Info";

    public static OverlayConfig DefaultConfig => new()
    {
        Id              = OverlayId,
        Enabled         = true,
        X               = 100,
        Y               = 600,
        Width           = 260,
        Height          = 310,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize        = 13f,
        ShowWeather     = true,
        ShowDelta       = true,
        ShowGameTime    = true,
    };

    private volatile SessionData? _session;
    private volatile DriverData?  _driver;
    private volatile TelemetryData? _telemetry;

    // Smoothed session timing вЂ” avoids display blinking caused by occasional SDK
    // returning 0 for SessionTime/SessionTimeRemain between valid samples.
    // Only updated when the SDK delivers a plausible (non-zero, non-decreasing) value.
    private TimeSpan  _syncedElapsed   = TimeSpan.Zero;
    private TimeSpan? _syncedRemaining;
    private DateTime  _syncWallClock   = DateTime.MinValue;

    private static readonly TimeSpan SyncTolerance = TimeSpan.FromSeconds(5);

    // в”Ђв”Ђ Edit-mode mock data в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private static readonly SessionData MockSession = new()
    {
        TrackName            = "Silverstone GP",
        SessionType          = SessionType.Race,
        TotalLaps            = 30,
        PlayerCountOverall   = 34,
        PlayerCountInClass   = 12,
        StrengthOfFieldOverall = 3120,
        StrengthOfFieldInClass = 2985,
        IncidentDriveThroughLimit = 17,
        IncidentDisqualificationLimit = 40,
        AirTempC             = 22.1f,
        TrackTempC           = 38.7f,
        RelativeHumidity     = 0.62f,
        WeatherDeclaredWet   = false,
        TrackWetness         = 1,
    };
    private static readonly DriverData MockDriver = new()
    {
        Position              = 5,
        Lap                   = 12,
        LastLapTime           = TimeSpan.FromSeconds(94.521),
        BestLapTime           = TimeSpan.FromSeconds(93.887),
        SessionBestLapTime    = TimeSpan.FromSeconds(93.102),
        LapDeltaVsBestLap     = -0.034f,
        SessionTimeElapsed    = TimeSpan.FromMinutes(18).Add(TimeSpan.FromSeconds(34)),
        SessionTimeRemaining  = TimeSpan.FromMinutes(27),
        GameTimeOfDay         = new TimeOnly(14, 45, 0),
        EstimatedLapsRemaining = 18,
    };
    private static readonly TelemetryData MockTelemetry = new(
        Throttle: 0f,
        Brake: 0f,
        Clutch: 0f,
        SteeringAngle: 0f,
        SpeedMps: 0f,
        Gear: 4,
        Rpm: 6200f,
        FuelLevelLiters: 42f,
        FuelConsumptionPerLap: 2.35f,
        LastLapFuelLiters: 2.41f,
        IncidentCount: 5);

    public SessionInfoOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<SessionData>(data =>
        {
            _session = data;
            // Reset the timing sync reference on session change so the new
            // session's elapsed starts fresh instead of inheriting stale values.
            _syncedElapsed   = TimeSpan.Zero;
            _syncedRemaining = null;
            _syncWallClock   = DateTime.MinValue;
        });

        Subscribe<DriverData>(data =>
        {
            _driver = data;
            // Keep remaining synced whenever it's available, even if elapsed is temporarily invalid.
            if (data.SessionTimeRemaining.HasValue && data.SessionTimeRemaining.Value >= TimeSpan.Zero)
                _syncedRemaining = data.SessionTimeRemaining;

            // Accept SDK elapsed only when it is non-zero and not more than
            // SyncTolerance behind our current local estimate (filters blips).
            if (data.SessionTimeElapsed > TimeSpan.Zero)
            {
                var localNow = _syncWallClock == DateTime.MinValue
                    ? TimeSpan.Zero
                    : _syncedElapsed + (DateTime.UtcNow - _syncWallClock);

                if (_syncWallClock == DateTime.MinValue ||
                    data.SessionTimeElapsed >= localNow - SyncTolerance)
                {
                    _syncedElapsed   = data.SessionTimeElapsed;
                    _syncWallClock   = DateTime.UtcNow;
                }
            }
        });

        Subscribe<TelemetryData>(data => _telemetry = data);
    }

    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        var session = IsLocked ? _session : MockSession;
        var driver  = IsLocked ? _driver  : MockDriver;
        var telemetry = IsLocked ? _telemetry : MockTelemetry;

        var fontSize = config.FontSize;
        var charW    = fontSize * 0.615f;
        var rowH     = fontSize + 6f;
        var pad      = 8f;
        var w        = (float)config.Width;

        var dw     = Resources.WriteFactory;
        var fmt    = Resources.GetTextFormat("Oswald", fontSize);
        var text   = Resources.GetBrush(config.TextColor);
        var dimmed = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                         config.TextColor.B, config.TextColor.A * 0.55f);
        var sep    = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                         config.TextColor.B, 0.25f);
        var green  = Resources.GetBrush(0f, 0.867f, 0f, 1f);
        var red    = Resources.GetBrush(0.867f, 0.133f, 0.133f, 1f);
        var purple = Resources.GetBrush(0.7f, 0.3f, 1f, 1f);

        // Label column: 9 chars wide; value takes the rest.
        float labelW = 9f * charW;
        float valueX = pad + labelW;
        float valueW = w - valueX - pad;

        // в”Ђв”Ђ Compute smoothed session timing (before any rendering) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        TimeSpan smoothedElapsed;
        TimeSpan? smoothedRemaining;
        if (IsLocked)
        {
            var drift = _syncWallClock == DateTime.MinValue
                ? TimeSpan.Zero
                : DateTime.UtcNow - _syncWallClock;
            smoothedElapsed   = _syncedElapsed + drift;
            smoothedRemaining = _syncedRemaining.HasValue
                ? _syncedRemaining.Value - drift
                : (TimeSpan?)null;

        }
        else
        {
            smoothedElapsed   = MockDriver.SessionTimeElapsed;
            smoothedRemaining = MockDriver.SessionTimeRemaining;
        }

        float y = pad;

        // в”Ђв”Ђ Track name в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        DrawL(context, dw, fmt, text, session?.TrackName ?? "\u2014", pad, y, w - 2f * pad, rowH);
        y += rowH;

        // в”Ђв”Ђ Session type (stable, no multiplexed values) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        DrawL(context, dw, fmt, dimmed, FormatSessionType(session), pad, y, w - 2f * pad, rowH);
        y += rowH + 2f;

        // в”Ђв”Ђ Separator в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        context.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 4f;

        // в”Ђв”Ђ Separate remaining fields (time + laps) to avoid blinking between meanings в”Ђв”Ђ
        var remainingTimeText = smoothedRemaining.HasValue && smoothedRemaining.Value >= TimeSpan.Zero
            ? FormatCountdown(smoothedRemaining.Value)
            : "--:--";
        DrawRow(context, dw, fmt, dimmed, text, "Remain", remainingTimeText, pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        var remainingLaps = GetRemainingLaps(session, driver);
        var remainingLapsText = remainingLaps.HasValue
            ? remainingLaps.Value.ToString()
            : "--";
        DrawRow(context, dw, fmt, dimmed, text, "Laps", remainingLapsText, pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // в”Ђв”Ђ Session elapsed в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        DrawRow(context, dw, fmt, dimmed, text, "Elapsed",
            smoothedElapsed > TimeSpan.Zero ? FormatElapsed(smoothedElapsed) : "--:--:--",
            pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        if (session != null && session.PlayerCountOverall > 0)
        {
            var fieldStr = session.PlayerCountInClass > 0 && session.PlayerCountInClass != session.PlayerCountOverall
                ? $"{session.PlayerCountOverall} ({session.PlayerCountInClass} cls)"
                : session.PlayerCountOverall.ToString();
            DrawRow(context, dw, fmt, dimmed, text, "Field", fieldStr, pad, y, labelW, valueX, valueW, rowH);
            y += rowH;
        }

        if (session != null && session.StrengthOfFieldOverall > 0)
        {
            var sofStr = session.StrengthOfFieldInClass > 0 && session.StrengthOfFieldInClass != session.StrengthOfFieldOverall
                ? $"{session.StrengthOfFieldOverall} ({session.StrengthOfFieldInClass} cls)"
                : session.StrengthOfFieldOverall.ToString();
            DrawRow(context, dw, fmt, dimmed, text, "SOF", sofStr, pad, y, labelW, valueX, valueW, rowH);
            y += rowH;
        }

        if (telemetry != null)
        {
            var dt = session?.IncidentDriveThroughLimit ?? 0;
            var dq = session?.IncidentDisqualificationLimit ?? 0;
            var incidents = telemetry.IncidentCount;
            var incidentsStr = dt > 0 || dq > 0
                ? $"{incidents} / DT {FmtLimit(dt)} / DSQ {FmtLimit(dq)}"
                : incidents.ToString();
            DrawRow(context, dw, fmt, dimmed, text, "Inc", incidentsStr, pad, y, labelW, valueX, valueW, rowH);
            y += rowH;
        }

        // в”Ђв”Ђ Wall clock в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var clockStr = config.Use12HourClock
            ? DateTime.Now.ToString("hh:mm:ss tt")
            : DateTime.Now.ToString("HH:mm:ss");
        DrawRow(context, dw, fmt, dimmed, text, "Clock", clockStr, pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // в”Ђв”Ђ Game time of day (live from DriverData, optional) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        if (config.ShowGameTime)
        {
            // Prefer the live DriverData.GameTimeOfDay (60 Hz) over the stale session snapshot.
            var gameToD = driver?.GameTimeOfDay ?? session?.GameTimeOfDay;
            string gameStr = gameToD.HasValue
                ? $"{gameToD.Value:HH:mm} ({GetTimeOfDayDesc(gameToD.Value.Hour)})"
                : "--:-- (??)";
            DrawRow(context, dw, fmt, dimmed, text, "Game", gameStr, pad, y, labelW, valueX, valueW, rowH);
            y += rowH;
        }

        // в”Ђв”Ђ Weather section (optional) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

            if (session != null && session.RelativeHumidity > 0f)
            {
                var humStr = $"{(session.RelativeHumidity * 100f).ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}%";
                DrawRow(context, dw, fmt, dimmed, text, "Humidity", humStr, pad, y, labelW, valueX, valueW, rowH);
                y += rowH;
            }

            if (session != null && session.TrackWetness > 0)
            {
                var wetnessStr = FormatTrackWetness(session.TrackWetness, session.WeatherDeclaredWet);
                DrawRow(context, dw, fmt, dimmed, text, "Condition", wetnessStr, pad, y, labelW, valueX, valueW, rowH);
                y += rowH;
            }
        }

        // в”Ђв”Ђ Lap data в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

        // Session best (purple вЂ” typically the track authority)
        var sbTime  = driver?.SessionBestLapTime ?? TimeSpan.Zero;
        var sbStr   = sbTime > TimeSpan.Zero ? FormatLapTime(sbTime) : "--:--.---";
        DrawRow(context, dw, fmt, dimmed, purple, "SB", sbStr, pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // Personal best
        DrawRow(context, dw, fmt, dimmed, text, "PB",
            driver?.BestLapTime > TimeSpan.Zero ? FormatLapTime(driver.BestLapTime) : "--:--.---",
            pad, y, labelW, valueX, valueW, rowH);
        y += rowH;

        // в”Ђв”Ђ Delta (optional, colored) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        if (config.ShowDelta)
        {
            var deltaRaw   = driver?.LapDeltaVsBestLap ?? 0f;
            var delta      = float.IsNaN(deltaRaw) ? 0f : deltaRaw;
            var deltaStr   = driver != null ? FormatDelta(delta) : "---.---";
            var deltaBrush = driver == null ? dimmed : (delta <= 0f ? green : red);
            DrawRow(context, dw, fmt, dimmed, deltaBrush, "Delta", deltaStr, pad, y, labelW, valueX, valueW, rowH);
        }
    }

    // в”Ђв”Ђ Layout helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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
        ctx.DrawTextLayout(new System.Numerics.Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }

    // в”Ђв”Ђ Formatting helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static string FormatSessionType(SessionData? session)
    {
        if (session == null) return "Waiting for session";

        return session.SessionType switch
        {
            SessionType.Practice  => "Practice",
            SessionType.Qualify   => "Qualifying",
            SessionType.Race      => "Race",
            SessionType.TimeTrial => "Time Trial",
            SessionType.Warmup    => "Warmup",
            _                     => "Session",
        };
    }

    private static int? GetRemainingLaps(SessionData? session, DriverData? driver)
    {
        if (session == null)
            return null;

        if (session.TotalLaps > 0)
        {
            if (driver?.SessionLapsRemaining is >= 0 and < short.MaxValue)
                return driver.SessionLapsRemaining.Value;

            return driver != null
                ? Math.Max(0, session.TotalLaps - driver.Lap)
                : session.TotalLaps;
        }

        if (driver?.EstimatedLapsRemaining is > 0)
            return driver.EstimatedLapsRemaining.Value;

        return null;
    }

    /// <summary>Always HH:MM:SS вЂ” for session elapsed display.</summary>
    private static string FormatElapsed(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    /// <summary>MM:SS or H:MM:SS вЂ” for session countdown display.</summary>
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

    private static string FormatDelta(float delta)
    {
        if (float.IsNaN(delta) || delta == 0f) return " 0.000";
        var s = delta.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        return delta < 0f ? s : $"+{s}";
    }

    internal static string FormatTemp(float tempC, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit
            ? $"{(tempC * 9f / 5f + 32f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}\u00b0F"
            : $"{tempC.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}\u00b0C";

    private static string FormatLap(DriverData? driver, SessionData? session)
    {
        if (driver == null) return "---";
        return session?.TotalLaps > 0
            ? $"{driver.Lap} / {session.TotalLaps}"
            : driver.Lap.ToString();
    }

    private static string FormatTrackWetness(int wetness, bool declaredWet)
    {
        _ = declaredWet; // administrative flag not displayed вЂ” physically confusing when track is dry
        return wetness switch
        {
            1 => "Dry",
            2 => "Mostly dry",
            3 => "V. light wet",
            4 => "Light wet",
            5 => "Moderate wet",
            6 => "Very wet",
            7 => "Extreme wet",
            _ => "Unknown",
        };
    }

    private static string GetTimeOfDayDesc(int hour) => hour switch
    {
        < 6  => "night",
        < 12 => "morning",
        < 17 => "afternoon",
        < 21 => "evening",
        _    => "night",
    };

    private static string FmtLimit(int value) => value > 0 ? value.ToString() : "--";
}



