using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Pit planning overlay. Shows planned service (tyre change + calculated fuel add) at all
/// times, plus speed compliance while on pit road.
/// </summary>
public sealed class PitHelperOverlay : BaseOverlay
{
    public const string OverlayId   = "PitHelper";
    public const string WindowTitle = "SimOverlay \u2014 Pit";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                   = OverlayId,
        Enabled              = false,
        X                    = 100,
        Y                    = 100,
        Width                = 280,
        Height               = 200,
        Opacity              = 0.85f,
        BackgroundColor      = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize             = 13f,
        ShowPitServices      = true,
        ShowNextPitEstimate  = true,
        SpeedUnit            = SpeedUnit.Kph,
    };

    private volatile PitData?       _pit;
    private volatile TelemetryData? _telemetry;
    private volatile SessionData?   _session;
    private volatile DriverData?    _driver;

    // Mock: fuel + all tyres planned, on pit road
    private static readonly PitData MockPit = new(
        IsOnPitRoad:        true,
        IsInPitStall:       false,
        PitLimiterSpeedMps: 60f / 3.6f,
        CurrentSpeedMps:    58.3f / 3.6f,
        PitLimiterActive:   true,
        PitStopCount:       2,
        RequestedService:   PitServiceFlags.Fuel | PitServiceFlags.AllTires,
        FuelToAddLiters:    18.7f);

    private static readonly TelemetryData MockTelemetry = new(
        Throttle: 0, Brake: 0, Clutch: 0, SteeringAngle: 0,
        SpeedMps: 58.3f / 3.6f, Gear: 1, Rpm: 2000,
        FuelLevelLiters: 12.4f,
        FuelConsumptionPerLap: 2.83f,
        LastLapFuelLiters: 2.91f,
        IncidentCount: 0);

    private static readonly SessionData MockSession = new()
    {
        SessionType = SessionType.Race,
        TotalLaps   = 20,
    };

    private static readonly DriverData MockDriver = new()
    {
        Lap = 10,
        SessionTimeRemaining = TimeSpan.FromMinutes(28),
        BestLapTime = TimeSpan.FromSeconds(94.5),
    };

    public PitHelperOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<PitData>(data       => _pit       = data);
        Subscribe<TelemetryData>(data => _telemetry = data);
        Subscribe<SessionData>(data   => _session   = data);
        Subscribe<DriverData>(data    => _driver    = data);
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        bool edit  = !IsLocked;
        var  pit   = edit ? MockPit       : _pit;
        var  telem = edit ? MockTelemetry : _telemetry;
        var  sess  = edit ? MockSession   : _session;
        var  drv   = edit ? MockDriver    : _driver;

        float pad    = 8f;
        float w      = (float)cfg.Width;
        float h      = (float)cfg.Height;
        float rowH   = cfg.FontSize + 6f;
        var   dw     = Resources.WriteFactory;
        var   fmt    = Resources.GetTextFormat("Consolas", cfg.FontSize);
        var   bigFmt = Resources.GetTextFormat("Consolas", MathF.Max(cfg.FontSize * 1.4f, 18f));
        var   text   = Resources.GetBrush(cfg.TextColor);
        var   dimmed = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, cfg.TextColor.A * 0.45f);
        var   sep    = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, 0.25f);
        var   green  = Resources.GetBrush(0f,    0.87f, 0f,   1f);
        var   red    = Resources.GetBrush(0.87f, 0.13f, 0.13f, 1f);
        var   accent = Resources.GetBrush(0.2f,  0.8f,  1f,   1f);

        float innerW = w - 2f * pad;
        float labelW = innerW * 0.55f;
        float valueW = innerW - labelW;
        float xL = pad, xV = pad + labelW;

        float y = pad;

        // ── ON PIT ROAD: speed compliance header ──────────────────────────────
        bool onPitRoad = pit?.IsOnPitRoad ?? false;

        if (onPitRoad)
        {
            using var titleLayout = dw.CreateTextLayout("PIT ROAD", bigFmt, innerW, rowH + 6f);
            titleLayout.TextAlignment = TextAlignment.Center;
            ctx.DrawTextLayout(new Vector2(pad, y), titleLayout, text, DrawTextOptions.Clip);
            y += rowH + 6f;

            ctx.DrawLine(new Vector2(pad, y),       new Vector2(w - pad, y),       sep, 2f);
            ctx.DrawLine(new Vector2(pad, y + 2f),  new Vector2(w - pad, y + 2f),  sep, 2f);
            y += 6f;

            bool limitKnown = (pit?.PitLimiterSpeedMps ?? 0f) > 0.1f;
            bool kph        = cfg.SpeedUnit == SpeedUnit.Kph;
            float factor    = kph ? 3.6f : 2.23694f;
            string unitStr  = kph ? "km/h" : "mph";

            if (limitKnown)
            {
                float limitMps  = pit!.PitLimiterSpeedMps;
                float speedMps  = pit.CurrentSpeedMps;
                bool  ok        = speedMps <= limitMps + 0.5f;
                var   indBrush  = ok ? green : red;
                string indStr   = ok ? "\u2713" : "\u2717";

                DrawRow(ctx, dw, fmt, text, dimmed, "Limit",
                    $"{limitMps * factor:F1} {unitStr}", xL, xV, y, labelW, valueW, rowH);
                y += rowH;

                DrawRow(ctx, dw, fmt, text, dimmed, "Speed",
                    $"{speedMps * factor:F1} {unitStr}", xL, xV, y, labelW, valueW, rowH);
                // compliance tick/cross at right edge
                float indX = w - pad - cfg.FontSize * 1.5f;
                using var indLayout = dw.CreateTextLayout(indStr, fmt, cfg.FontSize * 1.5f, rowH);
                indLayout.TextAlignment = TextAlignment.Trailing;
                ctx.DrawTextLayout(new Vector2(indX, y), indLayout, indBrush, DrawTextOptions.Clip);
                y += rowH;
            }
            else if (pit != null && pit.CurrentSpeedMps > 0f)
            {
                bool kph2      = cfg.SpeedUnit == SpeedUnit.Kph;
                float factor2  = kph2 ? 3.6f : 2.23694f;
                string unit2   = kph2 ? "km/h" : "mph";
                DrawRow(ctx, dw, fmt, text, dimmed, "Limit", "??",   xL, xV, y, labelW, valueW, rowH);
                y += rowH;
                DrawRow(ctx, dw, fmt, text, dimmed, "Speed",
                    $"{pit.CurrentSpeedMps * factor2:F1} {unit2}", xL, xV, y, labelW, valueW, rowH);
                y += rowH;
            }

            ctx.DrawLine(new Vector2(pad, y + 1f), new Vector2(w - pad, y + 1f), sep, 1f);
            y += 5f;
        }

        // ── PLANNED SERVICE (always shown) ────────────────────────────────────
        var svc = pit?.RequestedService ?? PitServiceFlags.None;

        // Tyres
        if (cfg.ShowPitServices)
        {
            string tyreStr = FormatTyres(svc);
            DrawRow(ctx, dw, fmt, text, dimmed, "Tyres", tyreStr, xL, xV, y, labelW, valueW, rowH);
            y += rowH;
        }

        // Calculated fuel to add (same formula as FuelCalculatorOverlay)
        float? calcFuelAdd = ComputeFuelAdd(telem, sess, drv);
        string fuelAddStr;
        if (calcFuelAdd.HasValue)
        {
            bool gallons  = cfg.FuelUnit == FuelUnit.Gallons;
            float disp    = gallons ? calcFuelAdd.Value * 0.264172f : calcFuelAdd.Value;
            string uStr   = gallons ? "gal" : "L";
            fuelAddStr    = $"{disp:F1} {uStr}";
        }
        else
        {
            fuelAddStr = "\u2014";
        }
        DrawRow(ctx, dw, fmt, accent, dimmed, "Fuel Add", fuelAddStr, xL, xV, y, labelW, valueW, rowH);
        y += rowH;

        ctx.DrawLine(new Vector2(pad, y + 1f), new Vector2(w - pad, y + 1f), sep, 1f);
        y += 5f;

        // ── PIT STOP COUNT ────────────────────────────────────────────────────
        DrawRow(ctx, dw, fmt, text, dimmed, "Pit Stops",
            (pit?.PitStopCount ?? 0).ToString(), xL, xV, y, labelW, valueW, rowH);
        y += rowH;

        // ── NEXT STOP ESTIMATE ────────────────────────────────────────────────
        if (cfg.ShowNextPitEstimate && !onPitRoad && telem != null)
        {
            float fuel = telem.FuelLevelLiters;
            float avg  = telem.FuelConsumptionPerLap;
            if (avg > 0.001f)
            {
                float lapsLeft = fuel / avg;
                DrawRow(ctx, dw, fmt, text, dimmed, "Fuel left",
                    $"~{lapsLeft:F1} laps", xL, xV, y, labelW, valueW, rowH);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the calculated fuel-to-add for the next pit stop.
    /// Mirrors FuelCalculatorOverlay's logic (fuel needed to finish − current level).
    /// Returns null when not enough data is available yet.
    /// </summary>
    private static float? ComputeFuelAdd(TelemetryData? telem, SessionData? sess, DriverData? drv)
    {
        if (telem == null) return null;

        float fuelL  = telem.FuelLevelLiters;
        float avgL   = telem.FuelConsumptionPerLap;
        if (avgL <= 0.001f) return null;   // no average yet

        bool isRace       = sess?.SessionType == SessionType.Race;
        bool isLapLimited = isRace && (sess?.TotalLaps ?? 0) > 0;

        float? fuelToFinish = null;

        if (isLapLimited && sess != null && drv != null)
        {
            int remaining = Math.Max(0, sess.TotalLaps - drv.Lap);
            fuelToFinish = remaining * avgL;
        }
        else if (isRace && !isLapLimited && sess != null && drv != null)
        {
            // Time-limited: estimate laps from best/last lap time
            TimeSpan? avgLap = drv.BestLapTime.TotalSeconds > 10 ? drv.BestLapTime
                             : drv.LastLapTime.TotalSeconds > 10 ? drv.LastLapTime
                             : (TimeSpan?)null;
            if (avgLap.HasValue && drv.SessionTimeRemaining.HasValue)
            {
                double lapsEst = drv.SessionTimeRemaining.Value.TotalSeconds / avgLap.Value.TotalSeconds;
                fuelToFinish = (float)lapsEst * avgL;
            }
        }

        if (!fuelToFinish.HasValue) return null;
        return MathF.Max(0f, fuelToFinish.Value - fuelL);
    }

    /// <summary>
    /// Formats the tyre service plan from the iRacing pit-menu flags.
    /// </summary>
    private static string FormatTyres(PitServiceFlags svc)
    {
        bool lf = svc.HasFlag(PitServiceFlags.LeftFrontTire);
        bool rf = svc.HasFlag(PitServiceFlags.RightFrontTire);
        bool lr = svc.HasFlag(PitServiceFlags.LeftRearTire);
        bool rr = svc.HasFlag(PitServiceFlags.RightRearTire);

        int count = (lf ? 1 : 0) + (rf ? 1 : 0) + (lr ? 1 : 0) + (rr ? 1 : 0);

        if (count == 0) return "None \u2014";
        if (count == 4) return "All 4 \u2713";
        if (lf && rf && !lr && !rr) return "Front \u2713";
        if (!lf && !rf && lr && rr) return "Rear \u2713";

        // Mixed: list individual corners
        var parts = new List<string>(4);
        if (lf) parts.Add("LF");
        if (rf) parts.Add("RF");
        if (lr) parts.Add("LR");
        if (rr) parts.Add("RR");
        return string.Join(" ", parts) + " \u2713";
    }

    private static void DrawRow(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush labelBrush, ID2D1Brush valueBrush,
        string label, string value,
        float xL, float xV, float y, float labelW, float valueW, float rowH)
    {
        using var ll = dw.CreateTextLayout(label, fmt, labelW, rowH);
        ll.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(xL, y), ll, labelBrush, DrawTextOptions.Clip);

        using var vl = dw.CreateTextLayout(value, fmt, valueW, rowH);
        vl.TextAlignment = TextAlignment.Trailing;
        ctx.DrawTextLayout(new Vector2(xV, y), vl, valueBrush, DrawTextOptions.Clip);
    }
}
